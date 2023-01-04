﻿using FluentAssertions;
using WebJobs.Azure.QueueStorage.Function.Configurations;

namespace WebJobs.Azure.QueueStorage.Function.UnitTests.Configurations
{
    public class FunctionHostOptionsTests
    {
        [Fact]
        public void Given_a_BatchSize_of_five_when_NewBatchThreshold_is_set_to_four_then_it_set_correctly()
        {
            // Arrange
            var expected = 4;
            var sut = new FunctionHostOptions
            {
                BatchSize = 5,
                NewBatchThreshold = expected
            };

            // Act
            var actual = sut.NewBatchThreshold;

            // Assert
            actual.Should().Be(expected);
        }

        [Fact]
        public void Given_a_BatchSize_of_five_when_NewBatchThreshold_is_set_to_six_then_an_ArgumentException_is_thrown()
        {
            // Arrange
            var expected = 6;

            // Act
            Action act = () =>
            {
                var options = new FunctionHostOptions
                {
                    BatchSize = 5,
                    NewBatchThreshold = expected,
                };
            };

            // Assert
            act.Should().ThrowExactly<ArgumentException>();
        }

        [Fact]
        public void Given_a_BatchSize_of_five_when_NewBatchThreshold_is_not_set_then_it_the_getter_returns_the_correct_value()
        {
            // Arrange
            var expected = 3;
            var sut = new FunctionHostOptions
            {
                BatchSize = 5,
            };

            // Act
            var actual = sut.NewBatchThreshold;

            // Assert
            actual.Should().Be(expected);
        }

        [Fact]
        public void Given_a_MinimumPollingIntervalInMilliseconds_when_set_to_zero_then_an_ArgumentOutOfRangeException_is_thrown()
        {
            // Arrange
            var expected = -1;

            // Act
            Action act = () =>
            {
                var options = new FunctionHostOptions
                {
                    MinimumPollingIntervalInMilliseconds = expected,
                };
            };

            // Assert
            act.Should().ThrowExactly<ArgumentOutOfRangeException>();
        }

        [Fact]
        public void Given_a_MaximumPollingIntervalInMilliseconds_when_set_to_zero_then_an_ArgumentOutOfRangeException_is_thrown()
        {
            // Arrange
            var expected = 0;

            // Act
            Action act = () =>
            {
                var options = new FunctionHostOptions
                {
                    MaximumPollingIntervalInMilliseconds = expected,
                };
            };

            // Assert
            act.Should().ThrowExactly<ArgumentOutOfRangeException>();
        }

        [Fact]
        public void Given_a_MaximumPollingIntervalInMilliseconds_of_2_seconds_when_set_lower_than_MinimumPollingInterval_then_an_ArgumentOutOfRangeException_is_thrown()
        {
            // Arrange
            var expected = 2;
            // Act
            Action act = () =>
            {
                var options = new FunctionHostOptions
                {
                    MaximumPollingIntervalInMilliseconds = expected,
                };
            };

            // Assert
            act.Should().ThrowExactly<ArgumentException>();
        }

        [Fact]
        public void Given_a_MaximumPollingIntervalInMilliseconds_of_2_seconds_when_set_then_is_set_correctly()
        {
            // Arrange
            var expected = 2;

            // Act
            var options = new FunctionHostOptions
            {
                MaximumPollingIntervalInMilliseconds = expected,
            };

            // Assert
            options.MaximumPollingIntervalInMilliseconds.Should().Be(expected);
        }
    }
}
