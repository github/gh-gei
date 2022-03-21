using System;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Moq;
using Moq.Protected;
using Xunit;

namespace OctoshiftCLI.Tests
{
    public sealed class AdoClientTests : IDisposable
    {
        private readonly Mock<OctoLogger> _loggerMock;
        private readonly HttpResponseMessage _httpResponse;
        private readonly object _rawRequestBody;
        private const string EXPECTED_JSON_REQUEST_BODY = "{\"id\":\"ID\"}";
        private const string EXPECTED_RESPONSE_CONTENT = "RESPONSE_CONTENT";
        private const string PERSONAL_ACCESS_TOKEN = "PERSONAL_ACCESS_TOKEN";

        public AdoClientTests()
        {
            _rawRequestBody = new { id = "ID" };

            _httpResponse = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(EXPECTED_RESPONSE_CONTENT)
            };

            _loggerMock = new Mock<OctoLogger>();
        }

        [Fact]
        public void It_Adds_The_Authorization_Header()
        {
            // Arrange
            using var httpClient = new HttpClient(MockHttpHandlerForGet().Object);
            var expectedAuthToken = Convert.ToBase64String(Encoding.ASCII.GetBytes($":{PERSONAL_ACCESS_TOKEN}"));

            // Act
            _ = new AdoClient(_loggerMock.Object, httpClient, PERSONAL_ACCESS_TOKEN);

            // Assert
            httpClient.DefaultRequestHeaders.Authorization.Should().NotBeNull();
            httpClient.DefaultRequestHeaders.Authorization.Parameter.Should().Be(expectedAuthToken);
            httpClient.DefaultRequestHeaders.Authorization.Scheme.Should().Be("Basic");
        }

        [Fact]
        public async Task GetAsync_Encodes_The_Url()
        {
            // Arrange
            var handlerMock = MockHttpHandlerForGet();
            using var httpClient = new HttpClient(handlerMock.Object);
            var adoClient = new AdoClient(_loggerMock.Object, httpClient, PERSONAL_ACCESS_TOKEN);

            const string actualUrl = "http://example.com/param with space";
            const string expectedUrl = "http://example.com/param%20with%20space";

            // Act
            await adoClient.GetAsync(actualUrl);

            // Assert
            handlerMock.Protected().Verify(
                "SendAsync",
                Times.Once(),
                ItExpr.Is<HttpRequestMessage>(msg => msg.RequestUri.AbsoluteUri == expectedUrl),
                ItExpr.IsAny<CancellationToken>());
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
            using var thridResponse = new HttpResponseMessage(HttpStatusCode.OK)
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
                .ReturnsAsync(thridResponse);

            using var httpClient = new HttpClient(handlerMock.Object);
            var adoClient = new AdoClient(_loggerMock.Object, httpClient, PERSONAL_ACCESS_TOKEN);

            // Act
            await adoClient.GetAsync("http://example.com/resource"); // normal call
            await adoClient.GetAsync("http://example.com/resource"); // call with retry delay
            await adoClient.GetAsync("http://example.com/resource"); // normal call

            // Assert
            _loggerMock.Verify(m => m.LogWarning("THROTTLING IN EFFECT. Waiting 1000 ms"), Times.Once);
        }

        [Fact]
        public async Task GetAsync_Logs_The_Url()
        {
            // Arrange
            using var httpClient = new HttpClient(MockHttpHandlerForGet().Object);
            var adoClient = new AdoClient(_loggerMock.Object, httpClient, PERSONAL_ACCESS_TOKEN);
            var url = "http://example.com/resource";

            // Act
            await adoClient.GetAsync(url);

            // Assert
            _loggerMock.Verify(m => m.LogVerbose($"HTTP GET: {url}"), Times.Once);
        }

        [Fact]
        public async Task GetAsync_Returns_String_Response()
        {
            // Arrange
            using var httpClient = new HttpClient(MockHttpHandlerForGet().Object);
            var adoClient = new AdoClient(_loggerMock.Object, httpClient, PERSONAL_ACCESS_TOKEN);

            // Act
            var actualContent = await adoClient.GetAsync("http://example.com/resource");

            // Assert
            actualContent.Should().Be(EXPECTED_RESPONSE_CONTENT);
        }

