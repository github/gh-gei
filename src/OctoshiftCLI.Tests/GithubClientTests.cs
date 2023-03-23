using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Moq;
using Moq.Protected;
using Newtonsoft.Json.Linq;
using OctoshiftCLI.Contracts;
using OctoshiftCLI.Extensions;
using Xunit;

namespace OctoshiftCLI.Tests
{
    public sealed class GithubClientTests : IDisposable
    {
        private readonly Mock<OctoLogger> _mockOctoLogger = TestHelpers.CreateMock<OctoLogger>();
        private readonly HttpResponseMessage _defaultHttpResponse;
        private readonly HttpResponseMessage _graphqlHttpResponse;
        private readonly RetryPolicy _retryPolicy;
        private readonly Mock<DateTimeProvider> _dateTimeProvider = TestHelpers.CreateMock<DateTimeProvider>();
        private readonly object _rawRequestBody;
        private const string GITHUB_REQUEST_ID = "123-456-789";
        private const string EXPECTED_JSON_REQUEST_BODY = "{\"id\":\"ID\"}";
        private const string EXPECTED_RESPONSE_CONTENT = "RESPONSE_CONTENT";
        private const string EXPECTED_GRAPHQL_JSON_RESPONSE_BODY = "{\"id\":\"ID\"}";
        private const string EXPECTED_GRAPHQL_JSON_ERROR_RESPONSE_BODY = "{\"data\":{\"createMigrationSource\":null},\"errors\":[{\"type\":\"FORBIDDEN\",\"path\":[\"createMigrationSource\"],\"extensions\":{\"saml_failure\":true},\"locations\":[{\"line\":1,\"column\":109}],\"message\":\"Resource protected by organization SAML enforcement. You must grant your Personal Access token access to this organization.\"}]}";
        private const string PERSONAL_ACCESS_TOKEN = "PERSONAL_ACCESS_TOKEN";
        private const string URL = "http://example.com/resource";

        public GithubClientTests()
        {
            _rawRequestBody = new { id = "ID" };

            _defaultHttpResponse = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(EXPECTED_RESPONSE_CONTENT)
            };

            _graphqlHttpResponse = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(EXPECTED_GRAPHQL_JSON_RESPONSE_BODY)
            };

