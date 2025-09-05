using System;
using FluentAssertions;
using OctoshiftCLI.Models;
using Xunit;

namespace OctoshiftCLI.Tests
{
    public class PipelineTestResultTests
    {
        [Fact]
        public void IsSuccessful_Should_Return_True_For_Succeeded_Status()
        {
            // Arrange
            var result = new PipelineTestResult { Result = "succeeded" };

            // Act & Assert
            result.IsSuccessful.Should().BeTrue();
            result.IsFailed.Should().BeFalse();
        }

        [Fact]
        public void IsSuccessful_Should_Return_True_For_PartiallySucceeded_Status()
        {
            // Arrange
            var result = new PipelineTestResult { Result = "partiallySucceeded" };

            // Act & Assert
            result.IsSuccessful.Should().BeTrue();
            result.IsFailed.Should().BeFalse();
        }

        [Fact]
        public void IsFailed_Should_Return_True_For_Failed_Status()
        {
            // Arrange
            var result = new PipelineTestResult { Result = "failed" };

            // Act & Assert
            result.IsFailed.Should().BeTrue();
            result.IsSuccessful.Should().BeFalse();
        }

        [Fact]
        public void IsFailed_Should_Return_True_For_Canceled_Status()
        {
            // Arrange
            var result = new PipelineTestResult { Result = "canceled" };

            // Act & Assert
            result.IsFailed.Should().BeTrue();
            result.IsSuccessful.Should().BeFalse();
        }

        [Fact]
        public void IsCompleted_Should_Return_True_When_Result_Has_Value()
        {
            // Arrange
            var result = new PipelineTestResult { Result = "succeeded" };

            // Act & Assert
            result.IsCompleted.Should().BeTrue();
        }

        [Fact]
        public void IsCompleted_Should_Return_False_When_Result_Is_Null_Or_Empty()
        {
            // Arrange
            var result1 = new PipelineTestResult { Result = null };
            var result2 = new PipelineTestResult { Result = string.Empty };

            // Act & Assert
            result1.IsCompleted.Should().BeFalse();
            result2.IsCompleted.Should().BeFalse();
        }

        [Fact]
        public void IsRunning_Should_Return_True_For_InProgress_Status()
        {
            // Arrange
            var result = new PipelineTestResult { Status = "inProgress" };

            // Act & Assert
            result.IsRunning.Should().BeTrue();
        }

        [Fact]
        public void IsRunning_Should_Return_True_For_NotStarted_Status()
        {
            // Arrange
            var result = new PipelineTestResult { Status = "notStarted" };

            // Act & Assert
            result.IsRunning.Should().BeTrue();
        }

        [Fact]
        public void BuildDuration_Should_Calculate_Correctly_When_EndTime_Is_Set()
        {
            // Arrange
            var startTime = DateTime.UtcNow;
            var endTime = startTime.AddMinutes(5);
            var result = new PipelineTestResult
            {
                StartTime = startTime,
                EndTime = endTime
            };

            // Act & Assert
            result.BuildDuration.Should().Be(TimeSpan.FromMinutes(5));
        }

        [Fact]
        public void BuildDuration_Should_Return_Null_When_EndTime_Is_Not_Set()
        {
            // Arrange
            var result = new PipelineTestResult
            {
                StartTime = DateTime.UtcNow,
                EndTime = null
            };

            // Act & Assert
            result.BuildDuration.Should().BeNull();
        }
    }
}
