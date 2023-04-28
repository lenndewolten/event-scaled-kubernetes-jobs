﻿using Azure.Storage.Blobs;
using Azure.Storage.Queues;
using Dequeueable.AzureQueueStorage.IntegrationTests.Fixtures;
using Dequeueable.AzureQueueStorage.IntegrationTests.TestDataBuilders;
using Dequeueable.AzureQueueStorage.Models;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Moq;

namespace Dequeueable.AzureQueueStorage.IntegrationTests.Functions
{
    public class ListenerTests : IClassFixture<AzuriteFixture>, IAsyncLifetime
    {
        private readonly QueueClientOptions _queueClientOptions = new() { MessageEncoding = QueueMessageEncoding.Base64 };
        private readonly AzuriteFixture _azuriteFixture;
        private readonly string _queueName;
        private readonly QueueClient _queueClient;

        public ListenerTests(AzuriteFixture azuriteFixture)
        {
            _azuriteFixture = azuriteFixture;
            _queueName = "jobqueue";
            _queueClient = new QueueClient(_azuriteFixture.ConnectionString, _queueName, _queueClientOptions);
        }

        public Task InitializeAsync()
        {
            return _queueClient.CreateAsync();
        }

        public Task DisposeAsync()
        {
            return _queueClient.DeleteAsync();
        }

        [Fact]
        public async Task Given_a_Queue_when_is_has_two_messages_then_they_are_handled_correctly()
        {
            // Arrange
            var factory = new ListenerHostFactory<TestFunction>(_azuriteFixture.ConnectionString, _queueName);

            var fakeServiceMock = new Mock<IFakeService>();

            var messages = new[] { "message1", "message2" };

            factory.ConfigureTestServices(services =>
            {
                services.AddTransient(_ => fakeServiceMock.Object);
            });

            foreach (var message in messages)
            {
                await _queueClient.SendMessageAsync(message);
            }

            // Act
            var host = factory.Build();
            await host.HandleAsync(CancellationToken.None);

            // Assert
            var peekedMessage = await _queueClient.PeekMessageAsync();
            peekedMessage.Value.Should().BeNull();

            foreach (var message in messages)
            {
                fakeServiceMock.Verify(f => f.Execute(It.Is<Message>(m => m.Body.ToString() == message)), Times.Once());
            }
        }

        [Fact]
        public async Task Given_a_QueueMessage_with_DequeueCount_1_when_an_error_occurred_while_executing_the_function_and_the_MaxDequeueCount_is_not_yet_reached_then_the_message_is_enqueued_correctly()
        {
            // Arrange
            var factory = new ListenerHostFactory<TestFunction>(_azuriteFixture.ConnectionString, _queueName, options =>
            {
                options.MaxDequeueCount = 5;
            });

            var fakeServiceMock = new Mock<IFakeService>();
            fakeServiceMock.Setup(f => f.Execute(It.IsAny<Message>())).ThrowsAsync(new Exception("Test exception"));

            factory.ConfigureTestServices(services =>
            {
                services.AddTransient(_ => fakeServiceMock.Object);
            });

            var message = "message1";
            await _queueClient.SendMessageAsync(message);

            // Act
            var host = factory.Build();
            await host.HandleAsync(CancellationToken.None);

            // Assert
            var peekedMessage = await _queueClient.PeekMessageAsync();
            peekedMessage.Value.Should().NotBeNull();
            peekedMessage.Value.Body.ToString().Should().Be(message);
            peekedMessage.Value.DequeueCount.Should().Be(1);
        }

        [Fact]
        public async Task Given_a_QueueMessage_with_DequeueCount_1_when_an_error_occurred_while_executing_the_function_and_the_MaxDequeueCount_is_reached_then_the_message_is_moved_to_the_poisen_queue()
        {
            // Arrange
            var poisenQueueSuffix = "poison";

            var factory = new ListenerHostFactory<TestFunction>(_azuriteFixture.ConnectionString, _queueName, options =>
            {
                options.MaxDequeueCount = 1;
                options.PoisonQueueSuffix = poisenQueueSuffix;
            });

            var fakeServiceMock = new Mock<IFakeService>();
            fakeServiceMock.Setup(f => f.Execute(It.IsAny<Message>())).ThrowsAsync(new Exception("Test exception"));

            factory.ConfigureTestServices(services =>
            {
                services.AddTransient(_ => fakeServiceMock.Object);
            });

            var message = "message1";
            await _queueClient.SendMessageAsync(message);

            // Act
            var host = factory.Build();
            await host.HandleAsync(CancellationToken.None);

            // Assert
            var peekedMessage = await _queueClient.PeekMessageAsync();
            peekedMessage.Value.Should().BeNull();

            var poisenQueueClient = new QueueClient(_azuriteFixture.ConnectionString, $"{_queueName}-{poisenQueueSuffix}", _queueClientOptions);

            var peekedPoisonQueueMessage = await poisenQueueClient.PeekMessageAsync();
            peekedPoisonQueueMessage.Value.Should().NotBeNull();
            peekedPoisonQueueMessage.Value.Body.ToString().Should().Be(message);
        }

        [Fact]
        public async Task Given_a_Function_with_a_singleton_attribute_when_a_queue_has_two_messages_then_they_are_handled_correctly()
        {
            // Arrange
            var scope = "testscope";
            var factory = new ListenerHostFactory<SingletonFunction>(_azuriteFixture.ConnectionString, _queueName);

            var fakeServiceMock = new Mock<IFakeService>();

            factory.ConfigureTestServices(services =>
            {
                services.AddTransient(_ => fakeServiceMock.Object);
            });

            var blobContainerClient = new BlobContainerClient(_azuriteFixture.ConnectionString, SingletonFunction.ContainerName);

            var messages = new[] { new { Id = scope }, new { Id = scope } };
            foreach (var message in messages)
            {
                await _queueClient.SendMessageAsync(BinaryData.FromObjectAsJson(message));
            }

            // Act
            var host = factory.Build();
            await host.HandleAsync(CancellationToken.None);

            // Assert
            fakeServiceMock.Verify(f => f.Execute(It.IsAny<Message>()), Times.AtLeastOnce());
            var peekedMessage = await _queueClient.PeekMessageAsync();
            peekedMessage.Value.Should().BeNull();

            var blobclient = blobContainerClient.GetBlobClient(scope);
            (await blobclient.ExistsAsync()).Value.Should().BeTrue();
        }
    }
}
