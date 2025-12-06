using System;
using System.Collections.Generic;
using System.IO;
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
using OctoshiftCLI.Services;
using Xunit;

namespace OctoshiftCLI.Tests.Octoshift.Services;

public sealed class GithubClientTests
{
    private readonly Mock<OctoLogger> _mockOctoLogger = TestHelpers.CreateMock<OctoLogger>();
    private readonly RetryPolicy _retryPolicy;
    private readonly Mock<DateTimeProvider> _dateTimeProvider = TestHelpers.CreateMock<DateTimeProvider>();
    private readonly object _rawRequestBody;
    private const string GITHUB_REQUEST_ID = "123-456-789";
    private const string EXPECTED_JSON_REQUEST_BODY = "{\"id\":\"ID\"}";
    private const string EXPECTED_RESPONSE_CONTENT = "RESPONSE_CONTENT";
    private const string EXPECTED_GRAPHQL_JSON_RESPONSE_BODY = "{\"id\":\"ID\"}";
    private const string EXPECTED_GRAPHQL_JSON_ERROR_RESPONSE_BODY = "{\"data\":{\"createMigrationSource\":null},\"errors\":[{\"type\":\"FORBIDDEN\",\"path\":[\"createMigrationSource\"],\"extensions\":{\"saml_failure\":true},\"locations\":[{\"line\":1,\"column\":109}],\"message\":\"Resource protected by organization SAML enforcement. You must grant your Personal Access token access to this organization.\"}]}";
    private const string PERSONAL_ACCESS_TOKEN = "PERSONAL_ACCESS_TOKEN";
    private const string URL = "https://api.github.com/resource";

    public GithubClientTests()
    {
        _rawRequestBody = new { id = "ID" };

        _retryPolicy = new RetryPolicy(_mockOctoLogger.Object)
        {
            _httpRetryInterval = 0,
            _retryInterval = 0
        };
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

    [Theory]
    [InlineData(HttpStatusCode.InternalServerError)]
    [InlineData(HttpStatusCode.ServiceUnavailable)]
    public async Task GetAsync_Retries_On_Non_Success(HttpStatusCode httpStatusCode)
    {
        // Arrange
        var handlerMock = new Mock<HttpMessageHandler>();
        handlerMock
            .Protected()
            .SetupSequence<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.Is<HttpRequestMessage>(req => req.Method == HttpMethod.Get),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(CreateHttpResponseFactory(statusCode: httpStatusCode, content: "FIRST_RESPONSE"))
            .ReturnsAsync(CreateHttpResponseFactory(content: EXPECTED_RESPONSE_CONTENT));

        using var httpClient = new HttpClient(handlerMock.Object);
        var githubClient = new GithubClient(_mockOctoLogger.Object, httpClient, null, _retryPolicy, _dateTimeProvider.Object, PERSONAL_ACCESS_TOKEN);

        // Act
        var returnedContent = await githubClient.GetAsync(URL);

        // Assert
        returnedContent.Should().Be(EXPECTED_RESPONSE_CONTENT);
    }

    [Fact]
    public async Task GetAsync_Bubbles_UnAuthorized_Error()
    {
        // Arrange
        var handlerMock = new Mock<HttpMessageHandler>();
        handlerMock
            .Protected()
            .SetupSequence<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.Is<HttpRequestMessage>(req => req.Method == HttpMethod.Get),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(CreateHttpResponseFactory(statusCode: HttpStatusCode.Unauthorized, content: "FIRST_RESPONSE"));

        using var httpClient = new HttpClient(handlerMock.Object);
        var githubClient = new GithubClient(_mockOctoLogger.Object, httpClient, null, _retryPolicy, _dateTimeProvider.Object, PERSONAL_ACCESS_TOKEN);

        // Act
        // Assert
        await githubClient
            .Invoking(async x => await x.GetAsync(URL))
            .Should()
            .ThrowExactlyAsync<OctoshiftCliException>()
            .WithMessage("Unauthorized. Please check your token and try again");
    }

    [Fact]
    public async Task GetAsync_Retries_On_Timeout_Exception()
    {
        // Arrange
        var handlerMock = new Mock<HttpMessageHandler>();
        handlerMock
            .Protected()
            .SetupSequence<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.Is<HttpRequestMessage>(req => req.Method == HttpMethod.Get),
                ItExpr.IsAny<CancellationToken>())
            .ThrowsAsync(new TimeoutException())
            .ReturnsAsync(CreateHttpResponseFactory(content: EXPECTED_RESPONSE_CONTENT));

        using var httpClient = new HttpClient(handlerMock.Object);
        var githubClient = new GithubClient(_mockOctoLogger.Object, httpClient, null, _retryPolicy, _dateTimeProvider.Object, PERSONAL_ACCESS_TOKEN);

        // Act
        var returnedContent = await githubClient.GetAsync(URL);

        // Assert
        returnedContent.Should().Be(EXPECTED_RESPONSE_CONTENT);
    }

    [Fact]
    public async Task GetAsync_Logs_The_Url()
    {
        // Arrange
        using var httpClient = new HttpClient(MockHttpHandlerForGet().Object);
        var githubClient = new GithubClient(_mockOctoLogger.Object, httpClient, null, _retryPolicy, _dateTimeProvider.Object, PERSONAL_ACCESS_TOKEN);

        var expectedLogMessage = $"HTTP GET: {URL}";

        // Act
        await githubClient.GetAsync(URL);

        // Assert
        _mockOctoLogger.Verify(m =>
            m.LogVerbose(It.Is<string>(actualLogMessage => actualLogMessage == expectedLogMessage)));
    }

