using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Moq;
using Moq.Protected;
using OctoshiftCLI.Extensions;
using OctoshiftCLI.Services;
using Xunit;

namespace OctoshiftCLI.Tests.Octoshift.Services;

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

        _retryPolicy = new RetryPolicy(_mockOctoLogger.Object)
        {
            _httpRetryInterval = 1,
            _retryInterval = 1
        };
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
    [InlineData(HttpStatusCode.InternalServerError)]
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
    public async Task GetAsync_Bubbles_UnAuthorized_Error()
    {
        // Arrange
        using var firstHttpResponse = new HttpResponseMessage(HttpStatusCode.Unauthorized)
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
        // Assert
        await bbsClient
            .Invoking(async x => await x.GetAsync(URL))
            .Should()
            .ThrowExactlyAsync<OctoshiftCliException>()
            .WithMessage("Unauthorized. Please check your token and try again");
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

    [Fact]
    public async Task DeleteAsync_Logs_The_Url()
    {
        // Arrange
        using var httpClient = new HttpClient(MockHttpHandlerForDelete().Object);
        var bbsClient = new BbsClient(_mockOctoLogger.Object, httpClient, null, _retryPolicy, USERNAME, PASSWORD);

        // Act
        await bbsClient.DeleteAsync(URL);

        // Assert
        _mockOctoLogger.Verify(m => m.LogVerbose($"HTTP DELETE: {URL}"), Times.Once);
    }

    [Fact]
    public async Task DeleteAsync_Returns_String_Response()
    {
        // Arrange
        using var httpClient = new HttpClient(MockHttpHandlerForDelete().Object);
        var bbsClient = new BbsClient(_mockOctoLogger.Object, httpClient, null, _retryPolicy, USERNAME, PASSWORD);

        // Act
        var actualContent = await bbsClient.DeleteAsync(URL);

        // Assert
        actualContent.Should().Be(EXPECTED_RESPONSE_CONTENT);
    }

    [Fact]
    public async Task DeleteAsync_Logs_The_Response_Status_Code_And_Content()
    {
        // Arrange
        using var httpClient = new HttpClient(MockHttpHandlerForDelete().Object);
        var bbsClient = new BbsClient(_mockOctoLogger.Object, httpClient, null, _retryPolicy, USERNAME, PASSWORD);

        // Act
        await bbsClient.DeleteAsync(URL);

        // Assert
        _mockOctoLogger.Verify(m => m.LogVerbose($"RESPONSE ({_httpResponse.StatusCode}): {EXPECTED_RESPONSE_CONTENT}"), Times.Once);
    }

    [Fact]
    public async Task GetAllAsync_Should_Get_All_Pages()
    {
        // Arrange
        const string url = "http://localhost:7990/rest/api/1.0/projects";

        var firstValue = new { key = "PR1", id = 1 };
        var secondValue = new { key = "PR2", id = 2 };
        var thirdValue = new { key = "PR3", id = 3 };
        var fourthValue = new { key = "PR4", id = 4 };
        var fifthValue = new { key = "PR5", id = 5 };

        var firstResponseContent = new
        {
            isLastPage = false,
            nextPageStart = 2,
            values = new[]
            {
                firstValue,
                secondValue
            }
        }.ToJson();
        using var firstResponse = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(firstResponseContent)
        };

        var secondResponseContent = new
        {
            isLastPage = false,
            nextPageStart = 4,
            values = new[]
            {
                thirdValue,
                fourthValue
            }
        }.ToJson();
        using var secondResponse = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(secondResponseContent)
        };

        var thirdResponseContent = new
        {
            isLastPage = true,
            values = new[]
            {
                fifthValue
            }
        }.ToJson();
        using var thirdResponse = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(thirdResponseContent)
        };

        var handlerMock = new Mock<HttpMessageHandler>();

        // first request
        MockHttpHandler(
            req => req.Method == HttpMethod.Get && req.RequestUri!.ToString() == $"{url}?start=0&limit=100",
            firstResponse,
            handlerMock);

        // second request
        MockHttpHandler(
            req => req.Method == HttpMethod.Get && req.RequestUri!.ToString() == $"{url}?start=2&limit=100",
            secondResponse,
            handlerMock);

        // third request
        MockHttpHandler(
            req => req.Method == HttpMethod.Get && req.RequestUri!.ToString() == $"{url}?start=4&limit=100",
            thirdResponse,
            handlerMock);

        using var httpClient = new HttpClient(handlerMock.Object);
        var bbsClient = new BbsClient(_mockOctoLogger.Object, httpClient, null, _retryPolicy);

        // Act
        var results = await bbsClient.GetAllAsync(url).ToListAsync();

        // Assert
        results.Should().HaveCount(5);
        results[0].ToJson().Should().Be(firstValue.ToJson());
        results[1].ToJson().Should().Be(secondValue.ToJson());
        results[2].ToJson().Should().Be(thirdValue.ToJson());
        results[3].ToJson().Should().Be(fourthValue.ToJson());
        results[4].ToJson().Should().Be(fifthValue.ToJson());
    }

    [Fact]
    public async Task GetAllAsync_Logs_The_Url_Per_Each_Page_Request()
    {
        // Arrange
        const string url = "http://example.com/resource";

        var firstResponseContent = new { isLastPage = false, nextPageStart = 2, values = Array.Empty<object>() }.ToJson();
        using var firstResponse = new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(firstResponseContent) };

        var secondResponseContent = new { isLastPage = true, values = Array.Empty<object>() }.ToJson();
        using var secondResponse = new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(secondResponseContent) };

        var handlerMock = new Mock<HttpMessageHandler>();

        // first request
        const string firstRequestUrl = $"{url}?start=0&limit=100";
        MockHttpHandler(req => req.Method == HttpMethod.Get && req.RequestUri!.ToString() == firstRequestUrl, firstResponse, handlerMock);

        // second request
        const string secondRequestUrl = $"{url}?start=2&limit=100";
        MockHttpHandler(req => req.Method == HttpMethod.Get && req.RequestUri!.ToString() == secondRequestUrl, secondResponse, handlerMock);

        using var httpClient = new HttpClient(handlerMock.Object);
        var bbsClient = new BbsClient(_mockOctoLogger.Object, httpClient, null, _retryPolicy);

        // Act
        await bbsClient.GetAllAsync(url).ToListAsync();

        // Assert
        _mockOctoLogger.Verify(m => m.LogVerbose($"HTTP GET: {firstRequestUrl}"));
        _mockOctoLogger.Verify(m => m.LogVerbose($"HTTP GET: {secondRequestUrl}"));
    }

    [Fact]
    public async Task GetAllAsync_Logs_The_Response_Per_Each_Page_Request()
    {
        // Arrange
        const string url = "http://example.com/resource";

        var firstResponseContent = new { isLastPage = false, nextPageStart = 2, values = new[] { new { key = "value 1" } } }.ToJson();
        using var firstResponse = new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(firstResponseContent) };

        var secondResponseContent = new { isLastPage = true, values = new[] { new { key = "value 2" } } }.ToJson();
        using var secondResponse = new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(secondResponseContent) };

        var handlerMock = new Mock<HttpMessageHandler>();

        // first request
        const string firstRequestUrl = $"{url}?start=0&limit=100";
        MockHttpHandler(req => req.Method == HttpMethod.Get && req.RequestUri!.ToString() == firstRequestUrl, firstResponse, handlerMock);

        // second request
        const string secondRequestUrl = $"{url}?start=2&limit=100";
        MockHttpHandler(req => req.Method == HttpMethod.Get && req.RequestUri!.ToString() == secondRequestUrl, secondResponse, handlerMock);

        using var httpClient = new HttpClient(handlerMock.Object);
        var bbsClient = new BbsClient(_mockOctoLogger.Object, httpClient, null, _retryPolicy);

        // Act
        await bbsClient.GetAllAsync(url).ToListAsync();

        // Assert
        _mockOctoLogger.Verify(m => m.LogVerbose($"RESPONSE ({HttpStatusCode.OK}): {firstResponseContent}"));
        _mockOctoLogger.Verify(m => m.LogVerbose($"RESPONSE ({HttpStatusCode.OK}): {secondResponseContent}"));
    }

    [Fact]
    public async Task GetAllAsync_Throws_HttpRequestException_On_Non_Success_Response()
    {
        // Arrange
        const string url = "http://example.com/resource";

        var firstResponseContent = new { isLastPage = false, nextPageStart = 2, values = Array.Empty<object>() }.ToJson();
        using var firstResponse = new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(firstResponseContent) };

        using var secondResponse = new HttpResponseMessage(HttpStatusCode.InternalServerError);

        var handlerMock = new Mock<HttpMessageHandler>();

        // first request
        const string firstRequestUrl = $"{url}?start=0&limit=100";
        MockHttpHandler(req => req.Method == HttpMethod.Get && req.RequestUri!.ToString() == firstRequestUrl, firstResponse, handlerMock);

        // second request
        const string secondRequestUrl = $"{url}?start=2&limit=100";
        MockHttpHandler(req => req.Method == HttpMethod.Get && req.RequestUri!.ToString() == secondRequestUrl, secondResponse, handlerMock);

        using var httpClient = new HttpClient(handlerMock.Object);
        var bbsClient = new BbsClient(_mockOctoLogger.Object, httpClient, null, _retryPolicy);

        // Act, Assert
        await bbsClient
            .Invoking(async x => await x.GetAllAsync(url).ToListAsync())
            .Should()
            .ThrowExactlyAsync<HttpRequestException>();
    }

    [Fact]
    public async Task GetAllAsync_Overrides_Pagination_Query_Params_In_Request_Url()
    {
        // Arrange
        const string actualUrl = "http://example.com/resource?start=1&limit=1";
        const string expectedUrl = "http://example.com/resource?start=0&limit=100";

        var responseContent = new { values = Array.Empty<object>() }.ToJson();
        using var response = new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(responseContent) };

        var handlerMock = MockHttpHandler(req => req.Method == HttpMethod.Get, response);

        using var httpClient = new HttpClient(handlerMock.Object);
        var bbsClient = new BbsClient(_mockOctoLogger.Object, httpClient, null, _retryPolicy);

        // Act
        await bbsClient.GetAllAsync(actualUrl).ToListAsync();

        // Assert
        handlerMock.Protected().Verify(
            "SendAsync",
            Times.Once(),
            ItExpr.Is<HttpRequestMessage>(msg => msg.RequestUri.ToString() == expectedUrl),
            ItExpr.IsAny<CancellationToken>());
    }

    [Fact]
    public async Task GetAllAsync_Retries_On_Non_Success_Response()
    {
        const string url = "http://example.com/resource";

        var firstResponseContent = new { isLastPage = false, nextPageStart = 2, values = new[] { new { key = "value 1" } } }.ToJson();
        using var firstResponse = new HttpResponseMessage(HttpStatusCode.InternalServerError) { Content = new StringContent(firstResponseContent) };

        var expectedValue = new { key = "value 2" };
        var secondResponseContent = new { isLastPage = true, values = new[] { expectedValue } }.ToJson();
        using var secondResponse = new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(secondResponseContent) };

        var handlerMock = new Mock<HttpMessageHandler>();
        handlerMock
            .Protected()
            .SetupSequence<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.Is<HttpRequestMessage>(req => req.Method == HttpMethod.Get),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(firstResponse)
            .ReturnsAsync(secondResponse);

        using var httpClient = new HttpClient(handlerMock.Object);
        var bbsClient = new BbsClient(_mockOctoLogger.Object, httpClient, null, _retryPolicy);

        // Act
        var results = await bbsClient.GetAllAsync(url).ToListAsync();

        // Assert
        results.Should().HaveCount(1);
        results[0].ToJson().Should().Be(expectedValue.ToJson());
    }

    private Mock<HttpMessageHandler> MockHttpHandlerForGet() =>
        MockHttpHandler(req => req.Method == HttpMethod.Get);

    private Mock<HttpMessageHandler> MockHttpHandlerForPost() =>
        MockHttpHandler(req =>
            req.Content.ReadAsStringAsync().Result == EXPECTED_JSON_REQUEST_BODY
            && req.Method == HttpMethod.Post);

    private Mock<HttpMessageHandler> MockHttpHandlerForDelete() =>
        MockHttpHandler(req => req.Method == HttpMethod.Delete);

    private Mock<HttpMessageHandler> MockHttpHandler(
        Func<HttpRequestMessage, bool> requestMatcher,
        HttpResponseMessage httpResponse = null,
        Mock<HttpMessageHandler> handlerMock = null)
    {
        handlerMock ??= new Mock<HttpMessageHandler>();
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
