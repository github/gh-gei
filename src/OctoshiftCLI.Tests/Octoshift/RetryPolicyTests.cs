using System;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using FluentAssertions;
using Xunit;

namespace OctoshiftCLI.Tests.Octoshift;

public sealed class RetryPolicyTests
{
    private readonly RetryPolicy _retryPolicy;

    public RetryPolicyTests()
    {
        _retryPolicy = new RetryPolicy(null)
        {
            _retryInterval = 0,
            _httpRetryInterval = 0
        };
    }

    [Theory]
    [InlineData(HttpStatusCode.InternalServerError)]
    [InlineData(HttpStatusCode.BadGateway)]
    [InlineData(HttpStatusCode.ServiceUnavailable)]
    [InlineData(HttpStatusCode.GatewayTimeout)]
    public async Task Retry_Retries_On_5xx_HttpRequestException(HttpStatusCode statusCode)
    {
        // Arrange
        var callCount = 0;

        // Act
        var result = await _retryPolicy.Retry(async () =>
        {
            callCount++;
            return callCount == 1
                ? throw new HttpRequestException("server error", null, statusCode)
                : await Task.FromResult("success");
        });

        // Assert
        result.Should().Be("success");
        callCount.Should().Be(2);
    }

    [Theory]
    [InlineData(HttpStatusCode.BadRequest)]
    [InlineData(HttpStatusCode.NotFound)]
    [InlineData(HttpStatusCode.Conflict)]
    [InlineData(HttpStatusCode.Forbidden)]
    public async Task Retry_Does_Not_Retry_On_4xx_HttpRequestException(HttpStatusCode statusCode)
    {
        // Arrange
        var callCount = 0;

        // Act / Assert
        await FluentActions
            .Invoking(async () => await _retryPolicy.Retry<string>(async () =>
            {
                callCount++;
                throw new HttpRequestException("client error", null, statusCode);
            }))
            .Should()
            .ThrowAsync<HttpRequestException>();

        callCount.Should().Be(1);
    }

    [Fact]
    public async Task Retry_Retries_On_Null_StatusCode_HttpRequestException()
    {
        // Arrange - null status code represents network-level failures
        var callCount = 0;

        // Act
        var result = await _retryPolicy.Retry(async () =>
        {
            callCount++;
            return callCount == 1
                ? throw new HttpRequestException("network error")
                : await Task.FromResult("success");
        });

        // Assert
        result.Should().Be("success");
        callCount.Should().Be(2);
    }

    [Fact]
    public async Task Retry_Retries_On_Non_Http_Exceptions()
    {
        // Arrange
        var callCount = 0;

        // Act
        var result = await _retryPolicy.Retry(async () =>
        {
            callCount++;
            return callCount == 1
                ? throw new TimeoutException("timed out")
                : await Task.FromResult("success");
        });

        // Assert
        result.Should().Be("success");
        callCount.Should().Be(2);
    }

    [Fact]
    public async Task Retry_Does_Not_Retry_On_OctoshiftCliException()
    {
        // Arrange
        var callCount = 0;

        // Act / Assert
        await FluentActions
            .Invoking(async () => await _retryPolicy.Retry<string>(async () =>
            {
                callCount++;
                throw new OctoshiftCliException("terminal error");
            }))
            .Should()
            .ThrowAsync<OctoshiftCliException>();

        callCount.Should().Be(1);
    }

    [Fact]
    public async Task Retry_Throws_OctoshiftCliException_On_401()
    {
        // Arrange
        var callCount = 0;

        // Act / Assert
        await FluentActions
            .Invoking(async () => await _retryPolicy.Retry<string>(async () =>
            {
                callCount++;
                throw new HttpRequestException("unauthorized", null, HttpStatusCode.Unauthorized);
            }))
            .Should()
            .ThrowAsync<OctoshiftCliException>()
            .WithMessage("*Unauthorized*");
    }
}