    [Fact]
    public async Task GetAsync_Logs_The_GitHub_Request_Id_Header_Value()
    {
        // Arrange
        const string expectedLogMessage = $"GITHUB REQUEST ID: {GITHUB_REQUEST_ID}";

        var mockHandler = MockHttpHandlerForGet(CreateHttpResponseFactory(headers: new[] { ("X-GitHub-Request-Id", GITHUB_REQUEST_ID) }));
        using var httpClient = new HttpClient(mockHandler.Object);
        var githubClient = new GithubClient(_mockOctoLogger.Object, httpClient, null, _retryPolicy, _dateTimeProvider.Object, PERSONAL_ACCESS_TOKEN);

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
        var handlerMock = MockHttpHandler(req => req.Method == HttpMethod.Get, CreateHttpResponseFactory(HttpStatusCode.InternalServerError));

        using var httpClient = new HttpClient(handlerMock.Object);
        var githubClient = new GithubClient(_mockOctoLogger.Object, httpClient, null, _retryPolicy, _dateTimeProvider.Object, PERSONAL_ACCESS_TOKEN);

        // Act
        // Assert
        await FluentActions
            .Invoking(async () => await githubClient.GetAsync("http://example.com"))
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

        var handlerMock = new Mock<HttpMessageHandler>();
        handlerMock
            .Protected()
            .SetupSequence<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.Is<HttpRequestMessage>(req => req.Method == HttpMethod.Get),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(CreateHttpResponseFactory(content: "FIRST_RESPONSE"))
            .ReturnsAsync(CreateHttpResponseFactory(
                content: "API RATE LIMIT EXCEEDED blah blah blah",
                headers: new[] { ("X-RateLimit-Reset", retryAt.ToString()), ("X-RateLimit-Remaining", "0") }))
            .ReturnsAsync(CreateHttpResponseFactory(content: "THIRD_RESPONSE"));

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

        var handlerMock = new Mock<HttpMessageHandler>();
        handlerMock
            .Protected()
            .SetupSequence<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.Is<HttpRequestMessage>(req => req.Method == HttpMethod.Get),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(CreateHttpResponseFactory(content: "FIRST_RESPONSE"))
            .ReturnsAsync(CreateHttpResponseFactory(
                statusCode: HttpStatusCode.Forbidden,
                content: "API RATE LIMIT EXCEEDED blah blah blah",
                headers: new[] { ("X-RateLimit-Reset", retryAt.ToString()), ("X-RateLimit-Remaining", "0") }))
            .ReturnsAsync(CreateHttpResponseFactory(content: "THIRD_RESPONSE"));

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
    public async Task PostAsync_With_StreamContent_Returns_String_Response()
    {
        // Arrange
        var stream = new MemoryStream(new byte[] { 1, 2, 3 });
        using var expectedStreamContent = new StreamContent(stream);
        expectedStreamContent.Headers.ContentType = new("application/octet-stream");

        var handlerMock = MockHttpHandler(req => req.Method == HttpMethod.Post && req.Content == expectedStreamContent);
        using var httpClient = new HttpClient(handlerMock.Object);
        var githubClient = new GithubClient(_mockOctoLogger.Object, httpClient, null, _retryPolicy, _dateTimeProvider.Object, PERSONAL_ACCESS_TOKEN);

        // Act
        var actualContent = await githubClient.PostAsync("http://example.com", expectedStreamContent);

        // Assert
        actualContent.Should().Be(EXPECTED_RESPONSE_CONTENT);
    }

    [Fact]
    public async Task PostAsync_With_MultipartFormDataContent_Returns_String_Response()
    {
        // Arrange
        using var stream = new StreamContent(new MemoryStream(new byte[] { 1, 2, 3 }));
        stream.Headers.ContentType = new("application/octet-stream");
#pragma warning disable IDE0028
        using var expectedMultipartContent = new MultipartFormDataContent();
        expectedMultipartContent.Add(stream, "filePart", "example.txt");
#pragma warning restore

        var handlerMock = MockHttpHandler(req => req.Method == HttpMethod.Post && req.Content == expectedMultipartContent);
        using var httpClient = new HttpClient(handlerMock.Object);
        var githubClient = new GithubClient(_mockOctoLogger.Object, httpClient, null, _retryPolicy, _dateTimeProvider.Object, PERSONAL_ACCESS_TOKEN);

        // Act
        var actualContent = await githubClient.PostAsync("http://example.com", expectedMultipartContent);

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

        var handlerMock = new Mock<HttpMessageHandler>();
        handlerMock
            .Protected()
            .SetupSequence<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.Is<HttpRequestMessage>(req => req.Method == HttpMethod.Post),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(CreateHttpResponseFactory(
                statusCode: HttpStatusCode.Unauthorized,
                content: "{\"message\":\"Bad credentials\",\"documentation_url\":\"https://docs.github.com/graphql\"}",
                headers: new[] { ("X-RateLimit-Reset", retryAt.ToString()), ("X-RateLimit-Remaining", "0") }))
            .ReturnsAsync(CreateHttpResponseFactory(
                statusCode: HttpStatusCode.Unauthorized,
                content: "{\"message\":\"Bad credentials\",\"documentation_url\":\"https://docs.github.com/graphql\"}",
                headers: new[] { ("X-RateLimit-Reset", retryAt.ToString()), ("X-RateLimit-Remaining", "0") }));

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

        var handlerMock = new Mock<HttpMessageHandler>();
        handlerMock
            .Protected()
            .SetupSequence<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.Is<HttpRequestMessage>(req => req.Method == HttpMethod.Post),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(CreateHttpResponseFactory(content: "FIRST_RESPONSE"))
            .ReturnsAsync(CreateHttpResponseFactory(
                content: "API RATE LIMIT EXCEEDED blah blah blah",
                headers: new[] { ("X-RateLimit-Reset", retryAt.ToString()), ("X-RateLimit-Remaining", "0") }))
            .ReturnsAsync(CreateHttpResponseFactory(content: "THIRD_RESPONSE"));

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

        var handlerMock = new Mock<HttpMessageHandler>();
        handlerMock
            .Protected()
            .SetupSequence<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.Is<HttpRequestMessage>(req => req.Method == HttpMethod.Post),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(CreateHttpResponseFactory(content: "FIRST_RESPONSE"))
            .ReturnsAsync(CreateHttpResponseFactory(
                statusCode: HttpStatusCode.Forbidden,
                content: "API RATE LIMIT EXCEEDED blah blah blah",
                headers: new[] { ("X-RateLimit-Reset", retryAt.ToString()), ("X-RateLimit-Remaining", "0") }))
            .ReturnsAsync(CreateHttpResponseFactory(content: "THIRD_RESPONSE"));

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
    public async Task PostAsync_Logs_The_Url()
    {
        // Arrange
        using var httpClient = new HttpClient(MockHttpHandlerForPost().Object);
        var githubClient = new GithubClient(_mockOctoLogger.Object, httpClient, null, _retryPolicy, _dateTimeProvider.Object, PERSONAL_ACCESS_TOKEN);

        var expectedLogMessage = $"HTTP POST: {URL}";

        // Act
        await githubClient.PostAsync(URL, _rawRequestBody);

        // Assert
        _mockOctoLogger.Verify(m =>
            m.LogVerbose(It.Is<string>(actualLogMessage => actualLogMessage == expectedLogMessage)));
    }

    [Fact]
    public async Task PostAsync_Logs_The_GitHub_Request_Id_Header_Value()
    {
        // Arrange
        const string expectedLogMessage = $"GITHUB REQUEST ID: {GITHUB_REQUEST_ID}";

        var mockHandler = MockHttpHandlerForPost(CreateHttpResponseFactory(headers: new[] { ("X-GitHub-Request-Id", GITHUB_REQUEST_ID) }));
        using var httpClient = new HttpClient(mockHandler.Object);
        var githubClient = new GithubClient(_mockOctoLogger.Object, httpClient, null, _retryPolicy, _dateTimeProvider.Object, PERSONAL_ACCESS_TOKEN);


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

        var expectedLogMessage = $"RESPONSE ({HttpStatusCode.OK}): {EXPECTED_RESPONSE_CONTENT}";

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
        var handlerMock = MockHttpHandlerForPost(CreateHttpResponseFactory(HttpStatusCode.InternalServerError, EXPECTED_RESPONSE_CONTENT));
        var loggerMock = TestHelpers.CreateMock<OctoLogger>();

        using var httpClient = new HttpClient(handlerMock.Object);
        var githubClient = new GithubClient(loggerMock.Object, httpClient, null, _retryPolicy, _dateTimeProvider.Object, PERSONAL_ACCESS_TOKEN);

        // Act
        // Assert
        await FluentActions
            .Invoking(() => githubClient.PostAsync("http://example.com", _rawRequestBody))
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
    public async Task PostGraphQLAsync_Logs_The_Url()
    {
        // Arrange
        using var httpClient = new HttpClient(MockHttpHandlerForGraphQLPost().Object);
        var githubClient = new GithubClient(_mockOctoLogger.Object, httpClient, null, _retryPolicy, _dateTimeProvider.Object, PERSONAL_ACCESS_TOKEN);

        var expectedLogMessage = $"HTTP POST: {URL}";

        // Act
        await githubClient.PostGraphQLAsync(URL, _rawRequestBody);

        // Assert
        _mockOctoLogger.Verify(m =>
            m.LogVerbose(It.Is<string>(actualLogMessage => actualLogMessage == expectedLogMessage)));
    }

    [Fact]
    public async Task PostGraphQLAsync_Logs_The_GitHub_Request_Id_Header_Value()
    {
        // Arrange
        var mockHandler = MockHttpHandlerForGraphQLPost(CreateHttpResponseFactory(
            content: EXPECTED_GRAPHQL_JSON_RESPONSE_BODY,
            headers: new[] { ("X-GitHub-Request-Id", GITHUB_REQUEST_ID) }));
        using var httpClient = new HttpClient(mockHandler.Object);
        var githubClient = new GithubClient(_mockOctoLogger.Object, httpClient, null, _retryPolicy, _dateTimeProvider.Object, PERSONAL_ACCESS_TOKEN);

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

        var expectedLogMessage = $"RESPONSE ({HttpStatusCode.OK}): {EXPECTED_GRAPHQL_JSON_RESPONSE_BODY}";

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
        var handlerMock = MockHttpHandlerForGraphQLPost(CreateHttpResponseFactory(content: EXPECTED_GRAPHQL_JSON_ERROR_RESPONSE_BODY));
        var loggerMock = TestHelpers.CreateMock<OctoLogger>();

        using var httpClient = new HttpClient(handlerMock.Object);
        var githubClient = new GithubClient(loggerMock.Object, httpClient, null, _retryPolicy, _dateTimeProvider.Object, PERSONAL_ACCESS_TOKEN);

        // Act
        // Assert
        await FluentActions
            .Invoking(() => githubClient.PostGraphQLAsync("http://example.com", _rawRequestBody))
            .Should()
            .ThrowExactlyAsync<OctoshiftCliException>()
            .WithMessage("Resource protected by organization SAML enforcement. You must grant your Personal Access token access to this organization.");
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

        var handlerMock = new Mock<HttpMessageHandler>();
        handlerMock
            .Protected()
            .SetupSequence<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.Is<HttpRequestMessage>(req => req.Method == HttpMethod.Put),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(CreateHttpResponseFactory(HttpStatusCode.OK, "FIRST_RESPONSE"))
            .ReturnsAsync(CreateHttpResponseFactory(
                statusCode: HttpStatusCode.OK,
                content: "API RATE LIMIT EXCEEDED blah blah blah",
                headers: new[] { ("X-RateLimit-Reset", retryAt.ToString()), ("X-RateLimit-Remaining", "0") }))
            .ReturnsAsync(CreateHttpResponseFactory(HttpStatusCode.OK, "THIRD_RESPONSE"));

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
    public async Task PutAsync_Logs_The_Url()
    {
        // Arrange
        using var httpClient = new HttpClient(MockHttpHandlerForPut().Object);
        var githubClient = new GithubClient(_mockOctoLogger.Object, httpClient, null, _retryPolicy, _dateTimeProvider.Object, PERSONAL_ACCESS_TOKEN);

        var expectedLogMessage = $"HTTP PUT: {URL}";

        // Act
        await githubClient.PutAsync(URL, _rawRequestBody);

        // Assert
        _mockOctoLogger.Verify(m =>
            m.LogVerbose(It.Is<string>(actualLogMessage => actualLogMessage == expectedLogMessage)));
    }

    [Fact]
    public async Task PutAsync_Logs_The_GitHub_Request_Id_Header_Value()
    {
        // Arrange
        const string expectedLogMessage = $"GITHUB REQUEST ID: {GITHUB_REQUEST_ID}";

        var mockHandler = MockHttpHandlerForPut(CreateHttpResponseFactory(headers: new[] { ("X-GitHub-Request-Id", GITHUB_REQUEST_ID) }));
        using var httpClient = new HttpClient(mockHandler.Object);
        var githubClient = new GithubClient(_mockOctoLogger.Object, httpClient, null, _retryPolicy, _dateTimeProvider.Object, PERSONAL_ACCESS_TOKEN);

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

        var expectedLogMessage = $"RESPONSE ({HttpStatusCode.OK}): {EXPECTED_RESPONSE_CONTENT}";

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
        var handlerMock = MockHttpHandlerForPut(CreateHttpResponseFactory(HttpStatusCode.InternalServerError));
        var loggerMock = TestHelpers.CreateMock<OctoLogger>();

        using var httpClient = new HttpClient(handlerMock.Object);
        var githubClient = new GithubClient(loggerMock.Object, httpClient, null, _retryPolicy, _dateTimeProvider.Object, PERSONAL_ACCESS_TOKEN);

        // Act
        // Assert
        await FluentActions
            .Invoking(() => githubClient.PutAsync("http://example.com", _rawRequestBody))
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

        var handlerMock = new Mock<HttpMessageHandler>();
        handlerMock
            .Protected()
            .SetupSequence<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.Is<HttpRequestMessage>(req => req.Method == HttpMethod.Patch),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(CreateHttpResponseFactory(HttpStatusCode.OK, "FIRST_RESPONSE"))
            .ReturnsAsync(CreateHttpResponseFactory(
                statusCode: HttpStatusCode.OK,
                content: "API RATE LIMIT EXCEEDED blah blah blah",
                headers: new[] { ("X-RateLimit-Reset", retryAt.ToString()), ("X-RateLimit-Remaining", "0") }))
            .ReturnsAsync(CreateHttpResponseFactory(HttpStatusCode.OK, "THIRD_RESPONSE"));

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
    public async Task PatchAsync_Logs_The_Url()
    {
        // Arrange
        using var httpClient = new HttpClient(MockHttpHandlerForPatch().Object);
        var githubClient = new GithubClient(_mockOctoLogger.Object, httpClient, null, _retryPolicy, _dateTimeProvider.Object, PERSONAL_ACCESS_TOKEN);

        var expectedLogMessage = $"HTTP PATCH: {URL}";

        // Act
        await githubClient.PatchAsync(URL, _rawRequestBody);

        // Assert
        _mockOctoLogger.Verify(m =>
            m.LogVerbose(It.Is<string>(actualLogMessage => actualLogMessage == expectedLogMessage)));
    }

    [Fact]
    public async Task PatchAsync_Logs_The_GitHub_Request_Id_Header_Value()
    {
        // Arrange
        const string expectedLogMessage = $"GITHUB REQUEST ID: {GITHUB_REQUEST_ID}";

        var mockHandler = MockHttpHandlerForPatch(CreateHttpResponseFactory(headers: new[] { ("X-GitHub-Request-Id", GITHUB_REQUEST_ID) }));
        using var httpClient = new HttpClient(mockHandler.Object);
        var githubClient = new GithubClient(_mockOctoLogger.Object, httpClient, null, _retryPolicy, _dateTimeProvider.Object, PERSONAL_ACCESS_TOKEN);

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

        var expectedLogMessage = $"RESPONSE ({HttpStatusCode.OK}): {EXPECTED_RESPONSE_CONTENT}";

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
        var handlerMock = MockHttpHandlerForPatch(CreateHttpResponseFactory(HttpStatusCode.InternalServerError));
        var loggerMock = TestHelpers.CreateMock<OctoLogger>();

        using var httpClient = new HttpClient(handlerMock.Object);
        var githubClient = new GithubClient(loggerMock.Object, httpClient, null, _retryPolicy, _dateTimeProvider.Object, PERSONAL_ACCESS_TOKEN);

        // Act
        // Assert
        await FluentActions
            .Invoking(() => githubClient.PatchAsync("http://example.com", _rawRequestBody))
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

        var handlerMock = new Mock<HttpMessageHandler>();
        handlerMock
            .Protected()
            .SetupSequence<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.Is<HttpRequestMessage>(req => req.Method == HttpMethod.Delete),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(CreateHttpResponseFactory(HttpStatusCode.OK, "FIRST_RESPONSE"))
            .ReturnsAsync(CreateHttpResponseFactory(
                statusCode: HttpStatusCode.OK,
                content: "API RATE LIMIT EXCEEDED blah blah blah",
                headers: new[] { ("X-RateLimit-Reset", retryAt.ToString()), ("X-RateLimit-Remaining", "0") }))
            .ReturnsAsync(CreateHttpResponseFactory(HttpStatusCode.OK, "THIRD_RESPONSE"));

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
    public async Task DeleteAsync_Logs_The_Url()
    {
        // Arrange
        using var httpClient = new HttpClient(MockHttpHandlerForDelete().Object);
        var githubClient = new GithubClient(_mockOctoLogger.Object, httpClient, null, _retryPolicy, _dateTimeProvider.Object, PERSONAL_ACCESS_TOKEN);

        var expectedLogMessage = $"HTTP DELETE: {URL}";

        // Act
        await githubClient.DeleteAsync(URL);

        // Assert
        _mockOctoLogger.Verify(m =>
            m.LogVerbose(It.Is<string>(actualLogMessage => actualLogMessage == expectedLogMessage)));
    }

    [Fact]
    public async Task DeleteAsync_Logs_The_GitHub_Request_Id_Header_Value()
    {
        // Arrange
        const string expectedLogMessage = $"GITHUB REQUEST ID: {GITHUB_REQUEST_ID}";

        var mockHandler = MockHttpHandlerForDelete(CreateHttpResponseFactory(headers: new[] { ("X-GitHub-Request-Id", GITHUB_REQUEST_ID) }));
        using var httpClient = new HttpClient(mockHandler.Object);
        var githubClient = new GithubClient(_mockOctoLogger.Object, httpClient, null, _retryPolicy, _dateTimeProvider.Object, PERSONAL_ACCESS_TOKEN);

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
        var handlerMock = MockHttpHandler(req => req.Method == HttpMethod.Get, CreateHttpResponseFactory(HttpStatusCode.NotFound));

        using var httpClient = new HttpClient(handlerMock.Object);
        var githubClient = new GithubClient(_mockOctoLogger.Object, httpClient, null, _retryPolicy, _dateTimeProvider.Object, PERSONAL_ACCESS_TOKEN);

        // Assert
        await FluentActions
            .Invoking(async () => await githubClient.GetNonSuccessAsync("http://example.com", HttpStatusCode.Moved))
            .Should()
            .ThrowExactlyAsync<HttpRequestException>();
    }

    [Fact]
    public async Task GetNonSuccessAsync_With_302_Is_Successful()
    {
        // Arrange
        var handlerMock = MockHttpHandlerForGet(CreateHttpResponseFactory(HttpStatusCode.Moved));

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
        var handlerMock = MockHttpHandler(
            req => req.Method == HttpMethod.Get,
            CreateHttpResponseFactory(HttpStatusCode.Moved, headers: new[] { ("X-GitHub-Request-Id", GITHUB_REQUEST_ID) }));

        using var httpClient = new HttpClient(handlerMock.Object);
        var githubClient = new GithubClient(_mockOctoLogger.Object, httpClient, null, _retryPolicy, _dateTimeProvider.Object, PERSONAL_ACCESS_TOKEN);

        // Act
        await githubClient.GetNonSuccessAsync("http://example.com", HttpStatusCode.Moved);

        // Assert
        _mockOctoLogger.Verify(m => m.LogVerbose($"GITHUB REQUEST ID: {GITHUB_REQUEST_ID}"));
    }

    [Fact]
    public async Task GetNonSuccessAsync_Retries_On_Non_Success()
    {
        // Arrange
        var handlerMock = new Mock<HttpMessageHandler>();
        handlerMock
            .Protected()
            .SetupSequence<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.Is<HttpRequestMessage>(req => req.Method == HttpMethod.Get),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(CreateHttpResponseFactory(statusCode: HttpStatusCode.InternalServerError))
            .ReturnsAsync(CreateHttpResponseFactory(statusCode: HttpStatusCode.Found, content: EXPECTED_RESPONSE_CONTENT));

        using var httpClient = new HttpClient(handlerMock.Object);
        var githubClient = new GithubClient(_mockOctoLogger.Object, httpClient, null, _retryPolicy, _dateTimeProvider.Object, PERSONAL_ACCESS_TOKEN);

        // Act
        var result = await githubClient.GetNonSuccessAsync(URL, HttpStatusCode.Found);

        // Assert
        result.Should().Be(EXPECTED_RESPONSE_CONTENT);
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
        var handlerMock = MockHttpHandlerForDelete(CreateHttpResponseFactory(HttpStatusCode.InternalServerError));

        var loggerMock = TestHelpers.CreateMock<OctoLogger>();

        using var httpClient = new HttpClient(handlerMock.Object);
        var githubClient = new GithubClient(loggerMock.Object, httpClient, null, _retryPolicy, _dateTimeProvider.Object, PERSONAL_ACCESS_TOKEN);

        // Act
        // Assert
        await FluentActions
            .Invoking(() => githubClient.DeleteAsync("http://example.com"))
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
        const string thirdItem = "third";
        const string fourthItem = "fourth";
        const string fifthItem = "fifth";

        var handlerMock = new Mock<HttpMessageHandler>();

        // first request
        MockHttpHandler(
            req => req.Method == HttpMethod.Get && req.RequestUri.ToString() == url,
            CreateHttpResponseFactory(
                content: $"[\"{firstItem}\", \"{secondItem}\"]",
                headers: new[]
                {
                    ("Link", $"<{url}&page=2>; rel=\"next\", " +
                             $"<{url}&page=4>; rel=\"last\""
                    )
                }),
            handlerMock);

        // second request
        MockHttpHandler(
            req => req.Method == HttpMethod.Get && req.RequestUri.ToString() == $"{url}&page=2",
            CreateHttpResponseFactory(
                content: $"[\"{thirdItem}\", \"{fourthItem}\"]",
                headers: new[]
                {
                    ("Link", $"<{url}&page=1>; rel=\"prev\", " +
                             $"<{url}&page=3>; rel=\"next\", " +
                             $"<{url}&page=3>; rel=\"last\", " +
                             $"<{url}&page=1>; rel=\"first\""
                    )
                }),
            handlerMock);

        // third request
        MockHttpHandler(
            req => req.Method == HttpMethod.Get && req.RequestUri.ToString() == $"{url}&page=3",
            CreateHttpResponseFactory(content: $"[\"{fifthItem}\"]"),
            handlerMock);

        using var httpClient = new HttpClient(handlerMock.Object);
        var githubClient = new GithubClient(_mockOctoLogger.Object, httpClient, null, _retryPolicy, _dateTimeProvider.Object, PERSONAL_ACCESS_TOKEN);

        // Act
        var results = await githubClient.GetAllAsync(url).ToListAsync();

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
        const string url = "https://api.github.com/search/code?q=addClass+user%3Amozilla";

        var handlerMock = new Mock<HttpMessageHandler>();

        // first request
        MockHttpHandler(
            req => req.Method == HttpMethod.Get && req.RequestUri.ToString() == url,
            CreateHttpResponseFactory(
                content: "[\"first\"]",
                headers: new[]
                {
                    ("Link", $"<{url}&page=2>; rel=\"next\", " +
                             $"<{url}&page=2>; rel=\"last\""
                    )
                }),
            handlerMock);

        // second request
        MockHttpHandler(
            req => req.Method == HttpMethod.Get && req.RequestUri.ToString() == $"{url}&page=2",
            CreateHttpResponseFactory(content: "[\"second\"]"),
            handlerMock);

        using var httpClient = new HttpClient(handlerMock.Object);
        var githubClient = new GithubClient(_mockOctoLogger.Object, httpClient, null, _retryPolicy, _dateTimeProvider.Object, PERSONAL_ACCESS_TOKEN);

        // Act
        await githubClient.GetAllAsync(url).ToListAsync();

        // Assert
        _mockOctoLogger.Verify(m => m.LogVerbose(It.Is<string>(actual => actual == $"HTTP GET: {url}")));
        _mockOctoLogger.Verify(m => m.LogVerbose(It.Is<string>(actual => actual == $"HTTP GET: {url}&page=2")));
    }

    [Fact]
    public async Task GetAllAsync_Logs_The_GitHub_Request_Id_Header_Value_Per_Each_Page_Request()
    {
        // Arrange
        const string url = "https://api.github.com/search/code?q=addClass+user%3Amozilla";
        const string firstGithubRequestId = "123-456";
        const string secondGithubRequestId = "456-789";

        var handlerMock = new Mock<HttpMessageHandler>();

        // first request
        MockHttpHandler(
            req => req.Method == HttpMethod.Get && req.RequestUri.ToString() == url,
            CreateHttpResponseFactory(
                content: "[\"first\"]",
                headers: new[]
                {
                    ("Link", $"<{url}&page=2>; rel=\"next\", " +
                             $"<{url}&page=2>; rel=\"last\""
                    ),
                    ("X-GitHub-Request-Id", firstGithubRequestId)
                }),
            handlerMock);

        // second request
        MockHttpHandler(
            req => req.Method == HttpMethod.Get && req.RequestUri.ToString() == $"{url}&page=2",
            CreateHttpResponseFactory(
                content: "[\"second\"]",
                headers: new[] { ("X-GitHub-Request-Id", secondGithubRequestId) }),
            handlerMock);

        using var httpClient = new HttpClient(handlerMock.Object);
        var githubClient = new GithubClient(_mockOctoLogger.Object, httpClient, null, _retryPolicy, _dateTimeProvider.Object, PERSONAL_ACCESS_TOKEN);

        // Act
        await githubClient.GetAllAsync(url).ToListAsync();

        // Assert
        _mockOctoLogger.Verify(m => m.LogVerbose(It.Is<string>(actual => actual == $"GITHUB REQUEST ID: {firstGithubRequestId}")));
        _mockOctoLogger.Verify(m => m.LogVerbose(It.Is<string>(actual => actual == $"GITHUB REQUEST ID: {secondGithubRequestId}")));
    }

    [Fact]
    public async Task GetAllAsync_Logs_The_Response_Per_Each_Page_Request()
    {
        // Arrange
        const string url = "https://api.github.com/search/code?q=addClass+user%3Amozilla";

        const string firstResponseContent = "[\"first\"]";
        const string secondResponseContent = "[\"second\"]";

        var handlerMock = new Mock<HttpMessageHandler>();

        // first request
        MockHttpHandler(
            req => req.Method == HttpMethod.Get && req.RequestUri.ToString() == url,
            CreateHttpResponseFactory(
                content: firstResponseContent,
                headers: new[]
                {
                    ("Link", $"<{url}&page=2>; rel=\"next\", " +
                             $"<{url}&page=2>; rel=\"last\""
                    )
                }),
            handlerMock);

        // second request
        MockHttpHandler(
            req => req.Method == HttpMethod.Get && req.RequestUri.ToString() == $"{url}&page=2",
            CreateHttpResponseFactory(content: secondResponseContent),
            handlerMock);

        using var httpClient = new HttpClient(handlerMock.Object);
        var githubClient = new GithubClient(_mockOctoLogger.Object, httpClient, null, _retryPolicy, _dateTimeProvider.Object, PERSONAL_ACCESS_TOKEN);

        // Act
        await githubClient.GetAllAsync(url).ToListAsync();

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
        const string url = "https://api.github.com/search/code?q=addClass+user%3Amozilla";

        var handlerMock = new Mock<HttpMessageHandler>();

        // first request
        MockHttpHandler(
            req => req.Method == HttpMethod.Get && req.RequestUri.ToString() == url,
            CreateHttpResponseFactory(
                content: "[\"first\"]",
                headers: new[]
                {
                    ("Link", $"<{url}&page=2>; rel=\"next\", " +
                             $"<{url}&page=2>; rel=\"last\""
                    )
                }),
            handlerMock);

        // second request
        MockHttpHandler(
            req => req.Method == HttpMethod.Get && req.RequestUri.ToString() == $"{url}&page=2",
            CreateHttpResponseFactory(statusCode: HttpStatusCode.InternalServerError),
            handlerMock);

        using var httpClient = new HttpClient(handlerMock.Object);
        var githubClient = new GithubClient(_mockOctoLogger.Object, httpClient, null, _retryPolicy, _dateTimeProvider.Object, PERSONAL_ACCESS_TOKEN);

        // Act, Assert
        await FluentActions
            .Invoking(async () => await githubClient.GetAllAsync(url).ToListAsync())
            .Should()
            .ThrowExactlyAsync<HttpRequestException>();
    }

    [Fact]
    public async Task GetAllAsync_Retries_On_Non_Success()
    {
        // Arrange
        const string url = "https://api.github.com/search/code?q=addClass+user%3Amozilla";
        const string firstItem = "first";
        const string secondItem = "second";

        var handlerMock = new Mock<HttpMessageHandler>();

        // first request
        handlerMock
            .Protected()
            .SetupSequence<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.Is<HttpRequestMessage>(req => req.Method == HttpMethod.Get),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(CreateHttpResponseFactory(statusCode: HttpStatusCode.InternalServerError, content: "error"))
            .ReturnsAsync(CreateHttpResponseFactory(
                content: $"[\"{firstItem}\"]",
                headers: new[]
                {
                    ("Link", $"<{url}&page=2>; rel=\"next\", " +
                             $"<{url}&page=2>; rel=\"last\""
                    )
                }));

        // second request
        MockHttpHandler(
            req => req.Method == HttpMethod.Get && req.RequestUri.ToString() == $"{url}&page=2",
            CreateHttpResponseFactory(content: $"[\"{secondItem}\"]"),
            handlerMock);

        using var httpClient = new HttpClient(handlerMock.Object);
        var githubClient = new GithubClient(_mockOctoLogger.Object, httpClient, null, _retryPolicy, _dateTimeProvider.Object, PERSONAL_ACCESS_TOKEN);

        // Act
        var results = await githubClient.GetAllAsync(url).Select(x => x.Value<string>()).ToListAsync();

        // Assert
        results.Should().HaveCount(2);
        results.Should().BeEquivalentTo(firstItem, secondItem);
    }

    [Fact]
    public async Task GetAllAsync_Should_Use_Result_Collection_Selector()
    {
        // Arrange
        const string url = "https://api.github.com/orgs/foo/external-groups";

        const string firstGroupId = "123";
        const string firstGroupName = "Octocat readers";
        const string secondGroupId = "456";
        const string secondGroupName = "Octocat admins";
        const string response = $@"
            {{
                ""groups"": [
                    {{
                       ""group_id"": ""{firstGroupId}"",
                       ""group_name"": ""{firstGroupName}"",
                       ""updated_at"": ""2021-01-24T11:31:04-06:00""
                    }},
                    {{
                       ""group_id"": ""{secondGroupId}"",
                       ""group_name"": ""{secondGroupName}"",
                       ""updated_at"": ""2021-03-24T11:31:04-06:00""
                    }},
                ]
            }}";

        var handlerMock = MockHttpHandler(req => req.Method == HttpMethod.Get && req.RequestUri.ToString() == url, CreateHttpResponseFactory(content: response));

        using var httpClient = new HttpClient(handlerMock.Object);
        var githubClient = new GithubClient(_mockOctoLogger.Object, httpClient, null, _retryPolicy, _dateTimeProvider.Object, PERSONAL_ACCESS_TOKEN);

        // Act
        var results = await githubClient.GetAllAsync(url, data => (JArray)data["groups"]).ToListAsync();

        // Assert
        results.Should().HaveCount(2);
        results[0]["group_id"].Value<string>().Should().Be(firstGroupId);
        results[0]["group_name"].Value<string>().Should().Be(firstGroupName);
        results[1]["group_id"].Value<string>().Should().Be(secondGroupId);
        results[1]["group_name"].Value<string>().Should().Be(secondGroupName);
    }

    [Fact]
    public async Task PostGraphQLWithPaginationAsync_Should_Return_All_Pages()
    {
        // Arrange
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
        var thirdRequestBody = new { query, variables = thirdRequestVariables };

        var handlerMock = new Mock<HttpMessageHandler>();

        // first request
        MockHttpHandler(
            req => req.Method == HttpMethod.Post && req.RequestUri.ToString() == URL && req.Content.ReadAsStringAsync().Result == firstRequestBody.ToJson(),
            CreateHttpResponseFactory(content: firstResponseContent.ToJson()),
            handlerMock);

        // second request
        MockHttpHandler(
            req => req.Method == HttpMethod.Post && req.RequestUri.ToString() == URL && req.Content.ReadAsStringAsync().Result == secondRequestBody.ToJson(),
            CreateHttpResponseFactory(content: secondResponseContent.ToJson()),
            handlerMock);

        // third request
        MockHttpHandler(
            req => req.Method == HttpMethod.Post && req.RequestUri.ToString() == URL && req.Content.ReadAsStringAsync().Result == thirdRequestBody.ToJson(),
            CreateHttpResponseFactory(content: thirdResponseContent.ToJson()),
            handlerMock);

        using var httpClient = new HttpClient(handlerMock.Object);
        var githubClient = new GithubClient(_mockOctoLogger.Object, httpClient, null, _retryPolicy, _dateTimeProvider.Object, PERSONAL_ACCESS_TOKEN);

        // Act
        var result = await githubClient.PostGraphQLWithPaginationAsync(
                URL,
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

        var handlerMock = MockHttpHandler(
            req => req.Method == HttpMethod.Post && req.RequestUri.ToString() == URL && req.Content.ReadAsStringAsync().Result == expectedRequestBody.ToJson(),
            CreateHttpResponseFactory(content: responseContent.ToJson()));

        using var httpClient = new HttpClient(handlerMock.Object);
        var githubClient = new GithubClient(_mockOctoLogger.Object, httpClient, null, _retryPolicy, _dateTimeProvider.Object, PERSONAL_ACCESS_TOKEN);

        // Act
        var result = await githubClient.PostGraphQLWithPaginationAsync(
                URL,
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

        var handlerMock = MockHttpHandler(
            req => req.Method == HttpMethod.Post && req.RequestUri.ToString() == URL && req.Content.ReadAsStringAsync().Result == expectedRequestBody.ToJson(),
            CreateHttpResponseFactory(content: responseContent.ToJson()));

        using var httpClient = new HttpClient(handlerMock.Object);
        var githubClient = new GithubClient(_mockOctoLogger.Object, httpClient, null, _retryPolicy, _dateTimeProvider.Object, PERSONAL_ACCESS_TOKEN);

        // Act
        var result = await githubClient.PostGraphQLWithPaginationAsync(
                URL,
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

        var handlerMock = MockHttpHandler(
            req => req.Method == HttpMethod.Post && req.RequestUri.ToString() == URL && req.Content.ReadAsStringAsync().Result == expectedRequestBody.ToJson(),
            CreateHttpResponseFactory(content: responseContent.ToJson()));

        using var httpClient = new HttpClient(handlerMock.Object);
        var githubClient = new GithubClient(_mockOctoLogger.Object, httpClient, null, _retryPolicy, _dateTimeProvider.Object, PERSONAL_ACCESS_TOKEN);

        // Act
        var result = await githubClient.PostGraphQLWithPaginationAsync(
                URL,
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
        using var httpClient = new HttpClient();
        var githubClient = new GithubClient(null, httpClient, null, _retryPolicy, _dateTimeProvider.Object, PERSONAL_ACCESS_TOKEN);

        // Act, Assert
        await githubClient
            .Invoking(async client => await client.PostGraphQLWithPaginationAsync(URL, "", null, null).ToListAsync())
            .Should()
            .ThrowAsync<ArgumentNullException>()
            .WithParameterName("resultCollectionSelector");
    }

#pragma warning disable CA1506
    [Fact]
    public async Task PostGraphQLWithPaginationAsync_Returns_When_PageInfo_Is_Missing()
    {
        // Arrange
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

        var handlerMock = MockHttpHandler(
            req => req.Method == HttpMethod.Post && req.RequestUri.ToString() == URL && req.Content.ReadAsStringAsync().Result == requestBody.ToJson(),
            CreateHttpResponseFactory(content: responseContent.ToJson()));

        using var httpClient = new HttpClient(handlerMock.Object);
        var githubClient = new GithubClient(_mockOctoLogger.Object, httpClient, null, _retryPolicy, _dateTimeProvider.Object, PERSONAL_ACCESS_TOKEN);

        // Act
        var result = await githubClient.PostGraphQLWithPaginationAsync(
                URL,
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
#pragma warning restore

#pragma warning disable CA1506
    [Fact]
    public async Task PostGraphQLWithPaginationAsync_Does_Not_Continue_When_HasNextPage_Is_Missing()
    {
        // Arrange
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

        var handlerMock = MockHttpHandler(
            req => req.Method == HttpMethod.Post && req.RequestUri.ToString() == URL && req.Content.ReadAsStringAsync().Result == requestBody.ToJson(),
            CreateHttpResponseFactory(content: responseContent.ToJson()));

        using var httpClient = new HttpClient(handlerMock.Object);
        var githubClient = new GithubClient(_mockOctoLogger.Object, httpClient, null, _retryPolicy, _dateTimeProvider.Object, PERSONAL_ACCESS_TOKEN);

        // Act
        var result = await githubClient.PostGraphQLWithPaginationAsync(
                URL,
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
#pragma warning restore

    [Fact]
    public async Task PostGraphQLWithPaginationAsync_Throws_If_PageInfo_Selector_Is_Null()
    {
        // Arrange
        using var httpClient = new HttpClient();
        var githubClient = new GithubClient(null, httpClient, null, _retryPolicy, _dateTimeProvider.Object, PERSONAL_ACCESS_TOKEN);

        // Act, Assert
        await githubClient
            .Invoking(async client => await client.PostGraphQLWithPaginationAsync(URL, "", x => (JArray)x["item"], null).ToListAsync())
            .Should()
            .ThrowAsync<ArgumentNullException>()
            .WithParameterName("pageInfoSelector");
    }

    [Fact]
    public async Task PostGraphQLWithPaginationAsync_Logs_The_GitHub_Request_Id_Header_Value_Per_Each_Page()
    {
        // Arrange
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
        const string firstGithubRequestId = "123-456";

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
        const string secondGithubRequestId = "456-789";
        var secondRequestBody = new { query, variables = secondRequestVariables };

        var handlerMock = new Mock<HttpMessageHandler>();

        // first request
        MockHttpHandler(
            req => req.Method == HttpMethod.Post && req.RequestUri.ToString() == URL && req.Content.ReadAsStringAsync().Result == firstRequestBody.ToJson(),
            CreateHttpResponseFactory(content: firstResponseContent.ToJson(), headers: new[] { ("X-GitHub-Request-Id", firstGithubRequestId) }),
            handlerMock);

        // second request
        MockHttpHandler(
            req => req.Method == HttpMethod.Post && req.RequestUri.ToString() == URL && req.Content.ReadAsStringAsync().Result == secondRequestBody.ToJson(),
            CreateHttpResponseFactory(content: secondResponseContent.ToJson(), headers: new[] { ("X-GitHub-Request-Id", secondGithubRequestId) }),
            handlerMock);

        using var httpClient = new HttpClient(handlerMock.Object);
        var githubClient = new GithubClient(_mockOctoLogger.Object, httpClient, null, _retryPolicy, _dateTimeProvider.Object, PERSONAL_ACCESS_TOKEN);

        // Act
        _ = await githubClient.PostGraphQLWithPaginationAsync(
                URL,
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
        const string currentVersion = "1.1.1";
        const string versionComments = "(COMMENTS)";

        using var httpClient = new HttpClient();

        var mockVersionProvider = new Mock<IVersionProvider>();
        mockVersionProvider.Setup(m => m.GetCurrentVersion()).Returns("1.1.1");
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

    [Fact]
    public async Task PostAsync_Handles_Secondary_Rate_Limit_With_429_Status()
    {
        // Arrange
        var handlerMock = new Mock<HttpMessageHandler>();
        handlerMock
            .Protected()
            .SetupSequence<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.Is<HttpRequestMessage>(req => req.Method == HttpMethod.Post),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(CreateHttpResponseFactory(
                statusCode: HttpStatusCode.TooManyRequests,
                content: "Too many requests",
                headers: new[] { ("Retry-After", "1") })())
            .ReturnsAsync(CreateHttpResponseFactory(content: "SUCCESS_RESPONSE")());

        using var httpClient = new HttpClient(handlerMock.Object);
        var githubClient = new GithubClient(_mockOctoLogger.Object, httpClient, null, _retryPolicy, _dateTimeProvider.Object, PERSONAL_ACCESS_TOKEN);

        // Act
        var result = await githubClient.PostAsync("http://example.com", _rawRequestBody);

        // Assert
        result.Should().Be("SUCCESS_RESPONSE");
        _mockOctoLogger.Verify(m => m.LogWarning(It.Is<string>(s => s.Contains("Secondary rate limit detected"))), Times.Once);
    }

    [Fact]
    public async Task GetAsync_Handles_Secondary_Rate_Limit_With_Forbidden_Status()
    {
        // Arrange
        var handlerMock = new Mock<HttpMessageHandler>();
        handlerMock
            .Protected()
            .SetupSequence<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.Is<HttpRequestMessage>(req => req.Method == HttpMethod.Get),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(CreateHttpResponseFactory(
                statusCode: HttpStatusCode.Forbidden,
                content: "You have triggered an abuse detection mechanism",
                headers: new[] { ("Retry-After", "2") })())
            .ReturnsAsync(CreateHttpResponseFactory(content: "SUCCESS_RESPONSE")());

        using var httpClient = new HttpClient(handlerMock.Object);
        var githubClient = new GithubClient(_mockOctoLogger.Object, httpClient, null, _retryPolicy, _dateTimeProvider.Object, PERSONAL_ACCESS_TOKEN);

        // Act
        var result = await githubClient.GetAsync("http://example.com");

        // Assert
        result.Should().Be("SUCCESS_RESPONSE");
        _mockOctoLogger.Verify(m => m.LogWarning("Secondary rate limit detected (attempt 1/3). Waiting 2 seconds before retrying..."), Times.Once);
    }

    [Fact]
    public async Task SendAsync_Uses_Exponential_Backoff_When_No_Retry_Headers()
    {
        // Arrange
        var handlerMock = new Mock<HttpMessageHandler>();
        handlerMock
            .Protected()
            .SetupSequence<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.Is<HttpRequestMessage>(req => req.Method == HttpMethod.Patch),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(CreateHttpResponseFactory(
                statusCode: HttpStatusCode.Forbidden,
                content: "abuse detection mechanism")())
            .ReturnsAsync(CreateHttpResponseFactory(
                statusCode: HttpStatusCode.Forbidden,
                content: "abuse detection mechanism")())
            .ReturnsAsync(CreateHttpResponseFactory(content: "SUCCESS_RESPONSE")());

        using var httpClient = new HttpClient(handlerMock.Object);
        var githubClient = new GithubClient(_mockOctoLogger.Object, httpClient, null, _retryPolicy, _dateTimeProvider.Object, PERSONAL_ACCESS_TOKEN);

        // Act
        var result = await githubClient.PatchAsync("http://example.com", _rawRequestBody);

        // Assert
        result.Should().Be("SUCCESS_RESPONSE");
        _mockOctoLogger.Verify(m => m.LogWarning("Secondary rate limit detected (attempt 1/3). Waiting 60 seconds before retrying..."), Times.Once);
        _mockOctoLogger.Verify(m => m.LogWarning("Secondary rate limit detected (attempt 2/3). Waiting 120 seconds before retrying..."), Times.Once);
    }

    [Fact]
    public async Task SendAsync_Throws_Exception_After_Max_Secondary_Rate_Limit_Retries()
    {
        // Arrange
        var handlerMock = new Mock<HttpMessageHandler>();
        handlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.Is<HttpRequestMessage>(req => req.Method == HttpMethod.Delete),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(CreateHttpResponseFactory(
                statusCode: HttpStatusCode.TooManyRequests,
                content: "Too many requests")());

        using var httpClient = new HttpClient(handlerMock.Object);
        var githubClient = new GithubClient(_mockOctoLogger.Object, httpClient, null, _retryPolicy, _dateTimeProvider.Object, PERSONAL_ACCESS_TOKEN);

        // Act & Assert
        await FluentActions
            .Invoking(async () => await githubClient.DeleteAsync("http://example.com"))
            .Should()
            .ThrowExactlyAsync<OctoshiftCliException>()
            .WithMessage("Secondary rate limit exceeded. Maximum retries (3) reached. Please wait before retrying your request.");

        // Verify all retry attempts were logged
        _mockOctoLogger.Verify(m => m.LogWarning("Secondary rate limit detected (attempt 1/3). Waiting 60 seconds before retrying..."), Times.Once);
        _mockOctoLogger.Verify(m => m.LogWarning("Secondary rate limit detected (attempt 2/3). Waiting 120 seconds before retrying..."), Times.Once);
        _mockOctoLogger.Verify(m => m.LogWarning("Secondary rate limit detected (attempt 3/3). Waiting 240 seconds before retrying..."), Times.Once);
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

    private Mock<HttpMessageHandler> MockHttpHandlerForGet(Func<HttpResponseMessage> httpResponseFactory = null) =>
        MockHttpHandler(req => req.Method == HttpMethod.Get, httpResponseFactory);

    private Mock<HttpMessageHandler> MockHttpHandlerForDelete(Func<HttpResponseMessage> httpResponseFactory = null) =>
        MockHttpHandler(req => req.Method == HttpMethod.Delete, httpResponseFactory);

    private Mock<HttpMessageHandler> MockHttpHandlerForPost(Func<HttpResponseMessage> httpResponseFactory = null) =>
        MockHttpHandler(req => req.Method == HttpMethod.Post && req.Content.ReadAsStringAsync().Result == EXPECTED_JSON_REQUEST_BODY, httpResponseFactory);

    private Mock<HttpMessageHandler> MockHttpHandlerForGraphQLPost(Func<HttpResponseMessage> httpResponseFactory = null) =>
        MockHttpHandler(req =>
            req.Method == HttpMethod.Post, httpResponseFactory ?? CreateHttpResponseFactory(content: EXPECTED_GRAPHQL_JSON_RESPONSE_BODY));

    private Mock<HttpMessageHandler> MockHttpHandlerForPut(Func<HttpResponseMessage> httpResponseFactory = null) =>
        MockHttpHandler(req =>
                req.Method == HttpMethod.Put && req.Content.ReadAsStringAsync().Result == EXPECTED_JSON_REQUEST_BODY,
            httpResponseFactory);

    private Mock<HttpMessageHandler> MockHttpHandlerForPatch(Func<HttpResponseMessage> httpResponseFactory = null) =>
        MockHttpHandler(req =>
                req.Method == HttpMethod.Patch && req.Content.ReadAsStringAsync().Result == EXPECTED_JSON_REQUEST_BODY,
            httpResponseFactory);

    private Mock<HttpMessageHandler> MockHttpHandler(
        Func<HttpRequestMessage, bool> requestMatcher,
        Func<HttpResponseMessage> httpResponseFactory = null,
        Mock<HttpMessageHandler> handlerMock = null)
    {
        handlerMock ??= new Mock<HttpMessageHandler>();
        handlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.Is<HttpRequestMessage>(x => requestMatcher(x)),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(httpResponseFactory ?? CreateHttpResponseFactory(content: EXPECTED_RESPONSE_CONTENT));
        return handlerMock;
    }

    private Func<HttpResponseMessage> CreateHttpResponseFactory(
        HttpStatusCode statusCode = HttpStatusCode.OK,
        string content = null,
        IEnumerable<(string Name, string Value)> headers = null) => () =>
    {
        var httpResponse = new HttpResponseMessage(statusCode);

        if (content.HasValue())
        {
            httpResponse.Content = new StringContent(content!);
        }

        foreach (var (name, value) in headers.ToEmptyEnumerableIfNull())
        {
            httpResponse.Headers.Add(name, value);
        }

        return httpResponse;
    };
}
