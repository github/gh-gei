using System;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Moq;
using Moq.Protected;
using Xunit;

namespace OctoshiftCLI.Tests;

public sealed class GithubClientTests : IDisposable
{
    private readonly Mock<HttpMessageHandler> _handlerMock;
    private readonly Mock<HttpMessageHandler> _handlerMockWithRequestBodyMatcher;
    private readonly Mock<OctoLogger> _loggerMock;
    private readonly HttpClient _httpClientForGet;
    private readonly HttpClient _httpClientForPost;
    private readonly HttpResponseMessage _httpResponse;
    private const string EXPECTED_RESPONSE_CONTENT = "RESPONSE_CONTENT";
    private const string EXPECTED_REQUEST_BODY = "REQUEST_BODY";

    public GithubClientTests()
    {
        _httpResponse = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(EXPECTED_RESPONSE_CONTENT)
        };

        _handlerMock = new Mock<HttpMessageHandler>();
        _handlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(_httpResponse);

        _handlerMockWithRequestBodyMatcher = new Mock<HttpMessageHandler>();
        _handlerMockWithRequestBodyMatcher
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.Is<HttpRequestMessage>(x => x.Content.ReadAsStringAsync().Result == EXPECTED_REQUEST_BODY),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(_httpResponse);

        _loggerMock = new Mock<OctoLogger>();