            _retryPolicy = new RetryPolicy(_mockOctoLogger.Object)
            {
                _httpRetryInterval = 0
            };
        }

        public void Dispose()
        {
            _defaultHttpResponse?.Dispose();
            _graphqlHttpResponse?.Dispose();
        }

        [Fact]
        public async Task GetAsync_Returns_String_Response()
        {
            // Arrange
            using var httpClient = new HttpClient(MockHttpHandlerForGet().Object);
            var githubClient = new GithubClient(_mockOctoLogger.Object, httpClient, null, _retryPolicy, _dateTimeProvider.Object, null);

            // Act
            var actualContent = await githubClient.GetAsync("http://example.com");

            // Assert
            actualContent.Should().Be(EXPECTED_RESPONSE_CONTENT);
        }

        [Fact]
        public async Task Custom_Headers_Are_Added_And_Removed()
        {
            // Arrange
            const string graphQLSchemaHeaderName = "GraphQL-schema";
            const string graphQLSchemaHeaderValue = "internal";
            var handlerMock = MockHttpHandlerForGet();
            using var httpClient = new HttpClient(handlerMock.Object);
            var githubClient = new GithubClient(_mockOctoLogger.Object, httpClient, null, _retryPolicy, _dateTimeProvider.Object, null);

            // Act
            var internalSchemaHeader = new Dictionary<string, string>() { { graphQLSchemaHeaderName, graphQLSchemaHeaderValue } };
            await githubClient.GetAsync("http://example.com", internalSchemaHeader);

            // Assert
            handlerMock.Protected().Verify(
                "SendAsync",
                Times.Once(),
                ItExpr.Is<HttpRequestMessage>(msg => msg.Headers.Any(kv => kv.Key == graphQLSchemaHeaderName && kv.Value.First() == graphQLSchemaHeaderValue)),
                ItExpr.IsAny<CancellationToken>());
        }

        [Fact]
        public async Task GetAsync_Encodes_The_Url()
        {
            // Arrange
            var handlerMock = MockHttpHandlerForGet();
            using var httpClient = new HttpClient(handlerMock.Object);
            var githubClient = new GithubClient(_mockOctoLogger.Object, httpClient, null, _retryPolicy, _dateTimeProvider.Object, PERSONAL_ACCESS_TOKEN);

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
            var githubClient = new GithubClient(_mockOctoLogger.Object, httpClient, null, _retryPolicy, _dateTimeProvider.Object, PERSONAL_ACCESS_TOKEN);

            // Act
            var returnedContent = await githubClient.GetAsync(URL);

            // Assert
            returnedContent.Should().Be("SECOND_RESPONSE");
        }

        [Fact]
        public async Task GetAsync_Logs_The_Url()
        {
            // Arrange
            using var httpClient = new HttpClient(MockHttpHandlerForGet().Object);
            var githubClient = new GithubClient(_mockOctoLogger.Object, httpClient, null, _retryPolicy, _dateTimeProvider.Object, PERSONAL_ACCESS_TOKEN);

            const string url = "http://example.com";
            var expectedLogMessage = $"HTTP GET: {url}";

            // Act
            await githubClient.GetAsync(url);

            // Assert
            _mockOctoLogger.Verify(m =>
                m.LogVerbose(It.Is<string>(actualLogMessage => actualLogMessage == expectedLogMessage)));
        }

        [Fact]
        public async Task GetAsync_Logs_The_GitHub_Request_Id_Header_Value()
        {
            // Arrange
            using var httpClient = new HttpClient(MockHttpHandlerForGet().Object);
            var githubClient = new GithubClient(_mockOctoLogger.Object, httpClient, null, _retryPolicy, _dateTimeProvider.Object, PERSONAL_ACCESS_TOKEN);

            const string githubRequestId = "123-456-789";
            _defaultHttpResponse.Headers.Add("X-GitHub-Request-Id", githubRequestId);

            const string expectedLogMessage = $"GITHUB REQUEST ID: {githubRequestId}";

            // Act
            await githubClient.GetAsync("http://example.com");

            // Assert
            _mockOctoLogger.Verify(m =>
                m.LogVerbose(It.Is<string>(actualLogMessage => actualLogMessage == expectedLogMessage)));
        }

        [Fact]
        public async Task GetAsync_Logs_The_Response_Status_Code_And_Content()
        {
            // Arrange
            using var httpClient = new HttpClient(MockHttpHandlerForGet().Object);
            var githubClient = new GithubClient(_mockOctoLogger.Object, httpClient, null, _retryPolicy, _dateTimeProvider.Object, PERSONAL_ACCESS_TOKEN);

            var expectedLogMessage = $"RESPONSE ({HttpStatusCode.OK}): {EXPECTED_RESPONSE_CONTENT}";

            // Act
            await githubClient.GetAsync("http://example.com");

            // Assert
            _mockOctoLogger.Verify(m =>
                m.LogVerbose(It.Is<string>(actualLogMessage => actualLogMessage == expectedLogMessage)));
        }

        [Fact]
        public async Task GetAsync_Throws_HttpRequestException_On_Non_Success_Response()
        {
            // Arrange
            var httpResponse = () => new HttpResponseMessage(HttpStatusCode.InternalServerError);
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
                    var githubClient = new GithubClient(_mockOctoLogger.Object, httpClient, null, _retryPolicy, _dateTimeProvider.Object, PERSONAL_ACCESS_TOKEN);
                    return await githubClient.GetAsync("http://example.com");
                })
                .Should()
                .ThrowExactlyAsync<HttpRequestException>();
        }

        [Fact]
        public async Task GetAsync_Applies_Retry_Delay()
        {
            // Arrange
            var now = DateTimeOffset.Now.ToUnixTimeSeconds();
            var retryAt = now + 1;

            _dateTimeProvider.Setup(m => m.CurrentUnixTimeSeconds()).Returns(now);

            using var firstHttpResponse = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("FIRST_RESPONSE")
            };

            using var secondHttpResponse = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("API RATE LIMIT EXCEEDED blah blah blah")
            };

            secondHttpResponse.Headers.Add("X-RateLimit-Reset", retryAt.ToString());
            secondHttpResponse.Headers.Add("X-RateLimit-Remaining", "0");

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
            var githubClient = new GithubClient(_mockOctoLogger.Object, httpClient, null, _retryPolicy, _dateTimeProvider.Object, PERSONAL_ACCESS_TOKEN);

            // Act
            await githubClient.GetAsync("http://example.com"); // normal call
            await githubClient.GetAsync("http://example.com"); // call with retry delay
            await githubClient.GetAsync("http://example.com"); // normal call

            // Assert
            _mockOctoLogger.Verify(m => m.LogWarning("GitHub rate limit exceeded. Waiting 1 seconds before continuing"), Times.Once);
        }

        [Fact]
        public async Task GetAsync_Applies_Retry_Delay_If_Forbidden()
        {
            // Arrange
            var now = DateTimeOffset.Now.ToUnixTimeSeconds();
            var retryAt = now + 1;

            _dateTimeProvider.Setup(m => m.CurrentUnixTimeSeconds()).Returns(now);

            using var firstHttpResponse = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("FIRST_RESPONSE")
            };

            using var secondHttpResponse = new HttpResponseMessage(HttpStatusCode.Forbidden)
            {
                Content = new StringContent("API RATE LIMIT EXCEEDED blah blah blah")
            };

            secondHttpResponse.Headers.Add("X-RateLimit-Reset", retryAt.ToString());
            secondHttpResponse.Headers.Add("X-RateLimit-Remaining", "0");

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
            var githubClient = new GithubClient(_mockOctoLogger.Object, httpClient, null, _retryPolicy, _dateTimeProvider.Object, PERSONAL_ACCESS_TOKEN);

            // Act
            await githubClient.GetAsync("http://example.com"); // normal call
            await githubClient.GetAsync("http://example.com"); // call with delay and retry

            // Assert
            _mockOctoLogger.Verify(m => m.LogWarning("GitHub rate limit exceeded. Waiting 1 seconds before continuing"), Times.Once);
        }

        [Fact]
        public async Task PostAsync_Returns_String_Response()
        {
            // Arrange
            using var httpClient = new HttpClient(MockHttpHandlerForPost().Object);
            var githubClient = new GithubClient(_mockOctoLogger.Object, httpClient, null, _retryPolicy, _dateTimeProvider.Object, PERSONAL_ACCESS_TOKEN);

            // Act
            var actualContent = await githubClient.PostAsync("http://example.com", _rawRequestBody);

            // Assert
            actualContent.Should().Be(EXPECTED_RESPONSE_CONTENT);
        }

        [Fact]
        public async Task PostAsync_Does_Not_Apply_Retry_Delay_To_Bad_Credentials_Response()
        {
            // Arrange
            var now = DateTimeOffset.Now.ToUnixTimeSeconds();
            var retryAt = now + 4;

            _dateTimeProvider.Setup(m => m.CurrentUnixTimeSeconds()).Returns(now);

            using var badCredentialResponse1 = new HttpResponseMessage(HttpStatusCode.Unauthorized)
            {
                Content = new StringContent("{\"message\":\"Bad credentials\",\"documentation_url\":\"https://docs.github.com/graphql\"}")
            };

            badCredentialResponse1.Headers.Add("X-RateLimit-Reset", retryAt.ToString());
            badCredentialResponse1.Headers.Add("X-RateLimit-Remaining", "0");

            using var badCredentialResponse2 = new HttpResponseMessage(HttpStatusCode.Unauthorized)
            {
                Content = new StringContent("{\"message\":\"Bad credentials\",\"documentation_url\":\"https://docs.github.com/graphql\"}")
            };

            badCredentialResponse2.Headers.Add("X-RateLimit-Reset", retryAt.ToString());
            badCredentialResponse2.Headers.Add("X-RateLimit-Remaining", "0");

            var handlerMock = new Mock<HttpMessageHandler>();
            handlerMock
                .Protected()
                .SetupSequence<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.Is<HttpRequestMessage>(req => req.Method == HttpMethod.Post),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(badCredentialResponse1)
                .ReturnsAsync(badCredentialResponse2);

            using var httpClient = new HttpClient(handlerMock.Object);
            var githubClient = new GithubClient(_mockOctoLogger.Object, httpClient, null, _retryPolicy, _dateTimeProvider.Object, PERSONAL_ACCESS_TOKEN);

            // Act
            await FluentActions
                .Invoking(async () =>
                {
                    await githubClient.PostAsync("http://example.com", "hello");
                })
                .Should()
                .ThrowExactlyAsync<HttpRequestException>();

            await FluentActions
                .Invoking(async () =>
                {
                    await githubClient.PostAsync("http://example.com", "hello");
                })
                .Should()
                .ThrowAsync<HttpRequestException>();

            // Assert
            _mockOctoLogger.Verify(m => m.LogWarning(It.IsAny<string>()), Times.Never);
        }

        [Fact]
        public async Task PostAsync_Applies_Retry_Delay()
        {
            // Arrange
            var now = DateTimeOffset.Now.ToUnixTimeSeconds();
            var retryAt = now + 1;

            _dateTimeProvider.Setup(m => m.CurrentUnixTimeSeconds()).Returns(now);

            using var firstHttpResponse = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("FIRST_RESPONSE")
            };

            using var secondHttpResponse = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("API RATE LIMIT EXCEEDED blah blah blah")
            };

            secondHttpResponse.Headers.Add("X-RateLimit-Reset", retryAt.ToString());
            secondHttpResponse.Headers.Add("X-RateLimit-Remaining", "0");

            using var thirdResponse = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("THIRD_RESPONSE")
            };

            var handlerMock = new Mock<HttpMessageHandler>();
            handlerMock
                .Protected()
                .SetupSequence<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.Is<HttpRequestMessage>(req => req.Method == HttpMethod.Post),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(firstHttpResponse)
                .ReturnsAsync(secondHttpResponse)
                .ReturnsAsync(thirdResponse);

            using var httpClient = new HttpClient(handlerMock.Object);
            var githubClient = new GithubClient(_mockOctoLogger.Object, httpClient, null, _retryPolicy, _dateTimeProvider.Object, PERSONAL_ACCESS_TOKEN);

            // Act
            await githubClient.PostAsync("http://example.com", "hello"); // normal call
            await githubClient.PostAsync("http://example.com", "hello"); // call with retry delay
            await githubClient.PostAsync("http://example.com", "hello"); // normal call

            // Assert
            _mockOctoLogger.Verify(m => m.LogWarning("GitHub rate limit exceeded. Waiting 1 seconds before continuing"), Times.Once);
        }

        [Fact]
        public async Task PostAsync_Applies_Retry_Delay_If_Forbidden()
        {
            // Arrange
            var now = DateTimeOffset.Now.ToUnixTimeSeconds();
            var retryAt = now + 1;

            _dateTimeProvider.Setup(m => m.CurrentUnixTimeSeconds()).Returns(now);

            using var firstHttpResponse = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("FIRST_RESPONSE")
            };

            using var secondHttpResponse = new HttpResponseMessage(HttpStatusCode.Forbidden)
            {
                Content = new StringContent("API RATE LIMIT EXCEEDED blah blah blah")
            };

            secondHttpResponse.Headers.Add("X-RateLimit-Reset", retryAt.ToString());
            secondHttpResponse.Headers.Add("X-RateLimit-Remaining", "0");

            using var thirdResponse = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("THIRD_RESPONSE")
            };

            var handlerMock = new Mock<HttpMessageHandler>();
            handlerMock
                .Protected()
                .SetupSequence<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.Is<HttpRequestMessage>(req => req.Method == HttpMethod.Post),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(firstHttpResponse)
                .ReturnsAsync(secondHttpResponse)
                .ReturnsAsync(thirdResponse);

            using var httpClient = new HttpClient(handlerMock.Object);
            var githubClient = new GithubClient(_mockOctoLogger.Object, httpClient, null, _retryPolicy, _dateTimeProvider.Object, PERSONAL_ACCESS_TOKEN);

            // Act
            await githubClient.PostAsync("http://example.com", "hello"); // normal call
            var result = await githubClient.PostAsync("http://example.com", "hello"); // call with retry delay

            // Assert
            _mockOctoLogger.Verify(m => m.LogWarning("GitHub rate limit exceeded. Waiting 1 seconds before continuing"), Times.Once);
            result.Should().Be("THIRD_RESPONSE");
        }

        [Fact]
        public async Task PostAsync_Encodes_The_Url()
        {
            // Arrange
            var handlerMock = MockHttpHandlerForPost();
            using var httpClient = new HttpClient(handlerMock.Object);
            var githubClient = new GithubClient(_mockOctoLogger.Object, httpClient, null, _retryPolicy, _dateTimeProvider.Object, PERSONAL_ACCESS_TOKEN);

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
            var githubClient = new GithubClient(_mockOctoLogger.Object, httpClient, null, _retryPolicy, _dateTimeProvider.Object, PERSONAL_ACCESS_TOKEN);

            const string url = "http://example.com";
            var expectedLogMessage = $"HTTP POST: {url}";

            // Act
            await githubClient.PostAsync(url, _rawRequestBody);

            // Assert
            _mockOctoLogger.Verify(m =>
                m.LogVerbose(It.Is<string>(actualLogMessage => actualLogMessage == expectedLogMessage)));
        }

        [Fact]
        public async Task PostAsync_Logs_The_GitHub_Request_Id_Header_Value()
        {
            // Arrange
            using var httpClient = new HttpClient(MockHttpHandlerForPost().Object);
            var githubClient = new GithubClient(_mockOctoLogger.Object, httpClient, null, _retryPolicy, _dateTimeProvider.Object, PERSONAL_ACCESS_TOKEN);

            const string githubRequestId = "123-456-789";
            _defaultHttpResponse.Headers.Add("X-GitHub-Request-Id", githubRequestId);

            const string expectedLogMessage = $"GITHUB REQUEST ID: {githubRequestId}";

            // Act
            await githubClient.PostAsync("http://example.com", _rawRequestBody);

            // Assert
            _mockOctoLogger.Verify(m =>
                m.LogVerbose(It.Is<string>(actualLogMessage => actualLogMessage == expectedLogMessage)));
        }

        [Fact]
        public async Task PostAsync_Logs_The_Request_Body()
        {
            // Arrange
            using var httpClient = new HttpClient(MockHttpHandlerForPost().Object);
            var githubClient = new GithubClient(_mockOctoLogger.Object, httpClient, null, _retryPolicy, _dateTimeProvider.Object, PERSONAL_ACCESS_TOKEN);

            var expectedLogMessage = $"HTTP BODY: {EXPECTED_JSON_REQUEST_BODY}";

            // Act
            await githubClient.PostAsync("http://example.com", _rawRequestBody);

            // Assert
            _mockOctoLogger.Verify(m =>
                m.LogVerbose(It.Is<string>(actualLogMessage => actualLogMessage == expectedLogMessage)));
        }

        [Fact]
        public async Task PostAsync_Logs_The_Response_Status_Code_And_Content()
        {
            // Arrange
            using var httpClient = new HttpClient(MockHttpHandlerForPost().Object);
            var githubClient = new GithubClient(_mockOctoLogger.Object, httpClient, null, _retryPolicy, _dateTimeProvider.Object, PERSONAL_ACCESS_TOKEN);

            var expectedLogMessage = $"RESPONSE ({_defaultHttpResponse.StatusCode}): {EXPECTED_RESPONSE_CONTENT}";

            // Act
            await githubClient.PostAsync("http://example.com", _rawRequestBody);

            // Assert
            _mockOctoLogger.Verify(m =>
                m.LogVerbose(It.Is<string>(actualLogMessage => actualLogMessage == expectedLogMessage)));
        }

        [Fact]
        public async Task PostAsync_Throws_HttpRequestException_On_Non_Success_Response()
        {
            // Arrange
            using var httpResponse = new HttpResponseMessage(HttpStatusCode.InternalServerError)
            {
                Content = new StringContent(EXPECTED_RESPONSE_CONTENT)
            };
            var handlerMock = new Mock<HttpMessageHandler>();
            handlerMock
                .Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.Is<HttpRequestMessage>(x =>
                        x.Content.ReadAsStringAsync().Result == EXPECTED_JSON_REQUEST_BODY),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(httpResponse);

            var loggerMock = TestHelpers.CreateMock<OctoLogger>();

            // Act
            // Assert
            await FluentActions
                .Invoking(() =>
                {
                    using var httpClient = new HttpClient(handlerMock.Object);
                    var githubClient = new GithubClient(loggerMock.Object, httpClient, null, _retryPolicy, _dateTimeProvider.Object, PERSONAL_ACCESS_TOKEN);
                    return githubClient.PostAsync("http://example.com", _rawRequestBody);
                })
                .Should()
                .ThrowExactlyAsync<HttpRequestException>()
                .WithMessage($"GitHub API error: {EXPECTED_RESPONSE_CONTENT}");
        }

        [Fact]
        public async Task PostGraphQLAsync_Returns_JObject_Response()
        {
            // Arrange
            using var httpClient = new HttpClient(MockHttpHandlerForGraphQLPost().Object);
            var githubClient = new GithubClient(_mockOctoLogger.Object, httpClient, null, _retryPolicy, _dateTimeProvider.Object, PERSONAL_ACCESS_TOKEN);

            // Act
            var response = await githubClient.PostGraphQLAsync("http://example.com", _rawRequestBody);

            // Assert
            response.ToJson().Should().Be(EXPECTED_JSON_REQUEST_BODY);
        }

        [Fact]
        public async Task PostGraphQLAsync_Encodes_The_Url()
        {
            // Arrange
            var handlerMock = MockHttpHandlerForGraphQLPost();
            using var httpClient = new HttpClient(handlerMock.Object);
            var githubClient = new GithubClient(_mockOctoLogger.Object, httpClient, null, _retryPolicy, _dateTimeProvider.Object, PERSONAL_ACCESS_TOKEN);

            const string actualUrl = "http://example.com/param with space";
            const string expectedUrl = "http://example.com/param%20with%20space";

            // Act
            await githubClient.PostGraphQLAsync(actualUrl, _rawRequestBody);

            // Assert
            handlerMock.Protected().Verify(
                "SendAsync",
                Times.Once(),
                ItExpr.Is<HttpRequestMessage>(msg => msg.RequestUri.AbsoluteUri == expectedUrl),
                ItExpr.IsAny<CancellationToken>());
        }

        [Fact]
        public async Task PostGraphQLAsync_Logs_The_Url()
        {
            // Arrange
            using var httpClient = new HttpClient(MockHttpHandlerForGraphQLPost().Object);
            var githubClient = new GithubClient(_mockOctoLogger.Object, httpClient, null, _retryPolicy, _dateTimeProvider.Object, PERSONAL_ACCESS_TOKEN);

            const string url = "http://example.com";
            var expectedLogMessage = $"HTTP POST: {url}";

            // Act
            await githubClient.PostGraphQLAsync(url, _rawRequestBody);

            // Assert
            _mockOctoLogger.Verify(m =>
                m.LogVerbose(It.Is<string>(actualLogMessage => actualLogMessage == expectedLogMessage)));
        }

        [Fact]
        public async Task PostGraphQLAsync_Logs_The_GitHub_Request_Id_Header_Value()
        {
            // Arrange
            using var httpClient = new HttpClient(MockHttpHandlerForGraphQLPost().Object);
            var githubClient = new GithubClient(_mockOctoLogger.Object, httpClient, null, _retryPolicy, _dateTimeProvider.Object, PERSONAL_ACCESS_TOKEN);
            _graphqlHttpResponse.Headers.Add("X-GitHub-Request-Id", GITHUB_REQUEST_ID);

            const string expectedLogMessage = $"GITHUB REQUEST ID: {GITHUB_REQUEST_ID}";

            // Act
            await githubClient.PostGraphQLAsync("http://example.com", _rawRequestBody);

            // Assert
            _mockOctoLogger.Verify(m =>
                m.LogVerbose(It.Is<string>(actualLogMessage => actualLogMessage == expectedLogMessage)));
        }

        [Fact]
        public async Task PostGraphQLAsync_Logs_The_Request_Body()
        {
            // Arrange
            using var httpClient = new HttpClient(MockHttpHandlerForGraphQLPost().Object);
            var githubClient = new GithubClient(_mockOctoLogger.Object, httpClient, null, _retryPolicy, _dateTimeProvider.Object, PERSONAL_ACCESS_TOKEN);

            var expectedLogMessage = $"HTTP BODY: {EXPECTED_JSON_REQUEST_BODY}";

            // Act
            await githubClient.PostGraphQLAsync("http://example.com", _rawRequestBody);

            // Assert
            _mockOctoLogger.Verify(m =>
                m.LogVerbose(It.Is<string>(actualLogMessage => actualLogMessage == expectedLogMessage)));
        }

        [Fact]
        public async Task PostGraphQLAsync_Logs_The_Response_Status_Code_And_Content()
        {
            // Arrange
            using var httpClient = new HttpClient(MockHttpHandlerForGraphQLPost().Object);
            var githubClient = new GithubClient(_mockOctoLogger.Object, httpClient, null, _retryPolicy, _dateTimeProvider.Object, PERSONAL_ACCESS_TOKEN);

            var expectedLogMessage = $"RESPONSE ({_graphqlHttpResponse.StatusCode}): {EXPECTED_GRAPHQL_JSON_RESPONSE_BODY}";

            // Act
            await githubClient.PostGraphQLAsync("http://example.com", _rawRequestBody);

            // Assert
            _mockOctoLogger.Verify(m =>
                m.LogVerbose(It.Is<string>(actualLogMessage => actualLogMessage == expectedLogMessage)));
        }

        [Fact]
        public async Task PostGraphQLAsync_Throws_OctoshiftCliException_On_Non_Success_Response()
        {
            // Arrange
            using var httpResponse = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(EXPECTED_GRAPHQL_JSON_ERROR_RESPONSE_BODY)
            };

            var handlerMock = MockHttpHandlerForGraphQLPost(httpResponse);
            var loggerMock = TestHelpers.CreateMock<OctoLogger>();

            // Act
            // Assert
            await FluentActions
                .Invoking(() =>
                {
                    using var httpClient = new HttpClient(handlerMock.Object);
                    var githubClient = new GithubClient(loggerMock.Object, httpClient, null, _retryPolicy, _dateTimeProvider.Object, PERSONAL_ACCESS_TOKEN);
                    return githubClient.PostGraphQLAsync("http://example.com", _rawRequestBody);
                })
                .Should()
                .ThrowExactlyAsync<OctoshiftCLI.OctoshiftCliException>()
                .WithMessage($"Resource protected by organization SAML enforcement. You must grant your Personal Access token access to this organization.");
        }

        [Fact]
        public async Task PutAsync_Returns_String_Response()
        {
            // Arrange
            using var httpClient = new HttpClient(MockHttpHandlerForPut().Object);
            var githubClient = new GithubClient(_mockOctoLogger.Object, httpClient, null, _retryPolicy, _dateTimeProvider.Object, PERSONAL_ACCESS_TOKEN);

            // Act
            var actualContent = await githubClient.PutAsync("http://example.com", _rawRequestBody);

            // Assert
            actualContent.Should().Be(EXPECTED_RESPONSE_CONTENT);
        }

        [Fact]
        public async Task PutAsync_Applies_Retry_Delay()
        {
            // Arrange
            var now = DateTimeOffset.Now.ToUnixTimeSeconds();
            var retryAt = now + 1;

            _dateTimeProvider.Setup(m => m.CurrentUnixTimeSeconds()).Returns(now);

            using var firstHttpResponse = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("FIRST_RESPONSE")
            };

            using var secondHttpResponse = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("API RATE LIMIT EXCEEDED blah blah blah")
            };

            secondHttpResponse.Headers.Add("X-RateLimit-Reset", retryAt.ToString());
            secondHttpResponse.Headers.Add("X-RateLimit-Remaining", "0");

            using var thirdResponse = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("THIRD_RESPONSE")
            };

            var handlerMock = new Mock<HttpMessageHandler>();
            handlerMock
                .Protected()
                .SetupSequence<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.Is<HttpRequestMessage>(req => req.Method == HttpMethod.Put),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(firstHttpResponse)
                .ReturnsAsync(secondHttpResponse)
                .ReturnsAsync(thirdResponse);

            using var httpClient = new HttpClient(handlerMock.Object);
            var githubClient = new GithubClient(_mockOctoLogger.Object, httpClient, null, _retryPolicy, _dateTimeProvider.Object, PERSONAL_ACCESS_TOKEN);

            // Act
            await githubClient.PutAsync("http://example.com", "hello"); // normal call
            await githubClient.PutAsync("http://example.com", "hello"); // call with retry delay
            await githubClient.PutAsync("http://example.com", "hello"); // normal call

            // Assert
            _mockOctoLogger.Verify(m => m.LogWarning("GitHub rate limit exceeded. Waiting 1 seconds before continuing"), Times.Once);
        }

        [Fact]
        public async Task PutAsync_Encodes_The_Url()
        {
            // Arrange
            var handlerMock = MockHttpHandlerForPut();
            using var httpClient = new HttpClient(handlerMock.Object);
            var githubClient = new GithubClient(_mockOctoLogger.Object, httpClient, null, _retryPolicy, _dateTimeProvider.Object, PERSONAL_ACCESS_TOKEN);

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
            var githubClient = new GithubClient(_mockOctoLogger.Object, httpClient, null, _retryPolicy, _dateTimeProvider.Object, PERSONAL_ACCESS_TOKEN);

            const string url = "http://example.com";
            var expectedLogMessage = $"HTTP PUT: {url}";

            // Act
            await githubClient.PutAsync(url, _rawRequestBody);

            // Assert
            _mockOctoLogger.Verify(m =>
                m.LogVerbose(It.Is<string>(actualLogMessage => actualLogMessage == expectedLogMessage)));
        }

        [Fact]
        public async Task PutAsync_Logs_The_GitHub_Request_Id_Header_Value()
        {
            // Arrange
            using var httpClient = new HttpClient(MockHttpHandlerForPut().Object);
            var githubClient = new GithubClient(_mockOctoLogger.Object, httpClient, null, _retryPolicy, _dateTimeProvider.Object, PERSONAL_ACCESS_TOKEN);

            const string githubRequestId = "123-456-789";
            _defaultHttpResponse.Headers.Add("X-GitHub-Request-Id", githubRequestId);

            const string expectedLogMessage = $"GITHUB REQUEST ID: {githubRequestId}";

            // Act
            await githubClient.PutAsync("http://example.com", _rawRequestBody);

            // Assert
            _mockOctoLogger.Verify(m =>
                m.LogVerbose(It.Is<string>(actualLogMessage => actualLogMessage == expectedLogMessage)));
        }

        [Fact]
        public async Task PutAsync_Logs_The_Request_Body()
        {
            // Arrange
            using var httpClient = new HttpClient(MockHttpHandlerForPut().Object);
            var githubClient = new GithubClient(_mockOctoLogger.Object, httpClient, null, _retryPolicy, _dateTimeProvider.Object, PERSONAL_ACCESS_TOKEN);

            var expectedLogMessage = $"HTTP BODY: {EXPECTED_JSON_REQUEST_BODY}";

            // Act
            await githubClient.PutAsync("http://example.com", _rawRequestBody);

            // Assert
            _mockOctoLogger.Verify(m =>
                m.LogVerbose(It.Is<string>(actualLogMessage => actualLogMessage == expectedLogMessage)));
        }

        [Fact]
        public async Task PutAsync_Logs_The_Response_Status_Code_And_Content()
        {
            // Arrange
            using var httpClient = new HttpClient(MockHttpHandlerForPut().Object);
            var githubClient = new GithubClient(_mockOctoLogger.Object, httpClient, null, _retryPolicy, _dateTimeProvider.Object, PERSONAL_ACCESS_TOKEN);

            var expectedLogMessage = $"RESPONSE ({_defaultHttpResponse.StatusCode}): {EXPECTED_RESPONSE_CONTENT}";

            // Act
            await githubClient.PutAsync("http://example.com", _rawRequestBody);

            // Assert
            _mockOctoLogger.Verify(m =>
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

            var loggerMock = TestHelpers.CreateMock<OctoLogger>();

            // Act
            // Assert
            await FluentActions
                .Invoking(() =>
                {
                    using var httpClient = new HttpClient(handlerMock.Object);
                    var githubClient = new GithubClient(loggerMock.Object, httpClient, null, _retryPolicy, _dateTimeProvider.Object, PERSONAL_ACCESS_TOKEN);
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
            var githubClient = new GithubClient(_mockOctoLogger.Object, httpClient, null, _retryPolicy, _dateTimeProvider.Object, PERSONAL_ACCESS_TOKEN);

            // Act
            var actualContent = await githubClient.PatchAsync("http://example.com", _rawRequestBody);

            // Assert
            actualContent.Should().Be(EXPECTED_RESPONSE_CONTENT);
        }

        [Fact]
        public async Task PatchAsync_Applies_Retry_Delay()
        {
            // Arrange
            var now = DateTimeOffset.Now.ToUnixTimeSeconds();
            var retryAt = now + 1;

            _dateTimeProvider.Setup(m => m.CurrentUnixTimeSeconds()).Returns(now);

            using var firstHttpResponse = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("FIRST_RESPONSE")
            };

            using var secondHttpResponse = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("API RATE LIMIT EXCEEDED blah blah blah")
            };

            secondHttpResponse.Headers.Add("X-RateLimit-Reset", retryAt.ToString());
            secondHttpResponse.Headers.Add("X-RateLimit-Remaining", "0");

            using var thirdResponse = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("THIRD_RESPONSE")
            };

            var handlerMock = new Mock<HttpMessageHandler>();
            handlerMock
                .Protected()
                .SetupSequence<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.Is<HttpRequestMessage>(req => req.Method == HttpMethod.Patch),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(firstHttpResponse)
                .ReturnsAsync(secondHttpResponse)
                .ReturnsAsync(thirdResponse);

            using var httpClient = new HttpClient(handlerMock.Object);
            var githubClient = new GithubClient(_mockOctoLogger.Object, httpClient, null, _retryPolicy, _dateTimeProvider.Object, PERSONAL_ACCESS_TOKEN);

            // Act
            await githubClient.PatchAsync("http://example.com", "hello"); // normal call
            await githubClient.PatchAsync("http://example.com", "hello"); // call with retry delay
            await githubClient.PatchAsync("http://example.com", "hello"); // normal call

            // Assert
            _mockOctoLogger.Verify(m => m.LogWarning("GitHub rate limit exceeded. Waiting 1 seconds before continuing"), Times.Once);
        }

        [Fact]
        public async Task PatchAsync_Encodes_The_Url()
        {
            // Arrange
            var handlerMock = MockHttpHandlerForPatch();
            using var httpClient = new HttpClient(handlerMock.Object);
            var githubClient = new GithubClient(_mockOctoLogger.Object, httpClient, null, _retryPolicy, _dateTimeProvider.Object, PERSONAL_ACCESS_TOKEN);

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
            var githubClient = new GithubClient(_mockOctoLogger.Object, httpClient, null, _retryPolicy, _dateTimeProvider.Object, PERSONAL_ACCESS_TOKEN);

            const string url = "http://example.com";
            var expectedLogMessage = $"HTTP PATCH: {url}";

            // Act
            await githubClient.PatchAsync(url, _rawRequestBody);

            // Assert
            _mockOctoLogger.Verify(m =>
                m.LogVerbose(It.Is<string>(actualLogMessage => actualLogMessage == expectedLogMessage)));
        }

        [Fact]
        public async Task PatchAsync_Logs_The_GitHub_Request_Id_Header_Value()
        {
            // Arrange
            using var httpClient = new HttpClient(MockHttpHandlerForPatch().Object);
            var githubClient = new GithubClient(_mockOctoLogger.Object, httpClient, null, _retryPolicy, _dateTimeProvider.Object, PERSONAL_ACCESS_TOKEN);

            const string githubRequestId = "123-456-789";
            _defaultHttpResponse.Headers.Add("X-GitHub-Request-Id", githubRequestId);

            const string expectedLogMessage = $"GITHUB REQUEST ID: {githubRequestId}";

            // Act
            await githubClient.PatchAsync("http://example.com", _rawRequestBody);

            // Assert
            _mockOctoLogger.Verify(m =>
                m.LogVerbose(It.Is<string>(actualLogMessage => actualLogMessage == expectedLogMessage)));
        }

        [Fact]
        public async Task PatchAsync_Logs_The_Request_Body()
        {
            // Arrange
            using var httpClient = new HttpClient(MockHttpHandlerForPatch().Object);
            var githubClient = new GithubClient(_mockOctoLogger.Object, httpClient, null, _retryPolicy, _dateTimeProvider.Object, PERSONAL_ACCESS_TOKEN);

            var expectedLogMessage = $"HTTP BODY: {EXPECTED_JSON_REQUEST_BODY}";

            // Act
            await githubClient.PatchAsync("http://example.com", _rawRequestBody);

            // Assert
            _mockOctoLogger.Verify(m =>
                m.LogVerbose(It.Is<string>(actualLogMessage => actualLogMessage == expectedLogMessage)));
        }

        [Fact]
        public async Task PatchAsync_Logs_The_Response_Status_Code_And_Content()
        {
            // Arrange
            using var httpClient = new HttpClient(MockHttpHandlerForPatch().Object);
            var githubClient = new GithubClient(_mockOctoLogger.Object, httpClient, null, _retryPolicy, _dateTimeProvider.Object, PERSONAL_ACCESS_TOKEN);

            var expectedLogMessage = $"RESPONSE ({_defaultHttpResponse.StatusCode}): {EXPECTED_RESPONSE_CONTENT}";

            // Act
            await githubClient.PatchAsync("http://example.com", _rawRequestBody);

            // Assert
            _mockOctoLogger.Verify(m =>
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

            var loggerMock = TestHelpers.CreateMock<OctoLogger>();

            // Act
            // Assert
            await FluentActions
                .Invoking(() =>
                {
                    using var httpClient = new HttpClient(handlerMock.Object);
                    var githubClient = new GithubClient(loggerMock.Object, httpClient, null, _retryPolicy, _dateTimeProvider.Object, PERSONAL_ACCESS_TOKEN);
                    return githubClient.PatchAsync("http://example.com", _rawRequestBody);
                })
                .Should()
                .ThrowExactlyAsync<HttpRequestException>();
        }

        [Fact]
        public async Task DeleteAsync_Applies_Retry_Delay()
        {
            // Arrange
            var now = DateTimeOffset.Now.ToUnixTimeSeconds();
            var retryAt = now + 1;

            _dateTimeProvider.Setup(m => m.CurrentUnixTimeSeconds()).Returns(now);

            using var firstHttpResponse = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("FIRST_RESPONSE")
            };

            using var secondHttpResponse = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("API RATE LIMIT EXCEEDED blah blah blah")
            };

            secondHttpResponse.Headers.Add("X-RateLimit-Reset", retryAt.ToString());
            secondHttpResponse.Headers.Add("X-RateLimit-Remaining", "0");

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
            var githubClient = new GithubClient(_mockOctoLogger.Object, httpClient, null, _retryPolicy, _dateTimeProvider.Object, PERSONAL_ACCESS_TOKEN);

            // Act
            await githubClient.DeleteAsync("http://example.com"); // normal call
            await githubClient.DeleteAsync("http://example.com"); // call with retry delay
            await githubClient.DeleteAsync("http://example.com"); // normal call

            // Assert
            _mockOctoLogger.Verify(m => m.LogWarning("GitHub rate limit exceeded. Waiting 1 seconds before continuing"), Times.Once);
        }

        [Fact]
        public async Task DeleteAsync_Returns_String_Response()
        {
            // Arrange
            using var httpClient = new HttpClient(MockHttpHandlerForDelete().Object);
            var githubClient = new GithubClient(_mockOctoLogger.Object, httpClient, null, _retryPolicy, _dateTimeProvider.Object, PERSONAL_ACCESS_TOKEN);

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
            var githubClient = new GithubClient(_mockOctoLogger.Object, httpClient, null, _retryPolicy, _dateTimeProvider.Object, PERSONAL_ACCESS_TOKEN);

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
            var githubClient = new GithubClient(_mockOctoLogger.Object, httpClient, null, _retryPolicy, _dateTimeProvider.Object, PERSONAL_ACCESS_TOKEN);

            const string url = "http://example.com";
            var expectedLogMessage = $"HTTP DELETE: {url}";

            // Act
            await githubClient.DeleteAsync(url);

            // Assert
            _mockOctoLogger.Verify(m =>
                m.LogVerbose(It.Is<string>(actualLogMessage => actualLogMessage == expectedLogMessage)));
        }

        [Fact]
        public async Task DeleteAsync_Logs_The_GitHub_Request_Id_Header_Value()
        {
            // Arrange
            using var httpClient = new HttpClient(MockHttpHandlerForDelete().Object);
            var githubClient = new GithubClient(_mockOctoLogger.Object, httpClient, null, _retryPolicy, _dateTimeProvider.Object, PERSONAL_ACCESS_TOKEN);

            const string githubRequestId = "123-456-789";
            _defaultHttpResponse.Headers.Add("X-GitHub-Request-Id", githubRequestId);

            const string expectedLogMessage = $"GITHUB REQUEST ID: {githubRequestId}";

            // Act
            await githubClient.DeleteAsync("http://example.com");

            // Assert
            _mockOctoLogger.Verify(m =>
                m.LogVerbose(It.Is<string>(actualLogMessage => actualLogMessage == expectedLogMessage)));
        }

        [Fact]
        public async Task GetNonSuccessAsync_Is_Unsuccessful()
        {
            // Arrange
            using var httpResponse = new HttpResponseMessage(HttpStatusCode.NotFound);
            var handlerMock = new Mock<HttpMessageHandler>();
            handlerMock
                .Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(httpResponse);

            // Assert
            await FluentActions
                .Invoking(() =>
                {
                    using var httpClient = new HttpClient(handlerMock.Object);
                    var githubClient = new GithubClient(_mockOctoLogger.Object, httpClient, null, _retryPolicy, _dateTimeProvider.Object, PERSONAL_ACCESS_TOKEN);
                    return githubClient.GetNonSuccessAsync("http://example.com", HttpStatusCode.Moved);
                })
                .Should()
                .ThrowExactlyAsync<HttpRequestException>();
        }

        [Fact]
        public async Task GetNonSuccessAsync_With_302_Is_Successful()
        {
            // Arrange
            using var httpResponse = new HttpResponseMessage(HttpStatusCode.Moved);
            var handlerMock = new Mock<HttpMessageHandler>();
            handlerMock
                .Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(httpResponse);

            using var httpClient = new HttpClient(handlerMock.Object);
            var githubClient = new GithubClient(_mockOctoLogger.Object, httpClient, null, _retryPolicy, _dateTimeProvider.Object, PERSONAL_ACCESS_TOKEN);

            // Act
            await githubClient.GetNonSuccessAsync("http://example.com", HttpStatusCode.Moved);

            // Assert
            // If it doesn't crash, we are good
        }

        [Fact]
        public async Task GetNonSuccessAsync_Logs_The_GitHub_Request_Id_Header_Value()
        {
            // Arrange
            using var httpResponse = new HttpResponseMessage(HttpStatusCode.Moved);
            const string githubRequestId = "123-456-789";
            httpResponse.Headers.Add("X-GitHub-Request-Id", githubRequestId);
            var handlerMock = MockHttpHandler(req => req.Method == HttpMethod.Get, httpResponse);

            using var httpClient = new HttpClient(handlerMock.Object);
            var githubClient = new GithubClient(_mockOctoLogger.Object, httpClient, null, _retryPolicy, _dateTimeProvider.Object, PERSONAL_ACCESS_TOKEN);

            // Act
            await githubClient.GetNonSuccessAsync("http://example.com", HttpStatusCode.Moved);

            // Assert
            _mockOctoLogger.Verify(m => m.LogVerbose($"GITHUB REQUEST ID: {githubRequestId}"));
        }

        [Fact]
        public async Task DeleteAsync_Logs_The_Response_Status_Code_And_Content()
        {
            // Arrange
            using var httpClient = new HttpClient(MockHttpHandlerForDelete().Object);
            var githubClient = new GithubClient(_mockOctoLogger.Object, httpClient, null, _retryPolicy, _dateTimeProvider.Object, PERSONAL_ACCESS_TOKEN);

            var expectedLogMessage = $"RESPONSE ({HttpStatusCode.OK}): {EXPECTED_RESPONSE_CONTENT}";

            // Act
            await githubClient.DeleteAsync("http://example.com");

            // Assert
            _mockOctoLogger.Verify(m =>
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

            var loggerMock = TestHelpers.CreateMock<OctoLogger>();

            // Act
            // Assert
            await FluentActions
                .Invoking(() =>
                {
                    using var httpClient = new HttpClient(handlerMock.Object);
                    var githubClient = new GithubClient(loggerMock.Object, httpClient, null, _retryPolicy, _dateTimeProvider.Object, PERSONAL_ACCESS_TOKEN);
                    return githubClient.DeleteAsync("http://example.com");
                })
                .Should()
                .ThrowExactlyAsync<HttpRequestException>();
        }

        [Fact]
        public async Task GetAllAsync_Should_Get_All_Pages()
        {
            // Arrange
            const string url = "https://api.github.com/search/code?q=addClass+user%3Amozilla";

            const string firstItem = "first";
            const string secondItem = "second";
            using var firstResponse = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent($"[\"{firstItem}\", \"{secondItem}\"]"),
            };
            firstResponse.Headers.Add("Link", new[]
            {
                $"<{url}&page=2>; rel=\"next\", " +
                $"<{url}&page=4>; rel=\"last\""
            });

            const string thirdItem = "third";
            const string fourthItem = "fourth";
            using var secondResponse = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent($"[\"{thirdItem}\", \"{fourthItem}\"]")
            };
            secondResponse.Headers.Add("Link", new[]
            {
                $"<{url}&page=1>; rel=\"prev\", " +
                $"<{url}&page=3>; rel=\"next\", " +
                $"<{url}&page=3>; rel=\"last\", " +
                $"<{url}&page=1>; rel=\"first\""
            });

            const string fifthItem = "fifth";
            using var thirdResponse = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent($"[\"{fifthItem}\"]")
            };

            var handlerMock = new Mock<HttpMessageHandler>();
            handlerMock // first request
                .Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.Is<HttpRequestMessage>(req => req.Method == HttpMethod.Get &&
                                                         req.RequestUri.ToString() == url),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(firstResponse);
            handlerMock // second request
                .Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.Is<HttpRequestMessage>(req => req.Method == HttpMethod.Get &&
                                                         req.RequestUri.ToString() == $"{url}&page=2"),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(secondResponse);
            handlerMock // third request
                .Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.Is<HttpRequestMessage>(req => req.Method == HttpMethod.Get &&
                                                         req.RequestUri.ToString() == $"{url}&page=3"),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(thirdResponse);

            using var httpClient = new HttpClient(handlerMock.Object);
            var githubClient = new GithubClient(_mockOctoLogger.Object, httpClient, null, _retryPolicy, _dateTimeProvider.Object, PERSONAL_ACCESS_TOKEN);

            // Act
            var results = new List<JToken>();
            await foreach (var result in githubClient.GetAllAsync(url))
            {
                results.Add(result);
            }

            // Assert
            results.Should().HaveCount(5);
            results[0].Value<string>().Should().Be(firstItem);
            results[1].Value<string>().Should().Be(secondItem);
            results[2].Value<string>().Should().Be(thirdItem);
            results[3].Value<string>().Should().Be(fourthItem);
            results[4].Value<string>().Should().Be(fifthItem);
        }

        [Fact]
        public async Task GetAllAsync_Logs_The_Url_Per_Each_Page_Request()
        {
            // Arrange
            const string url = "https://example.com/resource";

            using var firstResponse = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("[\"first\"]"),
            };
            firstResponse.Headers.Add("Link", new[]
            {
                $"<{url}&page=2>; rel=\"next\", " +
                $"<{url}&page=2>; rel=\"last\""
            });

            using var secondResponse = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("[\"second\"]"),
            };

            var handlerMock = new Mock<HttpMessageHandler>();
            handlerMock // first request
                .Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.Is<HttpRequestMessage>(req => req.Method == HttpMethod.Get &&
                                                         req.RequestUri.ToString() == url),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(firstResponse);

            handlerMock // second request
                .Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.Is<HttpRequestMessage>(req => req.Method == HttpMethod.Get &&
                                                         req.RequestUri.ToString() == $"{url}&page=2"),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(secondResponse);

            using var httpClient = new HttpClient(handlerMock.Object);
            var githubClient = new GithubClient(_mockOctoLogger.Object, httpClient, null, _retryPolicy, _dateTimeProvider.Object, PERSONAL_ACCESS_TOKEN);

            // Act
            await foreach (var _ in githubClient.GetAllAsync(url)) { }

            // Assert
            _mockOctoLogger.Verify(m => m.LogVerbose(It.Is<string>(actual => actual == $"HTTP GET: {url}")));
            _mockOctoLogger.Verify(m => m.LogVerbose(It.Is<string>(actual => actual == $"HTTP GET: {url}&page=2")));
        }

        [Fact]
        public async Task GetAllAsync_Logs_The_GitHub_Request_Id_Header_Value_Per_Each_Page_Request()
        {
            // Arrange
            const string url = "https://example.com/resource";

            using var firstResponse = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("[\"first\"]"),
            };
            firstResponse.Headers.Add("Link", new[]
            {
                $"<{url}&page=2>; rel=\"next\", " +
                $"<{url}&page=2>; rel=\"last\""
            });
            const string firstGithubRequestId = "123-456";
            firstResponse.Headers.Add("X-GitHub-Request-Id", firstGithubRequestId);

            using var secondResponse = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("[\"second\"]"),
            };
            const string secondGithubRequestId = "456-789";
            secondResponse.Headers.Add("X-GitHub-Request-Id", secondGithubRequestId);

            var handlerMock = new Mock<HttpMessageHandler>();

            // first request
            MockHttpHandler(
                req => req.Method == HttpMethod.Get && req.RequestUri.ToString() == url,
                firstResponse,
                handlerMock);

            // second request
            MockHttpHandler(
                req => req.Method == HttpMethod.Get && req.RequestUri.ToString() == $"{url}&page=2",
                secondResponse,
                handlerMock);

            using var httpClient = new HttpClient(handlerMock.Object);
            var githubClient = new GithubClient(_mockOctoLogger.Object, httpClient, null, _retryPolicy, _dateTimeProvider.Object, PERSONAL_ACCESS_TOKEN);

            // Act
            await foreach (var _ in githubClient.GetAllAsync(url)) { }

            // Assert
            _mockOctoLogger.Verify(m => m.LogVerbose(It.Is<string>(actual => actual == $"GITHUB REQUEST ID: {firstGithubRequestId}")));
            _mockOctoLogger.Verify(m => m.LogVerbose(It.Is<string>(actual => actual == $"GITHUB REQUEST ID: {secondGithubRequestId}")));
        }

        [Fact]
        public async Task GetAllAsync_Logs_The_Response_Per_Each_Page_Request()
        {
            // Arrange
            const string url = "https://example.com/resource";

            var firstResponseContent = "[\"firs\"]";
            using var firstResponse = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(firstResponseContent),
            };
            firstResponse.Headers.Add("Link", new[]
            {
                $"<{url}&page=2>; rel=\"next\", " +
                $"<{url}&page=2>; rel=\"last\""
            });

            var secondResponseContent = "[\"second\"]";
            using var secondResponse = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(secondResponseContent),
            };

            var handlerMock = new Mock<HttpMessageHandler>();
            handlerMock // first request
                .Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.Is<HttpRequestMessage>(req => req.Method == HttpMethod.Get &&
                                                         req.RequestUri.ToString() == url),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(firstResponse);

            handlerMock // second request
                .Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.Is<HttpRequestMessage>(req => req.Method == HttpMethod.Get &&
                                                         req.RequestUri.ToString() == $"{url}&page=2"),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(secondResponse);

            using var httpClient = new HttpClient(handlerMock.Object);
            var githubClient = new GithubClient(_mockOctoLogger.Object, httpClient, null, _retryPolicy, _dateTimeProvider.Object, PERSONAL_ACCESS_TOKEN);

            // Act
            await foreach (var _ in githubClient.GetAllAsync(url)) { }

            // Assert
            _mockOctoLogger.Verify(m =>
                m.LogVerbose(
                    It.Is<string>(actual =>
                        actual == $"RESPONSE ({HttpStatusCode.OK}): {firstResponseContent}")));
            _mockOctoLogger.Verify(m =>
                m.LogVerbose(It.Is<string>(actual =>
                    actual == $"RESPONSE ({HttpStatusCode.OK}): {secondResponseContent}")));
        }

        [Fact]
        public async Task GetAllAsync_Throws_HttpRequestException_On_Non_Success_Response()
        {
            // Arrange
            const string url = "https://example.com/resource";

            using var firstResponse = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("[\"first\"]"),
            };
            firstResponse.Headers.Add("Link", new[]
            {
                $"<{url}&page=2>; rel=\"next\", " +
                $"<{url}&page=2>; rel=\"last\""
            });

            using var failureResponse = new HttpResponseMessage(HttpStatusCode.InternalServerError);

            var handlerMock = new Mock<HttpMessageHandler>();
            handlerMock // first request
                .Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.Is<HttpRequestMessage>(req => req.Method == HttpMethod.Get &&
                                                         req.RequestUri.ToString() == url),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(firstResponse);

            handlerMock // second request
                .Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.Is<HttpRequestMessage>(req => req.Method == HttpMethod.Get &&
                                                         req.RequestUri.ToString() == $"{url}&page=2"),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(failureResponse);

            using var httpClient = new HttpClient(handlerMock.Object);
            var githubClient = new GithubClient(_mockOctoLogger.Object, httpClient, null, _retryPolicy, _dateTimeProvider.Object, PERSONAL_ACCESS_TOKEN);

            // Act, Assert
            await FluentActions
                .Invoking(async () =>
                {
                    await foreach (var _ in githubClient.GetAllAsync(url)) { }
                })
                .Should()
                .ThrowExactlyAsync<HttpRequestException>();
        }

        [Fact]
        public async Task PostGraphQLWithPaginationAsync_Should_Return_All_Pages()
        {
            // Arrange
            const string url = "https://example.com/graphql";
            const string orgId = "ORG_ID";
            const string query = @"
query($id: ID!, $first: Int, $after: String) {
    node(id: $id) { 
        ... on Organization { 
            login, 
            repositoryMigrations(first: $first, after: $after) {
                pageInfo {
                    endCursor
                    hasNextPage
                }
                totalCount
                nodes {
                    id
                    sourceUrl
                    migrationSource { name }
                    state
                    failureReason
                    createdAt
                }
            }
        }
    }
}";

            // first request/response
            var firstRequestVariables = new { id = orgId, first = 2, after = (string)null };
            var firstRequestBody = new { query, variables = firstRequestVariables };
            var firstResponseEndCursor = Guid.NewGuid().ToString();
            var repoMigration1 = CreateRepositoryMigration();
            var repoMigration2 = CreateRepositoryMigration();
            var firstResponseContent = new
            {
                data = new
                {
                    node = new
                    {
                        login = "github",
                        repositoryMigrations = new
                        {
                            pageInfo = new { endCursor = firstResponseEndCursor, hasNextPage = true },
                            totalCount = 5,
                            nodes = new[] { repoMigration1, repoMigration2 }
                        }
                    }
                }
            };
            using var firstResponse = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(firstResponseContent.ToJson())
            };

            // second request/response
            var secondRequestVariables = new { id = orgId, first = 2, after = firstResponseEndCursor };
            var secondResponseEndCursor = Guid.NewGuid().ToString();
            var repoMigration3 = CreateRepositoryMigration();
            var repoMigration4 = CreateRepositoryMigration();
            var secondResponseContent = new
            {
                data = new
                {
                    node = new
                    {
                        login = "github",
                        repositoryMigrations = new
                        {
                            pageInfo = new { endCursor = secondResponseEndCursor, hasNextPage = true },
                            totalCount = 5,
                            nodes = new[] { repoMigration3, repoMigration4 }
                        }
                    }
                }
            };
            using var secondResponse = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(secondResponseContent.ToJson())
            };
            var secondRequestBody = new { query, variables = secondRequestVariables };

            // third request/response
            var thirdRequestVariables = new { id = orgId, first = 2, after = secondResponseEndCursor };
            var repoMigration5 = CreateRepositoryMigration();
            var thirdResponseContent = new
            {
                data = new
                {
                    node = new
                    {
                        login = "github",
                        repositoryMigrations = new
                        {
                            pageInfo = new { endCursor = Guid.NewGuid().ToString(), hasNextPage = false },
                            totalCount = 5,
                            nodes = new[] { repoMigration5 }
                        }
                    }
                }
            };
            using var thirdResponse = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(thirdResponseContent.ToJson())
            };
            var thirdRequestBody = new { query, variables = thirdRequestVariables };

            var handlerMock = MockHttpHandler(
                req => req.Method == HttpMethod.Post && req.RequestUri.ToString() == url && req.Content.ReadAsStringAsync().Result == firstRequestBody.ToJson(),
                firstResponse); // first request
            MockHttpHandler(
                req => req.Method == HttpMethod.Post && req.RequestUri.ToString() == url && req.Content.ReadAsStringAsync().Result == secondRequestBody.ToJson(),
                secondResponse,
                handlerMock); // second request
            MockHttpHandler(
                req => req.Method == HttpMethod.Post && req.RequestUri.ToString() == url && req.Content.ReadAsStringAsync().Result == thirdRequestBody.ToJson(),
                thirdResponse,
                handlerMock); // third request

            using var httpClient = new HttpClient(handlerMock.Object);
            var githubClient = new GithubClient(_mockOctoLogger.Object, httpClient, null, _retryPolicy, _dateTimeProvider.Object, PERSONAL_ACCESS_TOKEN);

            // Act
            var result = await githubClient.PostGraphQLWithPaginationAsync(
                    url,
                    firstRequestBody,
                    obj => (JArray)obj["data"]["node"]["repositoryMigrations"]["nodes"],
                    obj => (JObject)obj["data"]["node"]["repositoryMigrations"]["pageInfo"],
                    2)
                .ToListAsync();


            // Assert
            result.Should().HaveCount(5);
            result[0].ToJson().Should().Be(repoMigration1.ToJson());
            result[1].ToJson().Should().Be(repoMigration2.ToJson());
            result[2].ToJson().Should().Be(repoMigration3.ToJson());
            result[3].ToJson().Should().Be(repoMigration4.ToJson());
            result[4].ToJson().Should().Be(repoMigration5.ToJson());
        }

        [Fact]
        public async Task PostGraphQLWithPaginationAsync_Should_Create_Query_Variables_If_Missing()
        {
            // Arrange
            const string url = "https://example.com/graphql";
            const string query = @"
query($id: ID!, $first: Int, $after: String) {
    node(id: $id) { 
        ... on Organization { 
            login, 
            repositoryMigrations(first: $first, after: $after) {
                pageInfo {
                    endCursor
                    hasNextPage
                }
                totalCount
                nodes {
                    id
                    sourceUrl
                    migrationSource { name }
                    state
                    failureReason
                    createdAt
                }
            }
        }
    }
}";

            // request/response
            var expectedRequestVariables = new { first = 2, after = Guid.NewGuid().ToString() };
            var expectedRequestBody = new { query, variables = expectedRequestVariables };
            var repoMigration = CreateRepositoryMigration();
            var responseContent = new
            {
                data = new
                {
                    node = new
                    {
                        login = "github",
                        repositoryMigrations = new
                        {
                            pageInfo = new { endCursor = Guid.NewGuid().ToString(), hasNextPage = false },
                            totalCount = 1,
                            nodes = new[] { repoMigration }
                        }
                    }
                }
            };
            using var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(responseContent.ToJson())
            };

            var handlerMock = MockHttpHandler(
                req => req.Method == HttpMethod.Post && req.RequestUri.ToString() == url && req.Content.ReadAsStringAsync().Result == expectedRequestBody.ToJson(),
                response);

            using var httpClient = new HttpClient(handlerMock.Object);
            var githubClient = new GithubClient(_mockOctoLogger.Object, httpClient, null, _retryPolicy, _dateTimeProvider.Object, PERSONAL_ACCESS_TOKEN);

            // Act
            var result = await githubClient.PostGraphQLWithPaginationAsync(
                    url,
                    new { query },
                    obj => (JArray)obj["data"]["node"]["repositoryMigrations"]["nodes"],
                    obj => (JObject)obj["data"]["node"]["repositoryMigrations"]["pageInfo"],
                    expectedRequestVariables.first,
                    expectedRequestVariables.after)
                .ToListAsync();

            // Assert
            result.Should().HaveCount(1);
            result[0].ToJson().Should().Be(repoMigration.ToJson());
        }

        [Fact]
        public async Task PostGraphQLWithPaginationAsync_Should_Create_First_And_After_In_Query_Variables_If_Missing()
        {
            // Arrange
            const string url = "https://example.com/graphql";
            const string query = @"
query($id: ID!, $first: Int, $after: String) {
    node(id: $id) { 
        ... on Organization { 
            login, 
            repositoryMigrations(first: $first, after: $after) {
                pageInfo {
                    endCursor
                    hasNextPage
                }
                totalCount
                nodes {
                    id
                    sourceUrl
                    migrationSource { name }
                    state
                    failureReason
                    createdAt
                }
            }
        }
    }
}";

            // request/response
            var expectedRequestVariables = new { id = "ORG_ID", first = 10, after = Guid.NewGuid().ToString() };
            var expectedRequestBody = new { query, variables = expectedRequestVariables };
            var repoMigration = CreateRepositoryMigration();
            var responseContent = new
            {
                data = new
                {
                    node = new
                    {
                        login = "github",
                        repositoryMigrations = new
                        {
                            pageInfo = new { endCursor = Guid.NewGuid().ToString(), hasNextPage = false },
                            totalCount = 1,
                            nodes = new[] { repoMigration }
                        }
                    }
                }
            };
            using var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(responseContent.ToJson())
            };

            var handlerMock = MockHttpHandler(
                req => req.Method == HttpMethod.Post && req.RequestUri.ToString() == url && req.Content.ReadAsStringAsync().Result == expectedRequestBody.ToJson(),
                response);

            using var httpClient = new HttpClient(handlerMock.Object);
            var githubClient = new GithubClient(_mockOctoLogger.Object, httpClient, null, _retryPolicy, _dateTimeProvider.Object, PERSONAL_ACCESS_TOKEN);

            // Act
            var result = await githubClient.PostGraphQLWithPaginationAsync(
                    url,
                    new { query, variables = new { id = "ORG_ID" } },
                    obj => (JArray)obj["data"]["node"]["repositoryMigrations"]["nodes"],
                    obj => (JObject)obj["data"]["node"]["repositoryMigrations"]["pageInfo"],
                    expectedRequestVariables.first,
                    expectedRequestVariables.after)
                .ToListAsync();

            // Assert
            result.Should().HaveCount(1);
            result[0].ToJson().Should().Be(repoMigration.ToJson());
        }

        [Fact]
        public async Task PostGraphQLWithPaginationAsync_Should_Override_First_And_After_In_Query_Variables()
        {
            // Arrange
            const string url = "https://example.com/graphql";
            const string query = @"
query($id: ID!, $first: Int, $after: String) {
    node(id: $id) { 
        ... on Organization { 
            login, 
            repositoryMigrations(first: $first, after: $after) {
                pageInfo {
                    endCursor
                    hasNextPage
                }
                totalCount
                nodes {
                    id
                    sourceUrl
                    migrationSource { name }
                    state
                    failureReason
                    createdAt
                }
            }
        }
    }
}";

            // request/response
            var expectedRequestVariables = new { first = 2, after = Guid.NewGuid().ToString() };
            var expectedRequestBody = new { query, variables = expectedRequestVariables };
            var repoMigration = CreateRepositoryMigration();
            var responseContent = new
            {
                data = new
                {
                    node = new
                    {
                        login = "github",
                        repositoryMigrations = new
                        {
                            pageInfo = new { endCursor = Guid.NewGuid().ToString(), hasNextPage = false },
                            totalCount = 1,
                            nodes = new[] { repoMigration }
                        }
                    }
                }
            };
            using var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(responseContent.ToJson())
            };

            var handlerMock = MockHttpHandler(
                req => req.Method == HttpMethod.Post && req.RequestUri.ToString() == url && req.Content.ReadAsStringAsync().Result == expectedRequestBody.ToJson(),
                response);

            using var httpClient = new HttpClient(handlerMock.Object);
            var githubClient = new GithubClient(_mockOctoLogger.Object, httpClient, null, _retryPolicy, _dateTimeProvider.Object, PERSONAL_ACCESS_TOKEN);

            // Act
            var result = await githubClient.PostGraphQLWithPaginationAsync(
                    url,
                    new { query, variables = new { first = 10, after = Guid.NewGuid().ToString() } },
                    obj => (JArray)obj["data"]["node"]["repositoryMigrations"]["nodes"],
                    obj => (JObject)obj["data"]["node"]["repositoryMigrations"]["pageInfo"],
                    expectedRequestVariables.first,
                    expectedRequestVariables.after)
                .ToListAsync();

            // Assert
            result.Should().HaveCount(1);
            result[0].ToJson().Should().Be(repoMigration.ToJson());
        }

        [Fact]
        public async Task PostGraphQLWithPaginationAsync_Throws_If_Result_Collection_Selector_Is_Null()
        {
            // Arrange
            const string url = "https://example.com/graphql";
            using var httpClient = new HttpClient();
            var githubClient = new GithubClient(null, httpClient, null, _retryPolicy, _dateTimeProvider.Object, PERSONAL_ACCESS_TOKEN);

            // Act, Assert
            await githubClient
                .Invoking(async client => await client.PostGraphQLWithPaginationAsync(url, "", null, null).ToListAsync())
                .Should()
                .ThrowAsync<ArgumentNullException>()
                .WithParameterName("resultCollectionSelector");
        }

        [Fact]
        public async Task PostGraphQLWithPaginationAsync_Returns_When_PageInfo_Is_Missing()
        {
            // Arrange
            const string url = "https://example.com/graphql";
            const string query = @"
query($id: ID!, $first: Int, $after: String) {
    node(id: $id) { 
        ... on Organization { 
            login, 
            repositoryMigrations(first: $first, after: $after) {
                pageInfo {
                    endCursor
                    hasNextPage
                }
                totalCount
                nodes {
                    id
                    sourceUrl
                    migrationSource { name }
                    state
                    failureReason
                    createdAt
                }
            }
        }
    }
}";

            // request/response
            var requestVariables = new { first = 2, after = Guid.NewGuid().ToString() };
            var requestBody = new { query, variables = requestVariables };
            var repoMigration = CreateRepositoryMigration();
            var responseContent = new
            {
                data = new
                {
                    node = new
                    {
                        login = "github",
                        repositoryMigrations = new
                        {
                            totalCount = 1,
                            nodes = new[] { repoMigration }
                        }
                    }
                }
            };
            using var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(responseContent.ToJson())
            };

            var handlerMock = MockHttpHandler(
                req => req.Method == HttpMethod.Post && req.RequestUri.ToString() == url && req.Content.ReadAsStringAsync().Result == requestBody.ToJson(),
                response);

            using var httpClient = new HttpClient(handlerMock.Object);
            var githubClient = new GithubClient(_mockOctoLogger.Object, httpClient, null, _retryPolicy, _dateTimeProvider.Object, PERSONAL_ACCESS_TOKEN);

            // Act
            var result = await githubClient.PostGraphQLWithPaginationAsync(
                    url,
                    requestBody,
                    obj => (JArray)obj["data"]["node"]["repositoryMigrations"]["nodes"],
                    obj => (JObject)obj["data"]["node"]["repositoryMigrations"]["pageInfo"],
                    2,
                    requestVariables.after)
                .ToListAsync();

            // Assert
            result.Should().HaveCount(1);
            result[0].ToJson().Should().Be(repoMigration.ToJson());
            handlerMock.Protected().Verify(
                "SendAsync",
                Times.Once(),
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>());
        }

        [Fact]
        public async Task PostGraphQLWithPaginationAsync_Does_Not_Continue_When_HasNextPage_Is_Missing()
        {
            // Arrange
            const string url = "https://example.com/graphql";
            const string query = @"
query($id: ID!, $first: Int, $after: String) {
    node(id: $id) { 
        ... on Organization { 
            login, 
            repositoryMigrations(first: $first, after: $after) {
                pageInfo {
                    endCursor
                    hasNextPage
                }
                totalCount
                nodes {
                    id
                    sourceUrl
                    migrationSource { name }
                    state
                    failureReason
                    createdAt
                }
            }
        }
    }
}";

            // request/response
            var requestVariables = new { first = 2, after = Guid.NewGuid().ToString() };
            var requestBody = new { query, variables = requestVariables };
            var repoMigration = CreateRepositoryMigration();
            var responseContent = new
            {
                data = new
                {
                    node = new
                    {
                        login = "github",
                        repositoryMigrations = new
                        {
                            pageInfo = new { endCursor = Guid.NewGuid().ToString() },
                            totalCount = 1,
                            nodes = new[] { repoMigration }
                        }
                    }
                }
            };
            using var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(responseContent.ToJson())
            };

            var handlerMock = MockHttpHandler(
                req => req.Method == HttpMethod.Post && req.RequestUri.ToString() == url && req.Content.ReadAsStringAsync().Result == requestBody.ToJson(),
                response);

            using var httpClient = new HttpClient(handlerMock.Object);
            var githubClient = new GithubClient(_mockOctoLogger.Object, httpClient, null, _retryPolicy, _dateTimeProvider.Object, PERSONAL_ACCESS_TOKEN);

            // Act
            var result = await githubClient.PostGraphQLWithPaginationAsync(
                    url,
                    requestBody,
                    obj => (JArray)obj["data"]["node"]["repositoryMigrations"]["nodes"],
                    obj => (JObject)obj["data"]["node"]["repositoryMigrations"]["pageInfo"],
                    2,
                    requestVariables.after)
                .ToListAsync();

            // Assert
            result.Should().HaveCount(1);
            result[0].ToJson().Should().Be(repoMigration.ToJson());
            handlerMock.Protected().Verify(
                "SendAsync",
                Times.Once(),
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>());
        }

        [Fact]
        public async Task PostGraphQLWithPaginationAsync_Throws_If_PageInfo_Selector_Is_Null()
        {
            // Arrange
            const string url = "https://example.com/graphql";
            using var httpClient = new HttpClient();
            var githubClient = new GithubClient(null, httpClient, null, _retryPolicy, _dateTimeProvider.Object, PERSONAL_ACCESS_TOKEN);

            // Act, Assert
            await githubClient
                .Invoking(async client => await client.PostGraphQLWithPaginationAsync(url, "", x => (JArray)x["item"], null).ToListAsync())
                .Should()
                .ThrowAsync<ArgumentNullException>()
                .WithParameterName("pageInfoSelector");
        }

        [Fact]
        public async Task PostGraphQLWithPaginationAsync_Logs_The_GitHub_Request_Id_Header_Value_Per_Each_Page()
        {
            // Arrange
            const string url = "https://example.com/graphql";
            const string orgId = "ORG_ID";
            const string query = "QUERY";

            // first request/response
            var firstRequestVariables = new { id = orgId, first = 2, after = (string)null };
            var firstRequestBody = new { query, variables = firstRequestVariables };
            var firstResponseEndCursor = Guid.NewGuid().ToString();
            var firstResponseContent = new
            {
                data = new
                {
                    node = new
                    {
                        repositoryMigrations = new
                        {
                            pageInfo = new { endCursor = firstResponseEndCursor, hasNextPage = true },
                            nodes = new[] { 1, 2 }
                        }
                    }
                }
            };
            using var firstResponse = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(firstResponseContent.ToJson())
            };
            const string firstGithubRequestId = "123-456";
            firstResponse.Headers.Add("X-GitHub-Request-Id", firstGithubRequestId);

            // second request/response
            var secondRequestVariables = new { id = orgId, first = 2, after = firstResponseEndCursor };
            var secondResponseEndCursor = Guid.NewGuid().ToString();
            var secondResponseContent = new
            {
                data = new
                {
                    node = new
                    {
                        repositoryMigrations = new
                        {
                            pageInfo = new { endCursor = secondResponseEndCursor, hasNextPage = false },
                            nodes = new[] { 3, 4 }
                        }
                    }
                }
            };
            using var secondResponse = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(secondResponseContent.ToJson())
            };
            const string secondGithubRequestId = "456-789";
            secondResponse.Headers.Add("X-GitHub-Request-Id", secondGithubRequestId);
            var secondRequestBody = new { query, variables = secondRequestVariables };

            var handlerMock = MockHttpHandler(
                req => req.Method == HttpMethod.Post && req.RequestUri.ToString() == url && req.Content.ReadAsStringAsync().Result == firstRequestBody.ToJson(),
                firstResponse); // first request
            MockHttpHandler(
                req => req.Method == HttpMethod.Post && req.RequestUri.ToString() == url && req.Content.ReadAsStringAsync().Result == secondRequestBody.ToJson(),
                secondResponse,
                handlerMock); // second request

            using var httpClient = new HttpClient(handlerMock.Object);
            var githubClient = new GithubClient(_mockOctoLogger.Object, httpClient, null, _retryPolicy, _dateTimeProvider.Object, PERSONAL_ACCESS_TOKEN);

            // Act
            _ = await githubClient.PostGraphQLWithPaginationAsync(
                    url,
                    firstRequestBody,
                    obj => (JArray)obj["data"]["node"]["repositoryMigrations"]["nodes"],
                    obj => (JObject)obj["data"]["node"]["repositoryMigrations"]["pageInfo"],
                    2)
                .ToListAsync();

            // Assert
            _mockOctoLogger.Verify(m => m.LogVerbose($"GITHUB REQUEST ID: {firstGithubRequestId}"));
            _mockOctoLogger.Verify(m => m.LogVerbose($"GITHUB REQUEST ID: {secondGithubRequestId}"));
        }

        [Fact]
        public void It_Sets_User_Agent_Header_With_Comments()
        {
            // Arrange
            const string currentVersion = "1.1.1.1";
            const string versionComments = "(COMMENTS)";

            using var httpClient = new HttpClient();

            var mockVersionProvider = new Mock<IVersionProvider>();
            mockVersionProvider.Setup(m => m.GetCurrentVersion()).Returns("1.1.1.1");
            mockVersionProvider.Setup(m => m.GetVersionComments()).Returns(versionComments);

            // Act
            _ = new GithubClient(null, httpClient, mockVersionProvider.Object, _retryPolicy, _dateTimeProvider.Object, PERSONAL_ACCESS_TOKEN);

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
            _ = new GithubClient(null, httpClient, null, _retryPolicy, _dateTimeProvider.Object, PERSONAL_ACCESS_TOKEN);

            // Assert
            httpClient.DefaultRequestHeaders.UserAgent.Should().HaveCount(1);
            httpClient.DefaultRequestHeaders.UserAgent.ToString().Should().Be("OctoshiftCLI");
        }

        private object CreateRepositoryMigration(string migrationId = null, string state = RepositoryMigrationStatus.Succeeded) => new
        {
            id = migrationId ?? Guid.NewGuid().ToString(),
            sourceUrl = "SOURCE_URL",
            migrationSource = new { name = "Azure Devops Source" },
            state,
            failureReason = "",
            createdAt = DateTime.UtcNow
        };

        private Mock<HttpMessageHandler> MockHttpHandlerForGet() =>
            MockHttpHandler(req => req.Method == HttpMethod.Get);

        private Mock<HttpMessageHandler> MockHttpHandlerForDelete() =>
            MockHttpHandler(req => req.Method == HttpMethod.Delete);

        private Mock<HttpMessageHandler> MockHttpHandlerForPost() =>
            MockHttpHandler(req =>
                req.Method == HttpMethod.Post);

        private Mock<HttpMessageHandler> MockHttpHandlerForGraphQLPost(HttpResponseMessage httpResponseMessage = null)
        {
            return MockHttpHandler(req =>
                req.Method == HttpMethod.Post, httpResponseMessage ?? _graphqlHttpResponse);
        }

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
                .ReturnsAsync(httpResponse ?? _defaultHttpResponse);
            return handlerMock;
        }
    }
}
