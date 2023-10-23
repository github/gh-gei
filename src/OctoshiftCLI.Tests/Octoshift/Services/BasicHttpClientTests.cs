using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
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

public sealed class BasicHttpClientTests
{
    private readonly Mock<OctoLogger> _mockOctoLogger = TestHelpers.CreateMock<OctoLogger>();
    private const string EXPECTED_RESPONSE_CONTENT = "RESPONSE_CONTENT";
    private const string URL = "https://api.github.com/resource";

    public BasicHttpClientTests()
    { }

    [Fact]
    public async Task GetAsync_Returns_String_Response()
    {
        // Arrange
        using var httpClient = new HttpClient(MockHttpHandlerForGet().Object);
        var basicHttpClient = new BasicHttpClient(_mockOctoLogger.Object, httpClient, null);

        // Act
        var actualContent = await basicHttpClient.GetAsync(URL);

        // Assert
        actualContent.Should().Be(EXPECTED_RESPONSE_CONTENT);
    }

    [Fact]
    public async Task GetAsync_Logs_The_Url()
    {
        // Arrange
        using var httpClient = new HttpClient(MockHttpHandlerForGet().Object);
        var basicHttpClient = new BasicHttpClient(_mockOctoLogger.Object, httpClient, null);

        var expectedLogMessage = $"HTTP GET: {URL}";

        // Act
        await basicHttpClient.GetAsync(URL);

        // Assert
        _mockOctoLogger.Verify(m =>
            m.LogVerbose(It.Is<string>(actualLogMessage => actualLogMessage == expectedLogMessage)));
    }

    [Fact]
    public async Task GetAsync_Logs_The_Response_Status_Code_And_Content()
    {
        // Arrange
        using var httpClient = new HttpClient(MockHttpHandlerForGet().Object);
        var basicHttpClient = new BasicHttpClient(_mockOctoLogger.Object, httpClient, null);

        var expectedLogMessage = $"RESPONSE ({HttpStatusCode.OK}): {EXPECTED_RESPONSE_CONTENT}";

        // Act
        await basicHttpClient.GetAsync("http://example.com");

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
        var basicHttpClient = new BasicHttpClient(_mockOctoLogger.Object, httpClient, null);

        // Act
        // Assert
        await FluentActions
            .Invoking(async () => await basicHttpClient.GetAsync("http://example.com"))
            .Should()
            .ThrowExactlyAsync<HttpRequestException>();
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
        _ = new BasicHttpClient(null, httpClient, mockVersionProvider.Object);

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
        _ = new BasicHttpClient(null, httpClient, null);

        // Assert
        httpClient.DefaultRequestHeaders.UserAgent.Should().HaveCount(1);
        httpClient.DefaultRequestHeaders.UserAgent.ToString().Should().Be("OctoshiftCLI");
    }

    private Mock<HttpMessageHandler> MockHttpHandlerForGet(Func<HttpResponseMessage> httpResponseFactory = null) =>
        MockHttpHandler(req => req.Method == HttpMethod.Get, httpResponseFactory);

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