        _httpClientForGet = new HttpClient(_handlerMock.Object);
        _httpClientForPost = new HttpClient(_handlerMockWithRequestBodyMatcher.Object);
    }

    public void Dispose()
    {
        _httpResponse?.Dispose();
        _httpClientForGet?.Dispose();
        _httpClientForPost?.Dispose();
    }

    [Fact]
    public async Task GetAsync_Returns_String_Response()
    {
        // Arrange
        using var githubClient = new GithubClient(_loggerMock.Object, _httpClientForGet);

        // Act
        var actualContent = await githubClient.GetAsync("http://example.com");

        // Assert
        actualContent.Should().Be(EXPECTED_RESPONSE_CONTENT);
    }

    [Fact]
    public async Task GetAsync_Encodes_The_Url()
    {
        // Arrange
        using var githubClient = new GithubClient(_loggerMock.Object, _httpClientForGet);

        const string actualUrl = "http://example.com/param with space";
        const string expectedUrl = "http://example.com/param%20with%20space";

        // Act
        await githubClient.GetAsync(actualUrl);

        // Assert
        _handlerMock.Protected().Verify(
            "SendAsync",
            Times.Once(),
            ItExpr.Is<HttpRequestMessage>(msg => msg.RequestUri.AbsoluteUri == expectedUrl),
            ItExpr.IsAny<CancellationToken>());
    }

    [Fact]
    public async Task GetAsync_Logs_The_Url()
    {
        // Arrange
        using var githubClient = new GithubClient(_loggerMock.Object, _httpClientForGet);

        const string url = "http://example.com";
        var expectedLogMessage = $"HTTP GET: {url}";

        // Act
        await githubClient.GetAsync(url);

        // Assert
        _loggerMock.Verify(m =>
            m.LogVerbose(It.Is<string>(actualLogMessage => actualLogMessage == expectedLogMessage)));
    }

    [Fact]
    public async Task GetAsync_Logs_The_Response_Status_Code_And_Content()
    {
        // Arrange
        using var githubClient = new GithubClient(_loggerMock.Object, _httpClientForGet);

        var expectedLogMessage = $"RESPONSE ({HttpStatusCode.OK}): {EXPECTED_RESPONSE_CONTENT}";

        // Act
        await githubClient.GetAsync("http://example.com");

        // Assert
        _loggerMock.Verify(m =>
            m.LogVerbose(It.Is<string>(actualLogMessage => actualLogMessage == expectedLogMessage)));
    }

    [Fact]
    public async Task GetAsync_Throws_HttpRequestException_On_Non_Success_Response()
    {
        // Arrange
        using var httpResponse = new HttpResponseMessage(HttpStatusCode.InternalServerError);
        var handlerMock = new Mock<HttpMessageHandler>();
        handlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(httpResponse);

        // Act
        var func = () =>
        {
            using var httpClient = new HttpClient(handlerMock.Object);
            using var githubClient = new GithubClient(_loggerMock.Object, httpClient);
            return githubClient.GetAsync("http://example.com");
        };

        // Assert
        await func.Should().ThrowExactlyAsync<HttpRequestException>();
    }

    [Fact]
    public async Task PostAsync_Returns_String_Response()
    {
        // Arrange
        using var githubClient = new GithubClient(_loggerMock.Object, _httpClientForPost);

        // Act
        var actualContent = await githubClient.PostAsync("http://example.com", EXPECTED_REQUEST_BODY);

        // Assert
        actualContent.Should().Be(EXPECTED_RESPONSE_CONTENT);
    }

    [Fact]
    public async Task PostAsync_Encodes_The_Url()
    {
        // Arrange
        using var githubClient = new GithubClient(_loggerMock.Object, _httpClientForPost);

        const string actualUrl = "http://example.com/param with space";
        const string expectedUrl = "http://example.com/param%20with%20space";

        // Act
        await githubClient.PostAsync(actualUrl, EXPECTED_REQUEST_BODY);

        // Assert
        _handlerMockWithRequestBodyMatcher.Protected().Verify(
            "SendAsync",
            Times.Once(),
            ItExpr.Is<HttpRequestMessage>(msg => msg.RequestUri.AbsoluteUri == expectedUrl),
            ItExpr.IsAny<CancellationToken>());
    }

    [Fact]
    public async Task PostAsync_Logs_The_Url()
    {
        // Arrange
        using var githubClient = new GithubClient(_loggerMock.Object, _httpClientForPost);

        const string url = "http://example.com";
        var expectedLogMessage = $"HTTP POST: {url}";

        // Act
        await githubClient.PostAsync(url, EXPECTED_REQUEST_BODY);

        // Assert
        _loggerMock.Verify(m =>
            m.LogVerbose(It.Is<string>(actualLogMessage => actualLogMessage == expectedLogMessage)));
    }

    [Fact]
    public async Task PostAsync_Logs_The_Request_Body()
    {
        // Arrange
        using var githubClient = new GithubClient(_loggerMock.Object, _httpClientForPost);

        var expectedLogMessage = $"HTTP BODY: {EXPECTED_REQUEST_BODY}";

        // Act
        await githubClient.PostAsync("http://example.com", EXPECTED_REQUEST_BODY);

        // Assert
        _loggerMock.Verify(m =>
            m.LogVerbose(It.Is<string>(actualLogMessage => actualLogMessage == expectedLogMessage)));
    }

    [Fact]
    public async Task PostAsync_Logs_The_Response_Status_Code_And_Content()
    {
        // Arrange
        using var githubClient = new GithubClient(_loggerMock.Object, _httpClientForPost);

        var expectedLogMessage = $"RESPONSE ({_httpResponse.StatusCode}): {EXPECTED_RESPONSE_CONTENT}";

        // Act
        await githubClient.PostAsync("http://example.com", EXPECTED_REQUEST_BODY);

        // Assert
        _loggerMock.Verify(m =>
            m.LogVerbose(It.Is<string>(actualLogMessage => actualLogMessage == expectedLogMessage)));
    }

    [Fact]
    public async Task PostAsync_Throws_HttpRequestException_On_Non_Success_Response()
    {
        // Arrange
        using var httpResponse = new HttpResponseMessage(HttpStatusCode.InternalServerError);
        var handlerMock = new Mock<HttpMessageHandler>();
        handlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.Is<HttpRequestMessage>(x => x.Content.ReadAsStringAsync().Result == EXPECTED_REQUEST_BODY),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(httpResponse);

        var loggerMock = new Mock<OctoLogger>();

        // Act
        var func = () =>
        {
            using var httpClient = new HttpClient(handlerMock.Object);
            using var githubClient = new GithubClient(loggerMock.Object, httpClient);
            return githubClient.PostAsync("http://example.com", EXPECTED_REQUEST_BODY);
        };

        // Assert
        await func.Should().ThrowExactlyAsync<HttpRequestException>();
    }

    [Fact]
    public async Task PutAsync_Returns_String_Response()
    {
        // Arrange
        using var githubClient = new GithubClient(_loggerMock.Object, _httpClientForPost);

        // Act
        var actualContent = await githubClient.PutAsync("http://example.com", EXPECTED_REQUEST_BODY);

        // Assert
        actualContent.Should().Be(EXPECTED_RESPONSE_CONTENT);
    }

    [Fact]
    public async Task PutAsync_Encodes_The_Url()
    {
        // Arrange
        using var githubClient = new GithubClient(_loggerMock.Object, _httpClientForPost);

        const string actualUrl = "http://example.com/param with space";
        const string expectedUrl = "http://example.com/param%20with%20space";

        // Act
        await githubClient.PutAsync(actualUrl, EXPECTED_REQUEST_BODY);

        // Assert
        _handlerMockWithRequestBodyMatcher.Protected().Verify(
            "SendAsync",
            Times.Once(),
            ItExpr.Is<HttpRequestMessage>(msg => msg.RequestUri.AbsoluteUri == expectedUrl),
            ItExpr.IsAny<CancellationToken>());
    }

    [Fact]
    public async Task PutAsync_Logs_The_Url()
    {
        // Arrange
        using var githubClient = new GithubClient(_loggerMock.Object, _httpClientForPost);

        const string url = "http://example.com";
        var expectedLogMessage = $"HTTP PUT: {url}";

        // Act
        await githubClient.PutAsync(url, EXPECTED_REQUEST_BODY);

        // Assert
        _loggerMock.Verify(m =>
            m.LogVerbose(It.Is<string>(actualLogMessage => actualLogMessage == expectedLogMessage)));
    }

    [Fact]
    public async Task PutAsync_Logs_The_Request_Body()
    {
        // Arrange
        using var githubClient = new GithubClient(_loggerMock.Object, _httpClientForPost);

        var expectedLogMessage = $"HTTP BODY: {EXPECTED_REQUEST_BODY}";

        // Act
        await githubClient.PutAsync("http://example.com", EXPECTED_REQUEST_BODY);

        // Assert
        _loggerMock.Verify(m =>
            m.LogVerbose(It.Is<string>(actualLogMessage => actualLogMessage == expectedLogMessage)));
    }

    [Fact]
    public async Task PutAsync_Logs_The_Response_Status_Code_And_Content()
    {
        // Arrange
        using var githubClient = new GithubClient(_loggerMock.Object, _httpClientForPost);

        var expectedLogMessage = $"RESPONSE ({_httpResponse.StatusCode}): {EXPECTED_RESPONSE_CONTENT}";

        // Act
        await githubClient.PutAsync("http://example.com", EXPECTED_REQUEST_BODY);

        // Assert
        _loggerMock.Verify(m =>
            m.LogVerbose(It.Is<string>(actualLogMessage => actualLogMessage == expectedLogMessage)));
    }

    [Fact]
    public async Task PutAsync_Throws_HttpRequestException_On_Non_Success_Response()
    {
        // Arrange
        using var httpResponse = new HttpResponseMessage(HttpStatusCode.InternalServerError);
        var handlerMock = new Mock<HttpMessageHandler>();
        handlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.Is<HttpRequestMessage>(x => x.Content.ReadAsStringAsync().Result == EXPECTED_REQUEST_BODY),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(httpResponse);

        var loggerMock = new Mock<OctoLogger>();

        // Act
        var func = () =>
        {
            using var httpClient = new HttpClient(handlerMock.Object);
            using var githubClient = new GithubClient(loggerMock.Object, httpClient);
            return githubClient.PutAsync("http://example.com", EXPECTED_REQUEST_BODY);
        };

        // Assert
        await func.Should().ThrowExactlyAsync<HttpRequestException>();
    }

    [Fact]
    public async Task PatchAsync_Returns_String_Response()
    {
        // Arrange
        using var githubClient = new GithubClient(_loggerMock.Object, _httpClientForPost);

        // Act
        var actualContent = await githubClient.PatchAsync("http://example.com", EXPECTED_REQUEST_BODY);

        // Assert
        actualContent.Should().Be(EXPECTED_RESPONSE_CONTENT);
    }

    [Fact]
    public async Task PatchAsync_Encodes_The_Url()
    {
        // Arrange
        using var githubClient = new GithubClient(_loggerMock.Object, _httpClientForPost);

        const string actualUrl = "http://example.com/param with space";
        const string expectedUrl = "http://example.com/param%20with%20space";

        // Act
        await githubClient.PatchAsync(actualUrl, EXPECTED_REQUEST_BODY);

        // Assert
        _handlerMockWithRequestBodyMatcher.Protected().Verify(
            "SendAsync",
            Times.Once(),
            ItExpr.Is<HttpRequestMessage>(msg => msg.RequestUri.AbsoluteUri == expectedUrl),
            ItExpr.IsAny<CancellationToken>());
    }

    [Fact]
    public async Task PatchAsync_Logs_The_Url()
    {
        // Arrange
        using var githubClient = new GithubClient(_loggerMock.Object, _httpClientForPost);

        const string url = "http://example.com";
        var expectedLogMessage = $"HTTP PATCH: {url}";

        // Act
        await githubClient.PatchAsync(url, EXPECTED_REQUEST_BODY);

        // Assert
        _loggerMock.Verify(m =>
            m.LogVerbose(It.Is<string>(actualLogMessage => actualLogMessage == expectedLogMessage)));
    }

    [Fact]
    public async Task PatchAsync_Logs_The_Request_Body()
    {
        // Arrange
        using var githubClient = new GithubClient(_loggerMock.Object, _httpClientForPost);

        var expectedLogMessage = $"HTTP BODY: {EXPECTED_REQUEST_BODY}";

        // Act
        await githubClient.PatchAsync("http://example.com", EXPECTED_REQUEST_BODY);

        // Assert
        _loggerMock.Verify(m =>
            m.LogVerbose(It.Is<string>(actualLogMessage => actualLogMessage == expectedLogMessage)));
    }

    [Fact]
    public async Task PatchAsync_Logs_The_Response_Status_Code_And_Content()
    {
        // Arrange
        using var githubClient = new GithubClient(_loggerMock.Object, _httpClientForPost);

        var expectedLogMessage = $"RESPONSE ({_httpResponse.StatusCode}): {EXPECTED_RESPONSE_CONTENT}";

        // Act
        await githubClient.PatchAsync("http://example.com", EXPECTED_REQUEST_BODY);

        // Assert
        _loggerMock.Verify(m =>
            m.LogVerbose(It.Is<string>(actualLogMessage => actualLogMessage == expectedLogMessage)));
    }

    [Fact]
    public async Task PatchAsync_Throws_HttpRequestException_On_Non_Success_Response()
    {
        // Arrange
        using var httpResponse = new HttpResponseMessage(HttpStatusCode.InternalServerError);
        var handlerMock = new Mock<HttpMessageHandler>();
        handlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.Is<HttpRequestMessage>(x => x.Content.ReadAsStringAsync().Result == EXPECTED_REQUEST_BODY),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(httpResponse);

        var loggerMock = new Mock<OctoLogger>();

        // Act
        var func = () =>
        {
            using var httpClient = new HttpClient(handlerMock.Object);
            using var githubClient = new GithubClient(loggerMock.Object, httpClient);
            return githubClient.PatchAsync("http://example.com", EXPECTED_REQUEST_BODY);
        };

        // Assert
        await func.Should().ThrowExactlyAsync<HttpRequestException>();
    }

    [Fact]
    public async Task DeleteAsync_Returns_String_Response()
    {
        // Arrange
        using var githubClient = new GithubClient(_loggerMock.Object, _httpClientForGet);

        // Act
        var actualContent = await githubClient.DeleteAsync("http://example.com");

        // Assert
        actualContent.Should().Be(EXPECTED_RESPONSE_CONTENT);
    }

    [Fact]
    public async Task DeleteAsync_Encodes_The_Url()
    {
        // Arrange
        using var githubClient = new GithubClient(_loggerMock.Object, _httpClientForGet);

        const string actualUrl = "http://example.com/param with space";
        const string expectedUrl = "http://example.com/param%20with%20space";

        // Act
        await githubClient.DeleteAsync(actualUrl);

        // Assert
        _handlerMock.Protected().Verify(
            "SendAsync",
            Times.Once(),
            ItExpr.Is<HttpRequestMessage>(msg => msg.RequestUri.AbsoluteUri == expectedUrl),
            ItExpr.IsAny<CancellationToken>());
    }

    [Fact]
    public async Task DeleteAsync_Logs_The_Url()
    {
        // Arrange
        using var githubClient = new GithubClient(_loggerMock.Object, _httpClientForGet);

        const string url = "http://example.com";
        var expectedLogMessage = $"HTTP DELETE: {url}";

        // Act
        await githubClient.DeleteAsync(url);

        // Assert
        _loggerMock.Verify(m =>
            m.LogVerbose(It.Is<string>(actualLogMessage => actualLogMessage == expectedLogMessage)));
    }

    [Fact]
    public async Task DeleteAsync_Logs_The_Response_Status_Code_And_Content()
    {
        // Arrange
        using var githubClient = new GithubClient(_loggerMock.Object, _httpClientForGet);

        var expectedLogMessage = $"RESPONSE ({HttpStatusCode.OK}): {EXPECTED_RESPONSE_CONTENT}";

        // Act
        await githubClient.DeleteAsync("http://example.com");

        // Assert
        _loggerMock.Verify(m =>
            m.LogVerbose(It.Is<string>(actualLogMessage => actualLogMessage == expectedLogMessage)));
    }

    [Fact]
    public async Task DeleteAsync_Throws_HttpRequestException_On_Non_Success_Response()
    {
        // Arrange
        using var httpResponse = new HttpResponseMessage(HttpStatusCode.InternalServerError);
        var handlerMock = new Mock<HttpMessageHandler>();
        handlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(httpResponse);

        var loggerMock = new Mock<OctoLogger>();

        // Act
        var func = () =>
        {
            using var httpClient = new HttpClient(handlerMock.Object);
            using var githubClient = new GithubClient(loggerMock.Object, httpClient);
            return githubClient.DeleteAsync("http://example.com");
        };

        // Assert
        await func.Should().ThrowExactlyAsync<HttpRequestException>();
    }
}