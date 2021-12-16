using System;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Moq;
using Moq.Protected;
using Xunit;

namespace OctoshiftCLI.Tests
{
    public sealed class GithubClientTests : IDisposable
    {
        private readonly Mock<OctoLogger> _loggerMock;
        private readonly HttpResponseMessage _httpResponse;
        private readonly object _rawRequestBody;
        private const string EXPECTED_JSON_REQUEST_BODY = "{\"id\":\"ID\"}";
        private const string EXPECTED_RESPONSE_CONTENT = "RESPONSE_CONTENT";

        public GithubClientTests()
        {
            _rawRequestBody = new { id = "ID" };

            _httpResponse = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(EXPECTED_RESPONSE_CONTENT)
            };

            _loggerMock = new Mock<OctoLogger>();
        }

        public void Dispose()
        {
            _httpResponse?.Dispose();
        }

        [Fact]
        public async Task GetAsync_Returns_String_Response()
        {
            // Arrange
            using var httpClient = new HttpClient(MockHttpHandlerForGet().Object);
            using var githubClient = new GithubClient(_loggerMock.Object, httpClient);

            // Act
            var actualContent = await githubClient.GetAsync("http://example.com");

            // Assert
            actualContent.Should().Be(EXPECTED_RESPONSE_CONTENT);
        }

        [Fact]
        public async Task GetAsync_Encodes_The_Url()
        {
            // Arrange
            var handlerMock = MockHttpHandlerForGet();
            using var httpClient = new HttpClient(handlerMock.Object);
            using var githubClient = new GithubClient(_loggerMock.Object, httpClient);

            const string actualUrl = "http://example.com/param with space";
            const string expectedUrl = "http://example.com/param%20with%20space";

            // Act
            await githubClient.GetAsync(actualUrl);

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
            using var githubClient = new GithubClient(_loggerMock.Object, httpClient);

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
            using var httpClient = new HttpClient(MockHttpHandlerForGet().Object);
            using var githubClient = new GithubClient(_loggerMock.Object, httpClient);

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
            // Assert
            await FluentActions
                .Invoking(() =>
                {
                    using var httpClient = new HttpClient(handlerMock.Object);
                    using var githubClient = new GithubClient(_loggerMock.Object, httpClient);
                    return githubClient.GetAsync("http://example.com");
                })
                .Should()
                .ThrowExactlyAsync<HttpRequestException>();
        }

        [Fact]
        public async Task PostAsync_Returns_String_Response()
        {
            // Arrange
            using var httpClient = new HttpClient(MockHttpHandlerForPost().Object);
            using var githubClient = new GithubClient(_loggerMock.Object, httpClient);

            // Act
            var actualContent = await githubClient.PostAsync("http://example.com", _rawRequestBody);

            // Assert
            actualContent.Should().Be(EXPECTED_RESPONSE_CONTENT);
        }

        [Fact]
        public async Task PostAsync_Encodes_The_Url()
        {
            // Arrange
            var handlerMock = MockHttpHandlerForPost();
            using var httpClient = new HttpClient(handlerMock.Object);
            using var githubClient = new GithubClient(_loggerMock.Object, httpClient);

            const string actualUrl = "http://example.com/param with space";
            const string expectedUrl = "http://example.com/param%20with%20space";

            // Act
            await githubClient.PostAsync(actualUrl, _rawRequestBody);

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
            using var githubClient = new GithubClient(_loggerMock.Object, httpClient);

            const string url = "http://example.com";
            var expectedLogMessage = $"HTTP POST: {url}";

            // Act
            await githubClient.PostAsync(url, _rawRequestBody);

            // Assert
            _loggerMock.Verify(m =>
                m.LogVerbose(It.Is<string>(actualLogMessage => actualLogMessage == expectedLogMessage)));
        }

        [Fact]
        public async Task PostAsync_Logs_The_Request_Body()
        {
            // Arrange
            using var httpClient = new HttpClient(MockHttpHandlerForPost().Object);
            using var githubClient = new GithubClient(_loggerMock.Object, httpClient);

            var expectedLogMessage = $"HTTP BODY: {EXPECTED_JSON_REQUEST_BODY}";

            // Act
            await githubClient.PostAsync("http://example.com", _rawRequestBody);

            // Assert
            _loggerMock.Verify(m =>
                m.LogVerbose(It.Is<string>(actualLogMessage => actualLogMessage == expectedLogMessage)));
        }

        [Fact]
        public async Task PostAsync_Logs_The_Response_Status_Code_And_Content()
        {
            // Arrange
            using var httpClient = new HttpClient(MockHttpHandlerForPost().Object);
            using var githubClient = new GithubClient(_loggerMock.Object, httpClient);

            var expectedLogMessage = $"RESPONSE ({_httpResponse.StatusCode}): {EXPECTED_RESPONSE_CONTENT}";

            // Act
            await githubClient.PostAsync("http://example.com", _rawRequestBody);

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
                    ItExpr.Is<HttpRequestMessage>(x =>
                        x.Content.ReadAsStringAsync().Result == EXPECTED_JSON_REQUEST_BODY),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(httpResponse);

            var loggerMock = new Mock<OctoLogger>();

            // Act
            // Assert
            await FluentActions
                .Invoking(() =>
                {
                    using var httpClient = new HttpClient(handlerMock.Object);
                    using var githubClient = new GithubClient(loggerMock.Object, httpClient);
                    return githubClient.PostAsync("http://example.com", _rawRequestBody);
                })
                .Should()
                .ThrowExactlyAsync<HttpRequestException>();
        }

