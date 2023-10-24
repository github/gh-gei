using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Moq;
using Moq.Protected;
using OctoshiftCLI.Contracts;
using OctoshiftCLI.Extensions;
using OctoshiftCLI.Services;
using Xunit;

namespace OctoshiftCLI.Tests.Octoshift.Services;

public sealed class AdoClientTests : IDisposable
{
    private readonly Mock<OctoLogger> _mockOctoLogger = TestHelpers.CreateMock<OctoLogger>();
    private readonly HttpResponseMessage _httpResponse;
    private readonly RetryPolicy _retryPolicy;
    private readonly object _rawRequestBody;
    private const string EXPECTED_JSON_REQUEST_BODY = "{\"id\":\"ID\"}";
    private const string EXPECTED_RESPONSE_CONTENT = "RESPONSE_CONTENT";
    private const string PERSONAL_ACCESS_TOKEN = "PERSONAL_ACCESS_TOKEN";
    private const string URL = "http://example.com/resource";

    public AdoClientTests()
    {
        _rawRequestBody = new { id = "ID" };

        _httpResponse = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(EXPECTED_RESPONSE_CONTENT)
        };

        _retryPolicy = new RetryPolicy(_mockOctoLogger.Object)
        {
            _httpRetryInterval = 0,
            _retryInterval = 0
        };
    }

    [Fact]
    public void It_Adds_The_Authorization_Header()
    {
        // Arrange
        using var httpClient = new HttpClient(MockHttpHandlerForGet().Object);
        var expectedAuthToken = Convert.ToBase64String(Encoding.ASCII.GetBytes($":{PERSONAL_ACCESS_TOKEN}"));

        // Act
        _ = new AdoClient(_mockOctoLogger.Object, httpClient, null, _retryPolicy, PERSONAL_ACCESS_TOKEN);

        // Assert
        httpClient.DefaultRequestHeaders.Authorization.Should().NotBeNull();
        httpClient.DefaultRequestHeaders.Authorization.Parameter.Should().Be(expectedAuthToken);
        httpClient.DefaultRequestHeaders.Authorization.Scheme.Should().Be("Basic");
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
        var adoClient = new AdoClient(_mockOctoLogger.Object, httpClient, null, _retryPolicy, PERSONAL_ACCESS_TOKEN);

        // Act
        var returnedContent = await adoClient.GetAsync(URL);

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
        var adoClient = new AdoClient(_mockOctoLogger.Object, httpClient, null, _retryPolicy, PERSONAL_ACCESS_TOKEN);

        // Act
        // Assert
        await adoClient
            .Invoking(async x => await x.GetAsync(URL))
            .Should()
            .ThrowExactlyAsync<OctoshiftCliException>()
            .WithMessage("Unauthorized. Please check your token and try again");
    }

    [Fact]
    public async Task GetAsync_Applies_Retry_Delay()
    {
        // Arrange
        using var firstHttpResponse = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Headers = { RetryAfter = new RetryConditionHeaderValue(TimeSpan.FromSeconds(1)) },
            Content = new StringContent("FIRST_RESPONSE")
        };
        using var secondHttpResponse = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("SECOND_RESPONSE")
        };
        using var thirdResponse = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("THIRD_RESPONSE")
        };
        var handlerMock = new Mock<HttpMessageHandler>();
        handlerMock
            .Protected()
            .SetupSequence<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.Is<HttpRequestMessage>(req => req.Method == HttpMethod.Get),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(firstHttpResponse)
            .ReturnsAsync(secondHttpResponse)
            .ReturnsAsync(thirdResponse);

        using var httpClient = new HttpClient(handlerMock.Object);
        var adoClient = new AdoClient(_mockOctoLogger.Object, httpClient, null, _retryPolicy, PERSONAL_ACCESS_TOKEN);

        // Act
        await adoClient.GetAsync(URL); // normal call
        await adoClient.GetAsync(URL); // call with retry delay
        await adoClient.GetAsync(URL); // normal call

        // Assert
        _mockOctoLogger.Verify(m => m.LogWarning("THROTTLING IN EFFECT. Waiting 1000 ms"), Times.Once);
    }

    [Fact]
    public async Task GetAsync_Logs_The_Url()
    {
        // Arrange
        using var httpClient = new HttpClient(MockHttpHandlerForGet().Object);
        var adoClient = new AdoClient(_mockOctoLogger.Object, httpClient, null, _retryPolicy, PERSONAL_ACCESS_TOKEN);

        // Act
        await adoClient.GetAsync(URL);

        // Assert
        _mockOctoLogger.Verify(m => m.LogVerbose($"HTTP GET: {URL}"), Times.Once);
    }

    [Fact]
    public async Task GetAsync_Returns_String_Response()
    {
        // Arrange
        using var httpClient = new HttpClient(MockHttpHandlerForGet().Object);
        var adoClient = new AdoClient(_mockOctoLogger.Object, httpClient, null, _retryPolicy, PERSONAL_ACCESS_TOKEN);

        // Act
        var actualContent = await adoClient.GetAsync(URL);

        // Assert
        actualContent.Should().Be(EXPECTED_RESPONSE_CONTENT);
    }

    [Fact]
    public async Task GetAsync_Retries_On_503()
    {
        // Arrange
        var handlerMock = new Mock<HttpMessageHandler>();
        handlerMock
            .Protected()
            .SetupSequence<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.Is<HttpRequestMessage>(req => req.Method == HttpMethod.Get),
                ItExpr.IsAny<CancellationToken>())
            .Throws(new HttpRequestException(null, null, statusCode: HttpStatusCode.ServiceUnavailable))
            .Throws(new HttpRequestException(null, null, statusCode: HttpStatusCode.ServiceUnavailable))
            .ReturnsAsync(_httpResponse);

        using var httpClient = new HttpClient(handlerMock.Object);

        var adoClient = new AdoClient(_mockOctoLogger.Object, httpClient, null, _retryPolicy, PERSONAL_ACCESS_TOKEN);

        // Act
        var actualContent = await adoClient.GetAsync(URL);

        // Assert
        actualContent.Should().Be(EXPECTED_RESPONSE_CONTENT);
    }

    [Fact]
    public async Task GetAsync_Logs_The_Response_Status_Code_And_Content()
    {
        // Arrange
        using var httpClient = new HttpClient(MockHttpHandlerForGet().Object);
        var adoClient = new AdoClient(_mockOctoLogger.Object, httpClient, null, _retryPolicy, PERSONAL_ACCESS_TOKEN);

        // Act
        await adoClient.GetAsync(URL);

        // Assert
        _mockOctoLogger.Verify(m => m.LogVerbose($"RESPONSE ({_httpResponse.StatusCode}): {EXPECTED_RESPONSE_CONTENT}"), Times.Once);
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
        // Assert
        await FluentActions
            .Invoking(async () =>
            {
                using var httpClient = new HttpClient(handlerMock.Object);
                var adoClient = new AdoClient(_mockOctoLogger.Object, httpClient, null, _retryPolicy, PERSONAL_ACCESS_TOKEN);
                return await adoClient.GetAsync(URL);
            })
            .Should()
            .ThrowExactlyAsync<HttpRequestException>();
    }

    [Fact]
    public async Task PostAsync_Applies_Retry_Delay()
    {
        // Arrange
        using var firstHttpResponse = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Headers = { RetryAfter = new RetryConditionHeaderValue(TimeSpan.FromSeconds(1)) },
            Content = new StringContent("FIRST_RESPONSE")
        };
        using var secondHttpResponse = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("SECOND_RESPONSE")
        };
        using var thirdResponse = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("THIRD_RESPONSE")
        };
        var handlerMock = new Mock<HttpMessageHandler>();
        handlerMock
            .Protected()
            .SetupSequence<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.Is<HttpRequestMessage>(req =>
                    req.Method == HttpMethod.Post &&
                    req.Content.ReadAsStringAsync().Result == EXPECTED_JSON_REQUEST_BODY),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(firstHttpResponse)
            .ReturnsAsync(secondHttpResponse)
            .ReturnsAsync(thirdResponse);

        using var httpClient = new HttpClient(handlerMock.Object);
        var adoClient = new AdoClient(_mockOctoLogger.Object, httpClient, null, _retryPolicy, PERSONAL_ACCESS_TOKEN);

        // Act
        await adoClient.PostAsync(URL, _rawRequestBody); // normal call
        await adoClient.PostAsync(URL, _rawRequestBody); // call with retry delay
        await adoClient.PostAsync(URL, _rawRequestBody); // normal call

        // Assert
        _mockOctoLogger.Verify(m => m.LogWarning("THROTTLING IN EFFECT. Waiting 1000 ms"), Times.Once);
    }

    [Fact]
    public async Task PostAsync_Logs_The_Url()
    {
        // Arrange
        using var httpClient = new HttpClient(MockHttpHandlerForPost().Object);
        var adoClient = new AdoClient(_mockOctoLogger.Object, httpClient, null, _retryPolicy, PERSONAL_ACCESS_TOKEN);

        // Act
        await adoClient.PostAsync(URL, _rawRequestBody);

        // Assert
        _mockOctoLogger.Verify(m => m.LogVerbose($"HTTP POST: {URL}"), Times.Once);
    }

    [Fact]
    public async Task PostAsync_Logs_The_Request_Body()
    {
        // Arrange
        using var httpClient = new HttpClient(MockHttpHandlerForPost().Object);
        var adoClient = new AdoClient(_mockOctoLogger.Object, httpClient, null, _retryPolicy, PERSONAL_ACCESS_TOKEN);

        // Act
        await adoClient.PostAsync(URL, _rawRequestBody);

        // Assert
        _mockOctoLogger.Verify(m => m.LogVerbose($"HTTP BODY: {EXPECTED_JSON_REQUEST_BODY}"), Times.Once);
    }

    [Fact]
    public async Task PostAsync_Returns_String_Response()
    {
        // Arrange
        using var httpClient = new HttpClient(MockHttpHandlerForPost().Object);
        var adoClient = new AdoClient(_mockOctoLogger.Object, httpClient, null, _retryPolicy, PERSONAL_ACCESS_TOKEN);

        // Act
        var actualContent = await adoClient.PostAsync(URL, _rawRequestBody);

        // Assert
        actualContent.Should().Be(EXPECTED_RESPONSE_CONTENT);
    }

    [Fact]
    public async Task PostAsync_Logs_The_Response_Status_Code_And_Content()
    {
        // Arrange
        using var httpClient = new HttpClient(MockHttpHandlerForPost().Object);
        var adoClient = new AdoClient(_mockOctoLogger.Object, httpClient, null, _retryPolicy, PERSONAL_ACCESS_TOKEN);

        // Act
        await adoClient.PostAsync(URL, _rawRequestBody);

        // Assert
        _mockOctoLogger.Verify(m => m.LogVerbose($"RESPONSE ({_httpResponse.StatusCode}): {EXPECTED_RESPONSE_CONTENT}"), Times.Once);
    }

    [Fact]
    public async Task PostAsync_Throws_HttpRequestException_On_Non_Success_Response()
    {
        // Arrange
        using var httpResponse = new HttpResponseMessage(HttpStatusCode.InternalServerError);
        var handlerMock = MockHttpHandler(_ => true, httpResponse);

        // Act
        // Assert
        await FluentActions
            .Invoking(() =>
            {
                using var httpClient = new HttpClient(handlerMock.Object);
                var adoClient = new AdoClient(_mockOctoLogger.Object, httpClient, null, _retryPolicy, PERSONAL_ACCESS_TOKEN);
                return adoClient.PostAsync(URL, _rawRequestBody);
            })
            .Should()
            .ThrowExactlyAsync<HttpRequestException>();
    }

    [Fact]
    public async Task PutAsync_Applies_Retry_Delay()
    {
        // Arrange
        using var firstHttpResponse = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Headers = { RetryAfter = new RetryConditionHeaderValue(TimeSpan.FromSeconds(1)) },
            Content = new StringContent("FIRST_RESPONSE")
        };
        using var secondHttpResponse = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("SECOND_RESPONSE")
        };
        using var thirdResponse = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("THIRD_RESPONSE")
        };
        var handlerMock = new Mock<HttpMessageHandler>();
        handlerMock
            .Protected()
            .SetupSequence<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.Is<HttpRequestMessage>(req =>
                    req.Method == HttpMethod.Put &&
                    req.Content.ReadAsStringAsync().Result == EXPECTED_JSON_REQUEST_BODY),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(firstHttpResponse)
            .ReturnsAsync(secondHttpResponse)
            .ReturnsAsync(thirdResponse);

        using var httpClient = new HttpClient(handlerMock.Object);
        var adoClient = new AdoClient(_mockOctoLogger.Object, httpClient, null, _retryPolicy, PERSONAL_ACCESS_TOKEN);

        // Act
        await adoClient.PutAsync(URL, _rawRequestBody); // normal call
        await adoClient.PutAsync(URL, _rawRequestBody); // call with retry delay
        await adoClient.PutAsync(URL, _rawRequestBody); // normal call

        // Assert
        _mockOctoLogger.Verify(m => m.LogWarning("THROTTLING IN EFFECT. Waiting 1000 ms"), Times.Once);
    }

    [Fact]
    public async Task PutAsync_Logs_The_Url()
    {
        // Arrange
        using var httpClient = new HttpClient(MockHttpHandlerForPut().Object);
        var adoClient = new AdoClient(_mockOctoLogger.Object, httpClient, null, _retryPolicy, PERSONAL_ACCESS_TOKEN);

        // Act
        await adoClient.PutAsync(URL, _rawRequestBody);

        // Assert
        _mockOctoLogger.Verify(m => m.LogVerbose($"HTTP PUT: {URL}"), Times.Once);
    }

    [Fact]
    public async Task PutAsync_Logs_The_Request_Body()
    {
        // Arrange
        using var httpClient = new HttpClient(MockHttpHandlerForPut().Object);
        var adoClient = new AdoClient(_mockOctoLogger.Object, httpClient, null, _retryPolicy, PERSONAL_ACCESS_TOKEN);

        // Act
        await adoClient.PutAsync(URL, _rawRequestBody);

        // Assert
        _mockOctoLogger.Verify(m => m.LogVerbose($"HTTP BODY: {EXPECTED_JSON_REQUEST_BODY}"), Times.Once);
    }

    [Fact]
    public async Task PutAsync_Returns_String_Response()
    {
        // Arrange
        using var httpClient = new HttpClient(MockHttpHandlerForPut().Object);
        var adoClient = new AdoClient(_mockOctoLogger.Object, httpClient, null, _retryPolicy, PERSONAL_ACCESS_TOKEN);

        // Act
        var actualContent = await adoClient.PutAsync(URL, _rawRequestBody);

        // Assert
        actualContent.Should().Be(EXPECTED_RESPONSE_CONTENT);
    }

    [Fact]
    public async Task PutAsync_Logs_The_Response_Status_Code_And_Content()
    {
        // Arrange
        using var httpClient = new HttpClient(MockHttpHandlerForPut().Object);
        var adoClient = new AdoClient(_mockOctoLogger.Object, httpClient, null, _retryPolicy, PERSONAL_ACCESS_TOKEN);

        // Act
        await adoClient.PutAsync(URL, _rawRequestBody);

        // Assert
        _mockOctoLogger.Verify(m => m.LogVerbose($"RESPONSE ({_httpResponse.StatusCode}): {EXPECTED_RESPONSE_CONTENT}"), Times.Once);
    }

    [Fact]
    public async Task PutAsync_Throws_HttpRequestException_On_Non_Success_Response()
    {
        // Arrange
        using var httpResponse = new HttpResponseMessage(HttpStatusCode.InternalServerError);
        var handlerMock = MockHttpHandler(_ => true, httpResponse);

        // Act
        // Assert
        await FluentActions
            .Invoking(() =>
            {
                using var httpClient = new HttpClient(handlerMock.Object);
                var adoClient = new AdoClient(_mockOctoLogger.Object, httpClient, null, _retryPolicy, PERSONAL_ACCESS_TOKEN);
                return adoClient.PutAsync(URL, _rawRequestBody);
            })
            .Should()
            .ThrowExactlyAsync<HttpRequestException>();
    }

    [Fact]
    public async Task PatchAsync_Applies_Retry_Delay()
    {
        // Arrange
        using var firstHttpResponse = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Headers = { RetryAfter = new RetryConditionHeaderValue(TimeSpan.FromSeconds(1)) },
            Content = new StringContent("FIRST_RESPONSE")
        };
        using var secondHttpResponse = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("SECOND_RESPONSE")
        };
        using var thirdResponse = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("THIRD_RESPONSE")
        };
        var handlerMock = new Mock<HttpMessageHandler>();
        handlerMock
            .Protected()
            .SetupSequence<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.Is<HttpRequestMessage>(req =>
                    req.Method == HttpMethod.Patch &&
                    req.Content.ReadAsStringAsync().Result == EXPECTED_JSON_REQUEST_BODY),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(firstHttpResponse)
            .ReturnsAsync(secondHttpResponse)
            .ReturnsAsync(thirdResponse);

        using var httpClient = new HttpClient(handlerMock.Object);
        var adoClient = new AdoClient(_mockOctoLogger.Object, httpClient, null, _retryPolicy, PERSONAL_ACCESS_TOKEN);

        // Act
        await adoClient.PatchAsync(URL, _rawRequestBody); // normal call
        await adoClient.PatchAsync(URL, _rawRequestBody); // call with retry delay
        await adoClient.PatchAsync(URL, _rawRequestBody); // normal call

        // Assert
        _mockOctoLogger.Verify(m => m.LogWarning("THROTTLING IN EFFECT. Waiting 1000 ms"), Times.Once);
    }

    [Fact]
    public async Task PatchAsync_Logs_The_Url()
    {
        // Arrange
        using var httpClient = new HttpClient(MockHttpHandlerForPatch().Object);
        var adoClient = new AdoClient(_mockOctoLogger.Object, httpClient, null, _retryPolicy, PERSONAL_ACCESS_TOKEN);

        // Act
        await adoClient.PatchAsync(URL, _rawRequestBody);

        // Assert
        _mockOctoLogger.Verify(m => m.LogVerbose($"HTTP PATCH: {URL}"), Times.Once);
    }

    [Fact]
    public async Task PatchAsync_Logs_The_Request_Body()
    {
        // Arrange
        using var httpClient = new HttpClient(MockHttpHandlerForPatch().Object);
        var adoClient = new AdoClient(_mockOctoLogger.Object, httpClient, null, _retryPolicy, PERSONAL_ACCESS_TOKEN);

        // Act
        await adoClient.PatchAsync(URL, _rawRequestBody);

        // Assert
        _mockOctoLogger.Verify(m => m.LogVerbose($"HTTP BODY: {EXPECTED_JSON_REQUEST_BODY}"), Times.Once);
    }

    [Fact]
    public async Task PatchAsync_Returns_String_Response()
    {
        // Arrange
        using var httpClient = new HttpClient(MockHttpHandlerForPatch().Object);
        var adoClient = new AdoClient(_mockOctoLogger.Object, httpClient, null, _retryPolicy, PERSONAL_ACCESS_TOKEN);

        // Act
        var actualContent = await adoClient.PatchAsync(URL, _rawRequestBody);

        // Assert
        actualContent.Should().Be(EXPECTED_RESPONSE_CONTENT);
    }

    [Fact]
    public async Task PatchAsync_Logs_The_Response_Status_Code_And_Content()
    {
        // Arrange
        using var httpClient = new HttpClient(MockHttpHandlerForPatch().Object);
        var adoClient = new AdoClient(_mockOctoLogger.Object, httpClient, null, _retryPolicy, PERSONAL_ACCESS_TOKEN);

        // Act
        await adoClient.PatchAsync(URL, _rawRequestBody);

        // Assert
        _mockOctoLogger.Verify(m => m.LogVerbose($"RESPONSE ({_httpResponse.StatusCode}): {EXPECTED_RESPONSE_CONTENT}"), Times.Once);
    }

    [Fact]
    public async Task PatchAsync_Throws_HttpRequestException_On_Non_Success_Response()
    {
        // Arrange
        using var httpResponse = new HttpResponseMessage(HttpStatusCode.InternalServerError);
        var handlerMock = MockHttpHandler(_ => true, httpResponse);

        // Act
        // Assert
        await FluentActions
            .Invoking(() =>
            {
                using var httpClient = new HttpClient(handlerMock.Object);
                var adoClient = new AdoClient(_mockOctoLogger.Object, httpClient, null, _retryPolicy, PERSONAL_ACCESS_TOKEN);
                return adoClient.PatchAsync(URL, _rawRequestBody);
            })
            .Should()
            .ThrowExactlyAsync<HttpRequestException>();
    }

    [Fact]
    public async Task DeleteAsync_Applies_Retry_Delay()
    {
        // Arrange
        using var firstHttpResponse = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Headers = { RetryAfter = new RetryConditionHeaderValue(TimeSpan.FromSeconds(1)) },
            Content = new StringContent("FIRST_RESPONSE")
        };
        using var secondHttpResponse = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("SECOND_RESPONSE")
        };
        using var thirdResponse = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("THIRD_RESPONSE")
        };
        var handlerMock = new Mock<HttpMessageHandler>();
        handlerMock
            .Protected()
            .SetupSequence<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.Is<HttpRequestMessage>(req => req.Method == HttpMethod.Delete),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(firstHttpResponse)
            .ReturnsAsync(secondHttpResponse)
            .ReturnsAsync(thirdResponse);

        using var httpClient = new HttpClient(handlerMock.Object);
        var adoClient = new AdoClient(_mockOctoLogger.Object, httpClient, null, _retryPolicy, PERSONAL_ACCESS_TOKEN);

        // Act
        await adoClient.DeleteAsync(URL); // normal call
        await adoClient.DeleteAsync(URL); // call with retry delay
        await adoClient.DeleteAsync(URL); // normal call

        // Assert
        _mockOctoLogger.Verify(m => m.LogWarning("THROTTLING IN EFFECT. Waiting 1000 ms"), Times.Once);
    }

    [Fact]
    public async Task DeleteAsync_Logs_The_Url()
    {
        // Arrange
        using var httpClient = new HttpClient(MockHttpHandlerForDelete().Object);
        var adoClient = new AdoClient(_mockOctoLogger.Object, httpClient, null, _retryPolicy, PERSONAL_ACCESS_TOKEN);

        // Act
        await adoClient.DeleteAsync(URL);

        // Assert
        _mockOctoLogger.Verify(m => m.LogVerbose($"HTTP DELETE: {URL}"), Times.Once);
    }

    [Fact]
    public async Task DeleteAsync_Returns_String_Response()
    {
        // Arrange
        using var httpClient = new HttpClient(MockHttpHandlerForDelete().Object);
        var adoClient = new AdoClient(_mockOctoLogger.Object, httpClient, null, _retryPolicy, PERSONAL_ACCESS_TOKEN);

        // Act
        var actualContent = await adoClient.DeleteAsync(URL);

        // Assert
        actualContent.Should().Be(EXPECTED_RESPONSE_CONTENT);
    }

    [Fact]
    public async Task DeleteAsync_Logs_The_Response_Status_Code_And_Content()
    {
        // Arrange
        using var httpClient = new HttpClient(MockHttpHandlerForDelete().Object);
        var adoClient = new AdoClient(_mockOctoLogger.Object, httpClient, null, _retryPolicy, PERSONAL_ACCESS_TOKEN);

        // Act
        await adoClient.DeleteAsync(URL);

        // Assert
        _mockOctoLogger.Verify(m => m.LogVerbose($"RESPONSE ({_httpResponse.StatusCode}): {EXPECTED_RESPONSE_CONTENT}"), Times.Once);
    }

    [Fact]
    public async Task DeleteAsync_Throws_HttpRequestException_On_Non_Success_Response()
    {
        // Arrange
        using var httpResponse = new HttpResponseMessage(HttpStatusCode.InternalServerError);
        var handlerMock = MockHttpHandler(_ => true, httpResponse);

        // Act
        // Assert
        await FluentActions
            .Invoking(() =>
            {
                using var httpClient = new HttpClient(handlerMock.Object);
                var adoClient = new AdoClient(_mockOctoLogger.Object, httpClient, null, _retryPolicy, PERSONAL_ACCESS_TOKEN);
                return adoClient.DeleteAsync(URL);
            })
            .Should()
            .ThrowExactlyAsync<HttpRequestException>();
    }

    [Fact]
    public async Task GetWithPagingAsync_Throws_If_Url_Is_Null()
    {
        await FluentActions
            .Invoking(() => // Arrange, Act
            {
                var adoClient = new AdoClient(null, null, null, null, null);
                return adoClient.GetWithPagingAsync(null, "CONTINUATION_TOKEN");
            })
            .Should()
            .ThrowExactlyAsync<ArgumentNullException>() // Assert
            .WithParameterName("url");
    }

    [Fact]
    public async Task GetWithPagingAsync_Throws_If_Url_Is_Empty()
    {
        await FluentActions
            .Invoking(() => // Arrange, Act
            {
                var adoClient = new AdoClient(null, null, null, null, null);
                return adoClient.GetWithPagingAsync("", "CONTINUATION_TOKEN");
            })
            .Should()
            .ThrowExactlyAsync<ArgumentNullException>() // Assert
            .WithParameterName("url");
    }

    [Fact]
    public async Task GetWithPagingAsync_Throws_If_Url_Is_WhiteSpace()
    {
        await FluentActions
            .Invoking(() => // Arrange, Act
            {
                var adoClient = new AdoClient(null, null, null, null, null);
                return adoClient.GetWithPagingAsync("  ", "CONTINUATION_TOKEN");
            })
            .Should()
            .ThrowExactlyAsync<ArgumentNullException>() // Assert
            .WithParameterName("url");
    }

    [Fact]
    public async Task GetWithPagingAsync_Adds_The_Continuation_Token_As_Single_Query_Parameter()
    {
        // Arrange
        using var httpResponse = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(new { value = new[] { "item1", "item2", "item3" } }.ToJson())
        };
        var handlerMock = MockHttpHandler(req => req.Method == HttpMethod.Get, httpResponse);
        using var httpClient = new HttpClient(handlerMock.Object);
        var adoClient = new AdoClient(_mockOctoLogger.Object, httpClient, null, _retryPolicy, PERSONAL_ACCESS_TOKEN);

        const string continuationToken = "CONTINUATION_TOKEN";

        // Act
        await adoClient.GetWithPagingAsync(URL, continuationToken);

        // Assert
        handlerMock.Protected().Verify(
            "SendAsync",
            Times.Once(),
            ItExpr.Is<HttpRequestMessage>(msg => msg.RequestUri.AbsoluteUri == $"{URL}?continuationToken={continuationToken}"),
            ItExpr.IsAny<CancellationToken>());
    }

    [Fact]
    public async Task GetWithPagingAsync_Appends_The_Continuation_Token_To_Existing_Query_Parameters()
    {
        // Arrange
        using var httpResponse = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(new { value = new[] { "item1", "item2", "item3" } }.ToJson())
        };
        var handlerMock = MockHttpHandler(req => req.Method == HttpMethod.Get, httpResponse);
        using var httpClient = new HttpClient(handlerMock.Object);
        var adoClient = new AdoClient(_mockOctoLogger.Object, httpClient, null, _retryPolicy, PERSONAL_ACCESS_TOKEN);

        const string url = "http://example.com/resource?existing=param";
        const string continuationToken = "CONTINUATION_TOKEN";

        // Act
        await adoClient.GetWithPagingAsync(url, continuationToken);

        // Assert
        handlerMock.Protected().Verify(
            "SendAsync",
            Times.Once(),
            ItExpr.Is<HttpRequestMessage>(msg => msg.RequestUri.AbsoluteUri == $"{url}&continuationToken={continuationToken}"),
            ItExpr.IsAny<CancellationToken>());
    }

    [Fact]
    public async Task GetWithPagingAsync_Applies_Retry_Delay()
    {
        // Arrange
        using var firstHttpResponse = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Headers = { RetryAfter = new RetryConditionHeaderValue(TimeSpan.FromSeconds(1)) },
            Content = new StringContent(new { value = new[] { "item1", "item2", "item3" } }.ToJson())
        };
        using var secondHttpResponse = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(new { value = new[] { "item4", "item5", "item6" } }.ToJson())
        };
        using var thirdResponse = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(new { value = new[] { "item7", "item8", "item9" } }.ToJson())
        };
        var handlerMock = new Mock<HttpMessageHandler>();
        handlerMock
            .Protected()
            .SetupSequence<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.Is<HttpRequestMessage>(req => req.Method == HttpMethod.Get),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(firstHttpResponse)
            .ReturnsAsync(secondHttpResponse)
            .ReturnsAsync(thirdResponse);

        using var httpClient = new HttpClient(handlerMock.Object);
        var adoClient = new AdoClient(_mockOctoLogger.Object, httpClient, null, _retryPolicy, PERSONAL_ACCESS_TOKEN);

        // Act
        await adoClient.GetWithPagingAsync(URL, null); // normal call
        await adoClient.GetWithPagingAsync(URL, null); // call with retry delay
        await adoClient.GetWithPagingAsync(URL, null); // normal call

        // Assert
        _mockOctoLogger.Verify(m => m.LogWarning("THROTTLING IN EFFECT. Waiting 1000 ms"), Times.Once);
    }

    [Fact]
    public async Task GetWithPagingAsync_Logs_The_Url()
    {
        // Arrange
        using var httpResponse = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(new { value = new[] { "item1", "item2", "item3" } }.ToJson())
        };
        var handlerMock = MockHttpHandler(req => req.Method == HttpMethod.Get, httpResponse);
        using var httpClient = new HttpClient(handlerMock.Object);
        var adoClient = new AdoClient(_mockOctoLogger.Object, httpClient, null, _retryPolicy, PERSONAL_ACCESS_TOKEN);

        // Act
        await adoClient.GetWithPagingAsync(URL);

        // Assert
        _mockOctoLogger.Verify(m => m.LogVerbose($"HTTP GET: {URL}"), Times.Once);
    }

    [Fact]
    public async Task GetWithPagingAsync_Logs_The_Response_Status_Code_And_Content()
    {
        // Arrange
        var content = new { value = new[] { "item1", "item2", "item3" } }.ToJson();
        using var httpResponse = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(content)
        };
        var handlerMock = MockHttpHandler(req => req.Method == HttpMethod.Get, httpResponse);
        using var httpClient = new HttpClient(handlerMock.Object);
        var adoClient = new AdoClient(_mockOctoLogger.Object, httpClient, null, _retryPolicy, PERSONAL_ACCESS_TOKEN);

        // Act
        await adoClient.GetWithPagingAsync(URL);

        // Assert
        _mockOctoLogger.Verify(m => m.LogVerbose($"RESPONSE ({_httpResponse.StatusCode}): {content}"), Times.Once);
    }

    [Fact]
    public async Task GetWithPagingAsync_HttpRequestException_On_Non_Success_Response()
    {
        // Arrange
        using var httpResponse = new HttpResponseMessage(HttpStatusCode.InternalServerError);
        var handlerMock = MockHttpHandler(_ => true, httpResponse);

        // Act
        // Assert
        await FluentActions
            .Invoking(() =>
            {
                using var httpClient = new HttpClient(handlerMock.Object);
                var adoClient = new AdoClient(_mockOctoLogger.Object, httpClient, null, _retryPolicy, PERSONAL_ACCESS_TOKEN);
                return adoClient.GetWithPagingAsync(URL);
            })
            .Should()
            .ThrowExactlyAsync<HttpRequestException>();
    }

    [Fact]
    public async Task GetWithPagingAsync_Gets_All_Pages()
    {
        // Arrange
        var continuationToken = Guid.NewGuid().ToString();
        var firstResult = new[] { "item1", "item2", "item3" };
        using var firstHttpResponse = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(new { value = firstResult }.ToJson())
        };
        firstHttpResponse.Headers.Add("x-ms-continuationtoken", continuationToken);

        var secondResult = new[] { "item4", "item5" };
        using var secondHttpResponse = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(new { value = secondResult }.ToJson())
        };

        var handlerMock = new Mock<HttpMessageHandler>();
        handlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.Is<HttpRequestMessage>(req =>
                    req.Method == HttpMethod.Get &&
                    req.RequestUri.AbsoluteUri == URL),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(firstHttpResponse);
        handlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.Is<HttpRequestMessage>(req =>
                    req.Method == HttpMethod.Get &&
                    req.RequestUri.AbsoluteUri == $"{URL}?continuationToken={continuationToken}"),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(secondHttpResponse);

        using var httpClient = new HttpClient(handlerMock.Object);
        var adoClient = new AdoClient(_mockOctoLogger.Object, httpClient, null, _retryPolicy, PERSONAL_ACCESS_TOKEN);

        // Act
        var expectedResult = await adoClient.GetWithPagingAsync(URL);

        // Assert
        expectedResult.Should().HaveCount(5);
        expectedResult.Select(x => (string)x).Should().Equal(firstResult.Concat(secondResult));

        handlerMock.Protected().Verify(
            "SendAsync",
            Times.Exactly(2),
            ItExpr.IsAny<HttpRequestMessage>(),
            ItExpr.IsAny<CancellationToken>());
    }

    [Fact]
    public async Task GetWithPagingTopSkipAsync_Throws_If_Url_Is_Null()
    {
        await FluentActions
            .Invoking(() => // Arrange, Act
            {
                var adoClient = new AdoClient(null, null, null, null, null);
                return adoClient.GetWithPagingTopSkipAsync(null, x => x);
            })
            .Should()
            .ThrowExactlyAsync<ArgumentNullException>() // Assert
            .WithParameterName("url");
    }

    [Fact]
    public async Task GetWithPagingTopSkipAsync_Throws_If_Url_Is_Empty()
    {
        await FluentActions
            .Invoking(() => // Arrange, Act
            {
                var adoClient = new AdoClient(null, null, null, null, null);
                return adoClient.GetWithPagingTopSkipAsync("", x => x);
            })
            .Should()
            .ThrowExactlyAsync<ArgumentNullException>() // Assert
            .WithParameterName("url");
    }

    [Fact]
    public async Task GetWithPagingTopSkipAsync_Throws_If_Url_Is_WhiteSpace()
    {
        await FluentActions
            .Invoking(() => // Arrange, Act
            {
                var adoClient = new AdoClient(null, null, null, null, null);
                return adoClient.GetWithPagingTopSkipAsync("  ", x => x);
            })
            .Should()
            .ThrowExactlyAsync<ArgumentNullException>() // Assert
            .WithParameterName("url");
    }

    [Fact]
    public async Task GetWithPagingTopSkipAsync_Adds_Top_And_Skip_As_Query_Parameters()
    {
        // Arrange
        using var httpResponse = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("{ count: 0, value: [] }")
        };
        var handlerMock = MockHttpHandler(req => req.Method == HttpMethod.Get, httpResponse);
        using var httpClient = new HttpClient(handlerMock.Object);
        var adoClient = new AdoClient(_mockOctoLogger.Object, httpClient, null, _retryPolicy, PERSONAL_ACCESS_TOKEN);

        // Act
        await adoClient.GetWithPagingTopSkipAsync(URL, x => (string)x);

        // Assert
        handlerMock.Protected().Verify(
            "SendAsync",
            Times.Once(),
            ItExpr.Is<HttpRequestMessage>(msg => msg.RequestUri.AbsoluteUri == $"{URL}?$skip=0&$top=1000"),
            ItExpr.IsAny<CancellationToken>());
    }

    [Fact]
    public async Task GetWithPagingTopSkipAsync_Appends_Top_And_Skip_To_Existing_Query_Parameters()
    {
        // Arrange
        using var httpResponse = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("{ count: 0, value: [] }")
        };
        var handlerMock = MockHttpHandler(req => req.Method == HttpMethod.Get, httpResponse);
        using var httpClient = new HttpClient(handlerMock.Object);
        var adoClient = new AdoClient(_mockOctoLogger.Object, httpClient, null, _retryPolicy, PERSONAL_ACCESS_TOKEN);

        const string url = "http://example.com/resource?existing=param";

        // Act
        await adoClient.GetWithPagingTopSkipAsync(url, x => x);

        // Assert
        handlerMock.Protected().Verify(
            "SendAsync",
            Times.Once(),
            ItExpr.Is<HttpRequestMessage>(msg => msg.RequestUri.AbsoluteUri == $"{url}&$skip=0&$top=1000"),
            ItExpr.IsAny<CancellationToken>());
    }

    [Fact]
    public async Task GetWithPagingTopSkipAsync_Gets_All_Pages()
    {
        // Arrange
        var firstResult = new[] { "item1", "item2", "item3" };
        using var firstHttpResponse = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(new { value = firstResult }.ToJson())
        };

        var secondResult = new[] { "item4", "item5" };
        using var secondHttpResponse = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(new { value = secondResult }.ToJson())
        };

        using var thirdHttpResponse = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("{ value: [] }")
        };

        var handlerMock = new Mock<HttpMessageHandler>();
        handlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.Is<HttpRequestMessage>(req =>
                    req.Method == HttpMethod.Get &&
                    req.RequestUri.AbsoluteUri == $"{URL}?$skip=0&$top=1000"),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(firstHttpResponse);
        handlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.Is<HttpRequestMessage>(req =>
                    req.Method == HttpMethod.Get &&
                    req.RequestUri.AbsoluteUri == $"{URL}?$skip=1000&$top=1000"),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(secondHttpResponse);
        handlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.Is<HttpRequestMessage>(req =>
                    req.Method == HttpMethod.Get &&
                    req.RequestUri.AbsoluteUri == $"{URL}?$skip=2000&$top=1000"),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(thirdHttpResponse);

        using var httpClient = new HttpClient(handlerMock.Object);
        var adoClient = new AdoClient(_mockOctoLogger.Object, httpClient, null, _retryPolicy, PERSONAL_ACCESS_TOKEN);

        // Act
        var expectedResult = await adoClient.GetWithPagingTopSkipAsync(URL, x => x);

        // Assert
        expectedResult.Should().HaveCount(5);
        expectedResult.Select(x => (string)x).Should().Equal(firstResult.Concat(secondResult));

        handlerMock.Protected().Verify(
            "SendAsync",
            Times.Exactly(3),
            ItExpr.IsAny<HttpRequestMessage>(),
            ItExpr.IsAny<CancellationToken>());
    }

    [Fact]
    public async Task GetWithPagingTopSkipAsync_Applies_Selector()
    {
        // Arrange
        var firstResponse = new[] { "item1", "item2", "item3" };
        using var firstHttpResponse = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(new { value = firstResponse }.ToJson())
        };

        using var secondHttpResponse = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("{ value: [] }")
        };

        var handlerMock = new Mock<HttpMessageHandler>();
        handlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.Is<HttpRequestMessage>(req =>
                    req.Method == HttpMethod.Get &&
                    req.RequestUri.AbsoluteUri == $"{URL}?$skip=0&$top=1000"),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(firstHttpResponse);
        handlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.Is<HttpRequestMessage>(req =>
                    req.Method == HttpMethod.Get &&
                    req.RequestUri.AbsoluteUri == $"{URL}?$skip=1000&$top=1000"),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(secondHttpResponse);

        using var httpClient = new HttpClient(handlerMock.Object);
        var adoClient = new AdoClient(_mockOctoLogger.Object, httpClient, null, _retryPolicy, PERSONAL_ACCESS_TOKEN);

        // Act
        var result = await adoClient.GetWithPagingTopSkipAsync(URL, x => (string)x + "foo");

        // Assert
        var expectedResult = firstResponse.Select(x => x + "foo");
        result.Should().BeEquivalentTo(expectedResult);
    }

    [Fact]
    public void It_Sets_User_Agent_Header_With_Comments()
    {
        // Arrange
        const string currentVersion = "1.1.1";
        const string versionComments = "(COMMENTS)";

        using var httpClient = new HttpClient();

        var mockVersionProvider = new Mock<IVersionProvider>();
        mockVersionProvider.Setup(m => m.GetCurrentVersion()).Returns("1.1.1");
        mockVersionProvider.Setup(m => m.GetVersionComments()).Returns(versionComments);

        // Act
        _ = new AdoClient(null, httpClient, mockVersionProvider.Object, _retryPolicy, PERSONAL_ACCESS_TOKEN);

        // Assert
        httpClient.DefaultRequestHeaders.UserAgent.Should().HaveCount(2);
        httpClient.DefaultRequestHeaders.UserAgent.ToString().Should().Be($"OctoshiftCLI/{currentVersion} {versionComments}");
    }

    [Fact]
    public void It_Only_Sets_The_Product_Name_In_User_Agent_Header_When_Version_Provider_Is_Null()
    {
        // Arrange
        using var httpClient = new HttpClient();

        // Act
        _ = new AdoClient(null, httpClient, null, _retryPolicy, PERSONAL_ACCESS_TOKEN);

        // Assert
        httpClient.DefaultRequestHeaders.UserAgent.Should().HaveCount(1);
        httpClient.DefaultRequestHeaders.UserAgent.ToString().Should().Be("OctoshiftCLI");
    }

    private Mock<HttpMessageHandler> MockHttpHandlerForGet() =>
        MockHttpHandler(req => req.Method == HttpMethod.Get);

    private Mock<HttpMessageHandler> MockHttpHandlerForDelete() =>
        MockHttpHandler(req => req.Method == HttpMethod.Delete);

    private Mock<HttpMessageHandler> MockHttpHandlerForPost() =>
        MockHttpHandler(req =>
            req.Content.ReadAsStringAsync().Result == EXPECTED_JSON_REQUEST_BODY
            && req.Method == HttpMethod.Post);

    private Mock<HttpMessageHandler> MockHttpHandlerForPut() =>
        MockHttpHandler(req =>
            req.Content.ReadAsStringAsync().Result == EXPECTED_JSON_REQUEST_BODY
            && req.Method == HttpMethod.Put);

    private Mock<HttpMessageHandler> MockHttpHandlerForPatch() =>
        MockHttpHandler(req =>
            req.Content.ReadAsStringAsync().Result == EXPECTED_JSON_REQUEST_BODY
            && req.Method == HttpMethod.Patch);

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