        [Fact]
        public async Task GetAsync_Logs_The_Response_Status_Code_And_Content()
        {
            // Arrange
            using var httpClient = new HttpClient(MockHttpHandlerForGet().Object);
            var adoClient = new AdoClient(_loggerMock.Object, httpClient, PERSONAL_ACCESS_TOKEN);
            var url = "http://example.com/resource";

            // Act
            await adoClient.GetAsync(url);

            // Assert
            _loggerMock.Verify(m => m.LogVerbose($"RESPONSE ({_httpResponse.StatusCode}): {EXPECTED_RESPONSE_CONTENT}"), Times.Once);
        }

        [Fact]
        public async Task GetAsync_Throws_HttpRequestException_On_Non_Success_Response()
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
                    var adoClient = new AdoClient(_loggerMock.Object, httpClient, PERSONAL_ACCESS_TOKEN);
                    return adoClient.GetAsync("http://example.com/resource");
                })
                .Should()
                .ThrowExactlyAsync<HttpRequestException>();
        }

        [Fact]
        public async Task PostAsync_Encodes_The_Url()
        {
            var handlerMock = MockHttpHandlerForPost();
            using var httpClient = new HttpClient(handlerMock.Object);
            var adoClient = new AdoClient(_loggerMock.Object, httpClient, PERSONAL_ACCESS_TOKEN);

            const string actualUrl = "http://example.com/param with space";
            const string expectedUrl = "http://example.com/param%20with%20space";

            // Act
            await adoClient.PostAsync(actualUrl, _rawRequestBody);

            // Assert
            handlerMock.Protected().Verify(
                "SendAsync",
                Times.Once(),
                ItExpr.Is<HttpRequestMessage>(msg => msg.RequestUri.AbsoluteUri == expectedUrl),
                ItExpr.IsAny<CancellationToken>());
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
            using var thridResponse = new HttpResponseMessage(HttpStatusCode.OK)
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
                .ReturnsAsync(thridResponse);

            using var httpClient = new HttpClient(handlerMock.Object);
            var adoClient = new AdoClient(_loggerMock.Object, httpClient, PERSONAL_ACCESS_TOKEN);

            // Act
            await adoClient.PostAsync("http://example.com/resource", _rawRequestBody); // normal call
            await adoClient.PostAsync("http://example.com/resource", _rawRequestBody); // call with retry delay
            await adoClient.PostAsync("http://example.com/resource", _rawRequestBody); // normal call

            // Assert
            _loggerMock.Verify(m => m.LogWarning("THROTTLING IN EFFECT. Waiting 1000 ms"), Times.Once);
        }

        [Fact]
        public async Task PostAsync_Logs_The_Url()
        {
            // Arrange
            using var httpClient = new HttpClient(MockHttpHandlerForPost().Object);
            var adoClient = new AdoClient(_loggerMock.Object, httpClient, PERSONAL_ACCESS_TOKEN);
            var url = "http://example.com/resource";

            // Act
            await adoClient.PostAsync(url, _rawRequestBody);

            // Assert
            _loggerMock.Verify(m => m.LogVerbose($"HTTP POST: {url}"), Times.Once);
        }

        [Fact]
        public async Task PostAsync_Logs_The_Request_Body()
        {
            // Arrange
            using var httpClient = new HttpClient(MockHttpHandlerForPost().Object);
            var adoClient = new AdoClient(_loggerMock.Object, httpClient, PERSONAL_ACCESS_TOKEN);

            // Act
            await adoClient.PostAsync("http://example.com/resource", _rawRequestBody);

            // Assert
            _loggerMock.Verify(m => m.LogVerbose($"HTTP BODY: {EXPECTED_JSON_REQUEST_BODY}"), Times.Once);
        }

        [Fact]
        public async Task PostAsync_Returns_String_Response()
        {
            // Arrange
            using var httpClient = new HttpClient(MockHttpHandlerForPost().Object);
            var adoClient = new AdoClient(_loggerMock.Object, httpClient, PERSONAL_ACCESS_TOKEN);

            // Act
            var actualContent = await adoClient.PostAsync("http://example.com/resource", _rawRequestBody);

            // Assert
            actualContent.Should().Be(EXPECTED_RESPONSE_CONTENT);
        }

        [Fact]
        public async Task PostAsync_Logs_The_Response_Status_Code_And_Content()
        {
            // Arrange
            using var httpClient = new HttpClient(MockHttpHandlerForPost().Object);
            var adoClient = new AdoClient(_loggerMock.Object, httpClient, PERSONAL_ACCESS_TOKEN);

            // Act
            await adoClient.PostAsync("http://example.com/resource", _rawRequestBody);

            // Assert
            _loggerMock.Verify(m => m.LogVerbose($"RESPONSE ({_httpResponse.StatusCode}): {EXPECTED_RESPONSE_CONTENT}"), Times.Once);
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
                    var adoClient = new AdoClient(_loggerMock.Object, httpClient, PERSONAL_ACCESS_TOKEN);
                    return adoClient.PostAsync("http://example.com/resource", _rawRequestBody);
                })
                .Should()
                .ThrowExactlyAsync<HttpRequestException>();
        }

        [Fact]
        public async Task PutAsync_Encodes_The_Url()
        {
            var handlerMock = MockHttpHandlerForPut();
            using var httpClient = new HttpClient(handlerMock.Object);
            var adoClient = new AdoClient(_loggerMock.Object, httpClient, PERSONAL_ACCESS_TOKEN);

            const string actualUrl = "http://example.com/param with space";
            const string expectedUrl = "http://example.com/param%20with%20space";

            // Act
            await adoClient.PutAsync(actualUrl, _rawRequestBody);

            // Assert
            handlerMock.Protected().Verify(
                "SendAsync",
                Times.Once(),
                ItExpr.Is<HttpRequestMessage>(msg => msg.RequestUri.AbsoluteUri == expectedUrl),
                ItExpr.IsAny<CancellationToken>());
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
            using var thridResponse = new HttpResponseMessage(HttpStatusCode.OK)
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
                .ReturnsAsync(thridResponse);

            using var httpClient = new HttpClient(handlerMock.Object);
            var adoClient = new AdoClient(_loggerMock.Object, httpClient, PERSONAL_ACCESS_TOKEN);

            // Act
            await adoClient.PutAsync("http://example.com/resource", _rawRequestBody); // normal call
            await adoClient.PutAsync("http://example.com/resource", _rawRequestBody); // call with retry delay
            await adoClient.PutAsync("http://example.com/resource", _rawRequestBody); // normal call

            // Assert
            _loggerMock.Verify(m => m.LogWarning("THROTTLING IN EFFECT. Waiting 1000 ms"), Times.Once);
        }

        [Fact]
        public async Task PutAsync_Logs_The_Url()
        {
            // Arrange
            using var httpClient = new HttpClient(MockHttpHandlerForPut().Object);
            var adoClient = new AdoClient(_loggerMock.Object, httpClient, PERSONAL_ACCESS_TOKEN);
            var url = "http://example.com/resource";

            // Act
            await adoClient.PutAsync(url, _rawRequestBody);

            // Assert
            _loggerMock.Verify(m => m.LogVerbose($"HTTP PUT: {url}"), Times.Once);
        }

        [Fact]
        public async Task PutAsync_Logs_The_Request_Body()
        {
            // Arrange
            using var httpClient = new HttpClient(MockHttpHandlerForPut().Object);
            var adoClient = new AdoClient(_loggerMock.Object, httpClient, PERSONAL_ACCESS_TOKEN);

            // Act
            await adoClient.PutAsync("http://example.com/resource", _rawRequestBody);

            // Assert
            _loggerMock.Verify(m => m.LogVerbose($"HTTP BODY: {EXPECTED_JSON_REQUEST_BODY}"), Times.Once);
        }

        [Fact]
        public async Task PutAsync_Returns_String_Response()
        {
            // Arrange
            using var httpClient = new HttpClient(MockHttpHandlerForPut().Object);
            var adoClient = new AdoClient(_loggerMock.Object, httpClient, PERSONAL_ACCESS_TOKEN);

            // Act
            var actualContent = await adoClient.PutAsync("http://example.com/resource", _rawRequestBody);

            // Assert
            actualContent.Should().Be(EXPECTED_RESPONSE_CONTENT);
        }

        [Fact]
        public async Task PutAsync_Logs_The_Response_Status_Code_And_Content()
        {
            // Arrange
            using var httpClient = new HttpClient(MockHttpHandlerForPut().Object);
            var adoClient = new AdoClient(_loggerMock.Object, httpClient, PERSONAL_ACCESS_TOKEN);

            // Act
            await adoClient.PutAsync("http://example.com/resource", _rawRequestBody);

            // Assert
            _loggerMock.Verify(m => m.LogVerbose($"RESPONSE ({_httpResponse.StatusCode}): {EXPECTED_RESPONSE_CONTENT}"), Times.Once);
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
                    var adoClient = new AdoClient(_loggerMock.Object, httpClient, PERSONAL_ACCESS_TOKEN);
                    return adoClient.PutAsync("http://example.com/resource", _rawRequestBody);
                })
                .Should()
                .ThrowExactlyAsync<HttpRequestException>();
        }

        [Fact]
        public async Task PatchAsync_Encodes_The_Url()
        {
            var handlerMock = MockHttpHandlerForPatch();
            using var httpClient = new HttpClient(handlerMock.Object);
            var adoClient = new AdoClient(_loggerMock.Object, httpClient, PERSONAL_ACCESS_TOKEN);

            const string actualUrl = "http://example.com/param with space";
            const string expectedUrl = "http://example.com/param%20with%20space";

            // Act
            await adoClient.PatchAsync(actualUrl, _rawRequestBody);

            // Assert
            handlerMock.Protected().Verify(
                "SendAsync",
                Times.Once(),
                ItExpr.Is<HttpRequestMessage>(msg => msg.RequestUri.AbsoluteUri == expectedUrl),
                ItExpr.IsAny<CancellationToken>());
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
            using var thridResponse = new HttpResponseMessage(HttpStatusCode.OK)
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
                .ReturnsAsync(thridResponse);

            using var httpClient = new HttpClient(handlerMock.Object);
            var adoClient = new AdoClient(_loggerMock.Object, httpClient, PERSONAL_ACCESS_TOKEN);

            // Act
            await adoClient.PatchAsync("http://example.com/resource", _rawRequestBody); // normal call
            await adoClient.PatchAsync("http://example.com/resource", _rawRequestBody); // call with retry delay
            await adoClient.PatchAsync("http://example.com/resource", _rawRequestBody); // normal call

            // Assert
            _loggerMock.Verify(m => m.LogWarning("THROTTLING IN EFFECT. Waiting 1000 ms"), Times.Once);
        }

        [Fact]
        public async Task PatchAsync_Logs_The_Url()
        {
            // Arrange
            using var httpClient = new HttpClient(MockHttpHandlerForPatch().Object);
            var adoClient = new AdoClient(_loggerMock.Object, httpClient, PERSONAL_ACCESS_TOKEN);
            var url = "http://example.com/resource";

            // Act
            await adoClient.PatchAsync(url, _rawRequestBody);

            // Assert
            _loggerMock.Verify(m => m.LogVerbose($"HTTP PATCH: {url}"), Times.Once);
        }

        [Fact]
        public async Task PatchAsync_Logs_The_Request_Body()
        {
            // Arrange
            using var httpClient = new HttpClient(MockHttpHandlerForPatch().Object);
            var adoClient = new AdoClient(_loggerMock.Object, httpClient, PERSONAL_ACCESS_TOKEN);

            // Act
            await adoClient.PatchAsync("http://example.com/resource", _rawRequestBody);

            // Assert
            _loggerMock.Verify(m => m.LogVerbose($"HTTP BODY: {EXPECTED_JSON_REQUEST_BODY}"), Times.Once);
        }

        [Fact]
        public async Task PatchAsync_Returns_String_Response()
        {
            // Arrange
            using var httpClient = new HttpClient(MockHttpHandlerForPatch().Object);
            var adoClient = new AdoClient(_loggerMock.Object, httpClient, PERSONAL_ACCESS_TOKEN);

            // Act
            var actualContent = await adoClient.PatchAsync("http://example.com/resource", _rawRequestBody);

            // Assert
            actualContent.Should().Be(EXPECTED_RESPONSE_CONTENT);
        }

        [Fact]
        public async Task PatchAsync_Logs_The_Response_Status_Code_And_Content()
        {
            // Arrange
            using var httpClient = new HttpClient(MockHttpHandlerForPatch().Object);
            var adoClient = new AdoClient(_loggerMock.Object, httpClient, PERSONAL_ACCESS_TOKEN);

            // Act
            await adoClient.PatchAsync("http://example.com/resource", _rawRequestBody);

            // Assert
            _loggerMock.Verify(m => m.LogVerbose($"RESPONSE ({_httpResponse.StatusCode}): {EXPECTED_RESPONSE_CONTENT}"), Times.Once);
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
                    var adoClient = new AdoClient(_loggerMock.Object, httpClient, PERSONAL_ACCESS_TOKEN);
                    return adoClient.PatchAsync("http://example.com/resource", _rawRequestBody);
                })
                .Should()
                .ThrowExactlyAsync<HttpRequestException>();
        }

        private Mock<HttpMessageHandler> MockHttpHandlerForGet() =>
            MockHttpHandler(req => req.Method == HttpMethod.Get);

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
}