        [Fact]
        public async Task PutAsync_Returns_String_Response()
        {
            // Arrange
            using var httpClient = new HttpClient(MockHttpHandlerForPut().Object);
            using var githubClient = new GithubClient(_loggerMock.Object, httpClient);

            // Act
            var actualContent = await githubClient.PutAsync("http://example.com", _rawRequestBody);

            // Assert
            actualContent.Should().Be(EXPECTED_RESPONSE_CONTENT);
        }

        [Fact]
        public async Task PutAsync_Encodes_The_Url()
        {
            // Arrange
            var handlerMock = MockHttpHandlerForPut();
            using var httpClient = new HttpClient(handlerMock.Object);
            using var githubClient = new GithubClient(_loggerMock.Object, httpClient);

            const string actualUrl = "http://example.com/param with space";
            const string expectedUrl = "http://example.com/param%20with%20space";

            // Act
            await githubClient.PutAsync(actualUrl, _rawRequestBody);

            // Assert
            handlerMock.Protected().Verify(
                "SendAsync",
                Times.Once(),
                ItExpr.Is<HttpRequestMessage>(msg => msg.RequestUri.AbsoluteUri == expectedUrl),
                ItExpr.IsAny<CancellationToken>());
        }

        [Fact]
        public async Task PutAsync_Logs_The_Url()
        {
            // Arrange
            using var httpClient = new HttpClient(MockHttpHandlerForPut().Object);
            using var githubClient = new GithubClient(_loggerMock.Object, httpClient);

            const string url = "http://example.com";
            var expectedLogMessage = $"HTTP PUT: {url}";

            // Act
            await githubClient.PutAsync(url, _rawRequestBody);

            // Assert
            _loggerMock.Verify(m =>
                m.LogVerbose(It.Is<string>(actualLogMessage => actualLogMessage == expectedLogMessage)));
        }

        [Fact]
        public async Task PutAsync_Logs_The_Request_Body()
        {
            // Arrange
            using var httpClient = new HttpClient(MockHttpHandlerForPut().Object);
            using var githubClient = new GithubClient(_loggerMock.Object, httpClient);

            var expectedLogMessage = $"HTTP BODY: {EXPECTED_JSON_REQUEST_BODY}";

            // Act
            await githubClient.PutAsync("http://example.com", _rawRequestBody);

            // Assert
            _loggerMock.Verify(m =>
                m.LogVerbose(It.Is<string>(actualLogMessage => actualLogMessage == expectedLogMessage)));
        }

        [Fact]
        public async Task PutAsync_Logs_The_Response_Status_Code_And_Content()
        {
            // Arrange
            using var httpClient = new HttpClient(MockHttpHandlerForPut().Object);
            using var githubClient = new GithubClient(_loggerMock.Object, httpClient);

            var expectedLogMessage = $"RESPONSE ({_httpResponse.StatusCode}): {EXPECTED_RESPONSE_CONTENT}";

            // Act
            await githubClient.PutAsync("http://example.com", _rawRequestBody);

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
                    ItExpr.Is<HttpRequestMessage>(x =>
                        x.Content.ReadAsStringAsync().Result == EXPECTED_JSON_REQUEST_BODY),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(httpResponse);

            var loggerMock = new Mock<OctoLogger>();

            // Act
            // Assert
            await FluentActions
                .Invoking(() =>
                {
                    using var httpClient = new HttpClient(handlerMock.Object);
                    using var githubClient = new GithubClient(loggerMock.Object, httpClient);
                    return githubClient.PutAsync("http://example.com", _rawRequestBody);
                })
                .Should()
                .ThrowExactlyAsync<HttpRequestException>();
        }

        [Fact]
        public async Task PatchAsync_Returns_String_Response()
        {
            // Arrange
            using var httpClient = new HttpClient(MockHttpHandlerForPatch().Object);
            using var githubClient = new GithubClient(_loggerMock.Object, httpClient);

            // Act
            var actualContent = await githubClient.PatchAsync("http://example.com", _rawRequestBody);

            // Assert
            actualContent.Should().Be(EXPECTED_RESPONSE_CONTENT);
        }

        [Fact]
        public async Task PatchAsync_Encodes_The_Url()
        {
            // Arrange
            var handlerMock = MockHttpHandlerForPatch();
            using var httpClient = new HttpClient(handlerMock.Object);
            using var githubClient = new GithubClient(_loggerMock.Object, httpClient);

            const string actualUrl = "http://example.com/param with space";
            const string expectedUrl = "http://example.com/param%20with%20space";

            // Act
            await githubClient.PatchAsync(actualUrl, _rawRequestBody);

            // Assert
            handlerMock.Protected().Verify(
                "SendAsync",
                Times.Once(),
                ItExpr.Is<HttpRequestMessage>(msg => msg.RequestUri.AbsoluteUri == expectedUrl),
                ItExpr.IsAny<CancellationToken>());
        }

