using System;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Moq;
using Moq.Protected;
using Xunit;

namespace OctoshiftCLI.Tests;

public sealed class BbsClientTests : IDisposable
{
    private readonly Mock<OctoLogger> _mockOctoLogger = TestHelpers.CreateMock<OctoLogger>();
    private readonly HttpResponseMessage _httpResponse;
    private readonly RetryPolicy _retryPolicy;
    private readonly object _rawRequestBody;
    private const string EXPECTED_JSON_REQUEST_BODY = "{\"id\":\"ID\"}";
    private const string EXPECTED_RESPONSE_CONTENT = "RESPONSE_CONTENT";
    private const string USERNAME = "USER";
    private const string PASSWORD = "abc123";
    private const string URL = "http://example.com/resource";

    public BbsClientTests()
    {
        _rawRequestBody = new { id = "ID" };

        _httpResponse = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(EXPECTED_RESPONSE_CONTENT)
        };

        _retryPolicy = new RetryPolicy(_mockOctoLogger.Object);
    }

    [Fact]
    public void It_Adds_The_Authorization_Header()
    {
        // Arrange
        using var httpClient = new HttpClient(MockHttpHandlerForGet().Object);
        var expectedAuthToken = Convert.ToBase64String(Encoding.ASCII.GetBytes($"{USERNAME}:{PASSWORD}"));

        // Act
        _ = new BbsClient(_mockOctoLogger.Object, httpClient, null, _retryPolicy, USERNAME, PASSWORD);

        // Assert
        httpClient.DefaultRequestHeaders.Authorization.Should().NotBeNull();
        httpClient.DefaultRequestHeaders.Authorization.Parameter.Should().Be(expectedAuthToken);
        httpClient.DefaultRequestHeaders.Authorization.Scheme.Should().Be("Basic");
    }

    [Fact]
    public void It_Doesnt_Add_Authorization_Header_When_No_Credentials_Passed()
    {
        // Arrange
        using var httpClient = new HttpClient(MockHttpHandlerForGet().Object);

        // Act
        _ = new BbsClient(_mockOctoLogger.Object, httpClient, null, _retryPolicy);

        // Assert
        httpClient.DefaultRequestHeaders.Authorization.Should().BeNull();
    }

    [Fact]
    public async Task GetAsync_Encodes_The_Url()
    {
        // Arrange
        var handlerMock = MockHttpHandlerForGet();
        using var httpClient = new HttpClient(handlerMock.Object);
        var bbsClient = new BbsClient(_mockOctoLogger.Object, httpClient, null, _retryPolicy, USERNAME, PASSWORD);

        const string actualUrl = "http://example.com/param with space";
        const string expectedUrl = "http://example.com/param%20with%20space";

        // Act
        await bbsClient.GetAsync(actualUrl);

        // Assert
        handlerMock.Protected().Verify(
            "SendAsync",
            Times.Once(),
            ItExpr.Is<HttpRequestMessage>(msg => msg.RequestUri.AbsoluteUri == expectedUrl),
            ItExpr.IsAny<CancellationToken>());
    }

    [Fact]
    public async Task GetAsync_Logs_The_Url()
    {
        // Arrange
        using var httpClient = new HttpClient(MockHttpHandlerForGet().Object);
        var bbsClient = new BbsClient(_mockOctoLogger.Object, httpClient, null, _retryPolicy, USERNAME, PASSWORD);

        // Act
        await bbsClient.GetAsync(URL);

        // Assert
        _mockOctoLogger.Verify(m => m.LogVerbose($"HTTP GET: {URL}"), Times.Once);
    }

    [Fact]
    public async Task GetAsync_Returns_String_Response()
    {
        // Arrange
        using var httpClient = new HttpClient(MockHttpHandlerForGet().Object);
        var bbsClient = new BbsClient(_mockOctoLogger.Object, httpClient, null, _retryPolicy, USERNAME, PASSWORD);

        // Act
        var actualContent = await bbsClient.GetAsync(URL);

        // Assert
        actualContent.Should().Be(EXPECTED_RESPONSE_CONTENT);
    }

    [Fact]
    public async Task GetAsync_Logs_The_Response_Status_Code_And_Content()
    {
        // Arrange
        using var httpClient = new HttpClient(MockHttpHandlerForGet().Object);
        var bbsClient = new BbsClient(_mockOctoLogger.Object, httpClient, null, _retryPolicy, USERNAME, PASSWORD);

        // Act
        await bbsClient.GetAsync(URL);

        // Assert
        _mockOctoLogger.Verify(m => m.LogVerbose($"RESPONSE ({_httpResponse.StatusCode}): {EXPECTED_RESPONSE_CONTENT}"), Times.Once);
    }

    [Theory]
    [InlineData(HttpStatusCode.Unauthorized)]
    [InlineData(HttpStatusCode.ServiceUnavailable)]
    public async Task GetAsync_Retries_On_Non_Success(HttpStatusCode httpStatusCode)
    {
        // Arrange
        using var firstHttpResponse = new HttpResponseMessage(httpStatusCode)
        {
            Content = new StringContent("FIRST_RESPONSE")
        };
        using var secondHttpResponse = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("SECOND_RESPONSE")
        };
        var handlerMock = new Mock<HttpMessageHandler>();
        handlerMock
            .Protected()
            .SetupSequence<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.Is<HttpRequestMessage>(req => req.Method == HttpMethod.Get),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(firstHttpResponse)
            .ReturnsAsync(secondHttpResponse);

        using var httpClient = new HttpClient(handlerMock.Object);
        var bbsClient = new BbsClient(_mockOctoLogger.Object, httpClient, null, _retryPolicy, USERNAME, PASSWORD);

        // Act
        var returnedContent = await bbsClient.GetAsync(URL);

        // Assert
        returnedContent.Should().Be("SECOND_RESPONSE");
    }

    [Fact]
    public async Task PostAsync_Encodes_The_Url()
    {
        var handlerMock = MockHttpHandlerForPost();
        using var httpClient = new HttpClient(handlerMock.Object);
        var bbsClient = new BbsClient(_mockOctoLogger.Object, httpClient, null, _retryPolicy, USERNAME, PASSWORD);

        const string actualUrl = "http://example.com/param with space";
        const string expectedUrl = "http://example.com/param%20with%20space";

        // Act
        await bbsClient.PostAsync(actualUrl, _rawRequestBody);

        // Assert
        handlerMock.Protected().Verify(
            "SendAsync",
            Times.Once(),
            ItExpr.Is<HttpRequestMessage>(msg => msg.RequestUri.AbsoluteUri == expectedUrl),
            ItExpr.IsAny<CancellationToken>());
    }

    [Fact]
    public async Task PostAsync_Logs_The_Url()
    {
        // Arrange
        using var httpClient = new HttpClient(MockHttpHandlerForPost().Object);
        var bbsClient = new BbsClient(_mockOctoLogger.Object, httpClient, null, _retryPolicy, USERNAME, PASSWORD);

        // Act
        await bbsClient.PostAsync(URL, _rawRequestBody);

        // Assert
        _mockOctoLogger.Verify(m => m.LogVerbose($"HTTP POST: {URL}"), Times.Once);
    }

    [Fact]
    public async Task PostAsync_Logs_The_Request_Body()
    {
        // Arrange
        using var httpClient = new HttpClient(MockHttpHandlerForPost().Object);
        var bbsClient = new BbsClient(_mockOctoLogger.Object, httpClient, null, _retryPolicy, USERNAME, PASSWORD);

        // Act
        await bbsClient.PostAsync(URL, _rawRequestBody);

        // Assert
        _mockOctoLogger.Verify(m => m.LogVerbose($"HTTP BODY: {EXPECTED_JSON_REQUEST_BODY}"), Times.Once);
    }

    [Fact]
    public async Task PostAsync_Returns_String_Response()
    {
        // Arrange
        using var httpClient = new HttpClient(MockHttpHandlerForPost().Object);
        var bbsClient = new BbsClient(_mockOctoLogger.Object, httpClient, null, _retryPolicy, USERNAME, PASSWORD);

        // Act
        var actualContent = await bbsClient.PostAsync(URL, _rawRequestBody);

        // Assert
        actualContent.Should().Be(EXPECTED_RESPONSE_CONTENT);
    }

    [Fact]
    public async Task PostAsync_Logs_The_Response_Status_Code_And_Content()
    {
        // Arrange
        using var httpClient = new HttpClient(MockHttpHandlerForPost().Object);
        var bbsClient = new BbsClient(_mockOctoLogger.Object, httpClient, null, _retryPolicy, USERNAME, PASSWORD);

        // Act
        await bbsClient.PostAsync(URL, _rawRequestBody);

        // Assert
        _mockOctoLogger.Verify(m => m.LogVerbose($"RESPONSE ({_httpResponse.StatusCode}): {EXPECTED_RESPONSE_CONTENT}"), Times.Once);
    }

    private Mock<HttpMessageHandler> MockHttpHandlerForGet() =>
        MockHttpHandler(req => req.Method == HttpMethod.Get);

    private Mock<HttpMessageHandler> MockHttpHandlerForPost() =>
        MockHttpHandler(req =>
            req.Content.ReadAsStringAsync().Result == EXPECTED_JSON_REQUEST_BODY
            && req.Method == HttpMethod.Post);

    private Mock<HttpMessageHandler> MockHttpHandler(
        Func<HttpRequestMessage, bool> requestMatcher,
        HttpResponseMessage httpResponse = null)
    {
        var handlerMock = new Mock<HttpMessageHandler>();
        handlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.Is<HttpRequestMessage>(x => requestMatcher(x)),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(httpResponse ?? _httpResponse);
        return handlerMock;
    }

    public void Dispose() => _httpResponse?.Dispose();
}