        [Fact]
        public async Task PatchAsync_Logs_The_Url()
        {
            // Arrange
            using var httpClient = new HttpClient(MockHttpHandlerForPatch().Object);
            using var githubClient = new GithubClient(_loggerMock.Object, httpClient);

            const string url = "http://example.com";
            var expectedLogMessage = $"HTTP PATCH: {url}";

            // Act
            await githubClient.PatchAsync(url, _rawRequestBody);

            // Assert
            _loggerMock.Verify(m =>
                m.LogVerbose(It.Is<string>(actualLogMessage => actualLogMessage == expectedLogMessage)));
        }

        [Fact]
        public async Task PatchAsync_Logs_The_Request_Body()
        {
            // Arrange
            using var httpClient = new HttpClient(MockHttpHandlerForPatch().Object);
            using var githubClient = new GithubClient(_loggerMock.Object, httpClient);

            var expectedLogMessage = $"HTTP BODY: {EXPECTED_JSON_REQUEST_BODY}";

            // Act
            await githubClient.PatchAsync("http://example.com", _rawRequestBody);

            // Assert
            _loggerMock.Verify(m =>
                m.LogVerbose(It.Is<string>(actualLogMessage => actualLogMessage == expectedLogMessage)));
        }

        [Fact]
        public async Task PatchAsync_Logs_The_Response_Status_Code_And_Content()
        {
            // Arrange
            using var httpClient = new HttpClient(MockHttpHandlerForPatch().Object);
            using var githubClient = new GithubClient(_loggerMock.Object, httpClient);

            var expectedLogMessage = $"RESPONSE ({_httpResponse.StatusCode}): {EXPECTED_RESPONSE_CONTENT}";

            // Act
            await githubClient.PatchAsync("http://example.com", _rawRequestBody);

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
                    ItExpr.Is<HttpRequestMessage>(x =>
                        x.Content.ReadAsStringAsync().Result == EXPECTED_JSON_REQUEST_BODY),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(httpResponse);

            var loggerMock = new Mock<OctoLogger>();

            // Act
            // Assert
            await FluentActions
                .Invoking(() =>
                {
                    using var httpClient = new HttpClient(handlerMock.Object);
                    using var githubClient = new GithubClient(loggerMock.Object, httpClient);
                    return githubClient.PatchAsync("http://example.com", _rawRequestBody);
                })
                .Should()
                .ThrowExactlyAsync<HttpRequestException>();
        }

        [Fact]
        public async Task DeleteAsync_Returns_String_Response()
        {
            // Arrange
            using var httpClient = new HttpClient(MockHttpHandlerForDelete().Object);
            using var githubClient = new GithubClient(_loggerMock.Object, httpClient);

            // Act
            var actualContent = await githubClient.DeleteAsync("http://example.com");

            // Assert
            actualContent.Should().Be(EXPECTED_RESPONSE_CONTENT);
        }

        [Fact]
        public async Task DeleteAsync_Encodes_The_Url()
        {
            // Arrange
            var handlerMock = MockHttpHandlerForDelete();
            using var httpClient = new HttpClient(handlerMock.Object);
            using var githubClient = new GithubClient(_loggerMock.Object, httpClient);

            const string actualUrl = "http://example.com/param with space";
            const string expectedUrl = "http://example.com/param%20with%20space";

            // Act
            await githubClient.DeleteAsync(actualUrl);

            // Assert
            handlerMock.Protected().Verify(
                "SendAsync",
                Times.Once(),
                ItExpr.Is<HttpRequestMessage>(msg => msg.RequestUri.AbsoluteUri == expectedUrl),
                ItExpr.IsAny<CancellationToken>());
        }

        [Fact]
        public async Task DeleteAsync_Logs_The_Url()
        {
            // Arrange
            using var httpClient = new HttpClient(MockHttpHandlerForDelete().Object);
            using var githubClient = new GithubClient(_loggerMock.Object, httpClient);

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
            using var httpClient = new HttpClient(MockHttpHandlerForDelete().Object);
            using var githubClient = new GithubClient(_loggerMock.Object, httpClient);

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
            // Assert
            await FluentActions
                .Invoking(() =>
                {
                    using var httpClient = new HttpClient(handlerMock.Object);
                    using var githubClient = new GithubClient(loggerMock.Object, httpClient);
                    return githubClient.DeleteAsync("http://example.com");
                })
                .Should()
                .ThrowExactlyAsync<HttpRequestException>();
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

        private Mock<HttpMessageHandler> MockHttpHandler(Func<HttpRequestMessage, bool> requestMatcher)
        {
            var handlerMock = new Mock<HttpMessageHandler>();
            handlerMock
                .Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.Is<HttpRequestMessage>(x => requestMatcher(x)),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(_httpResponse);
            return handlerMock;
        }
    }
}