using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using FluentAssertions;
using Moq;
using OctoshiftCLI.Extensions;
using OctoshiftCLI.Services;
using Xunit;

namespace OctoshiftCLI.Tests.Octoshift.Services;

public class ArchiveUploaderTests : IDisposable
{
    private bool _disposed;

    private readonly Mock<GithubClient> _githubClientMock;
    private readonly Mock<OctoLogger> _logMock;
    private readonly ArchiveUploader _archiveUploader;
    private const string ENV_VAR_NAME = "GITHUB_OWNED_STORAGE_MULTIPART_BYTES";

    public ArchiveUploaderTests()
    {
        _logMock = TestHelpers.CreateMock<OctoLogger>();
        _githubClientMock = TestHelpers.CreateMock<GithubClient>();
        var retryPolicy = new RetryPolicy(_logMock.Object) { _httpRetryInterval = 1, _retryInterval = 0 };
        _archiveUploader = new ArchiveUploader(_githubClientMock.Object, _logMock.Object, retryPolicy);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (!_disposed)
        {
            if (disposing)
            {
                // Clean up environment variable after each test
                Environment.SetEnvironmentVariable(ENV_VAR_NAME, null);
            }
            _disposed = true;
        }
    }
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    [Fact]
    public async Task Upload_Should_Throw_ArgumentNullException_When_Archive_Content_Is_Null()
    {
        // Arrange
        Stream nullStream = null;
        var archiveName = "test-archive.zip";
        var orgDatabaseId = "12345";

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(() => _archiveUploader.Upload(nullStream, archiveName, orgDatabaseId));
    }

    [Fact]
    public void Constructor_Should_Use_Valid_Environment_Variable_Value()
    {
        // Arrange
        var customSize = 10 * 1024 * 1024; // 10 MiB
        Environment.SetEnvironmentVariable(ENV_VAR_NAME, customSize.ToString());

        var logMock = TestHelpers.CreateMock<OctoLogger>();
        var githubClientMock = TestHelpers.CreateMock<GithubClient>();
        var retryPolicy = new RetryPolicy(logMock.Object);

        // Act
        var archiveUploader = new ArchiveUploader(githubClientMock.Object, logMock.Object, retryPolicy);

        // Assert
        archiveUploader._streamSizeLimit.Should().Be(customSize);
        logMock.Verify(x => x.LogInformation($"Stream size limit set to {customSize} bytes."), Times.Once);
    }

    [Fact]
    public void Constructor_Should_Use_Default_When_Environment_Variable_Not_Set()
    {
        // Arrange
        Environment.SetEnvironmentVariable(ENV_VAR_NAME, null);
        var defaultSize = 100 * 1024 * 1024; // 100 MiB

        var logMock = TestHelpers.CreateMock<OctoLogger>();
        var githubClientMock = TestHelpers.CreateMock<GithubClient>();
        var retryPolicy = new RetryPolicy(logMock.Object);

        // Act
        var archiveUploader = new ArchiveUploader(githubClientMock.Object, logMock.Object, retryPolicy);

        // Assert
        archiveUploader._streamSizeLimit.Should().Be(defaultSize);
    }

    [Fact]
    public void Constructor_Should_Use_Default_When_Environment_Variable_Is_Invalid()
    {
        // Arrange
        Environment.SetEnvironmentVariable(ENV_VAR_NAME, "invalid_value");
        var defaultSize = 100 * 1024 * 1024; // 100 MiB

        var logMock = TestHelpers.CreateMock<OctoLogger>();
        var githubClientMock = TestHelpers.CreateMock<GithubClient>();
        var retryPolicy = new RetryPolicy(logMock.Object);

        // Act
        var archiveUploader = new ArchiveUploader(githubClientMock.Object, logMock.Object, retryPolicy);

        // Assert
        archiveUploader._streamSizeLimit.Should().Be(defaultSize);
    }

    [Fact]
    public void Constructor_Should_Use_Default_When_Environment_Variable_Is_Zero()
    {
        // Arrange
        Environment.SetEnvironmentVariable(ENV_VAR_NAME, "0");
        var defaultSize = 100 * 1024 * 1024; // 100 MiB

        var logMock = TestHelpers.CreateMock<OctoLogger>();
        var githubClientMock = TestHelpers.CreateMock<GithubClient>();
        var retryPolicy = new RetryPolicy(logMock.Object);

        // Act
        var archiveUploader = new ArchiveUploader(githubClientMock.Object, logMock.Object, retryPolicy);

        // Assert
        archiveUploader._streamSizeLimit.Should().Be(defaultSize);
    }

    [Fact]
    public void Constructor_Should_Use_Default_When_Environment_Variable_Is_Negative()
    {
        // Arrange
        Environment.SetEnvironmentVariable(ENV_VAR_NAME, "-1000");
        var defaultSize = 100 * 1024 * 1024; // 100 MiB

        var logMock = TestHelpers.CreateMock<OctoLogger>();
        var githubClientMock = TestHelpers.CreateMock<GithubClient>();
        var retryPolicy = new RetryPolicy(logMock.Object);

        // Act
        var archiveUploader = new ArchiveUploader(githubClientMock.Object, logMock.Object, retryPolicy);

        // Assert
        archiveUploader._streamSizeLimit.Should().Be(defaultSize);
    }

    [Fact]
    public void Constructor_Should_Use_Default_And_Log_Warning_When_Environment_Variable_Below_Minimum()
    {
        // Arrange
        var belowMinimumSize = 1024 * 1024; // 1 MiB (below 5 MiB minimum)
        var defaultSize = 100 * 1024 * 1024; // 100 MiB
        var minSize = 5 * 1024 * 1024; // 5 MiB minimum
        Environment.SetEnvironmentVariable(ENV_VAR_NAME, belowMinimumSize.ToString());

        var logMock = TestHelpers.CreateMock<OctoLogger>();
        var githubClientMock = TestHelpers.CreateMock<GithubClient>();
        var retryPolicy = new RetryPolicy(logMock.Object);

        // Act
        var archiveUploader = new ArchiveUploader(githubClientMock.Object, logMock.Object, retryPolicy);

        // Assert
        archiveUploader._streamSizeLimit.Should().Be(defaultSize);
        logMock.Verify(x => x.LogWarning($"GITHUB_OWNED_STORAGE_MULTIPART_BYTES is set to {belowMinimumSize} bytes, but the minimum value is {minSize} bytes. Using default value of {defaultSize} bytes."), Times.Once);
    }

    [Fact]
    public void Constructor_Should_Accept_Value_Equal_To_Minimum()
    {
        // Arrange
        var minimumSize = 5 * 1024 * 1024; // 5 MiB minimum
        Environment.SetEnvironmentVariable(ENV_VAR_NAME, minimumSize.ToString());

        var logMock = TestHelpers.CreateMock<OctoLogger>();
        var githubClientMock = TestHelpers.CreateMock<GithubClient>();
        var retryPolicy = new RetryPolicy(logMock.Object);

        // Act
        var archiveUploader = new ArchiveUploader(githubClientMock.Object, logMock.Object, retryPolicy);

        // Assert
        archiveUploader._streamSizeLimit.Should().Be(minimumSize);
        logMock.Verify(x => x.LogInformation($"Stream size limit set to {minimumSize} bytes."), Times.Once);
    }

    [Fact]
    public void Constructor_Should_Accept_Large_Valid_Value()
    {
        // Arrange
        var largeSize = 500 * 1024 * 1024; // 500 MiB
        Environment.SetEnvironmentVariable(ENV_VAR_NAME, largeSize.ToString());

        var logMock = TestHelpers.CreateMock<OctoLogger>();
        var githubClientMock = TestHelpers.CreateMock<GithubClient>();
        var retryPolicy = new RetryPolicy(logMock.Object);

        // Act
        var archiveUploader = new ArchiveUploader(githubClientMock.Object, logMock.Object, retryPolicy);

        // Assert
        archiveUploader._streamSizeLimit.Should().Be(largeSize);
        logMock.Verify(x => x.LogInformation($"Stream size limit set to {largeSize} bytes."), Times.Once);
    }

    [Fact]
    public async Task Upload_Should_Upload_All_Chunks_When_Stream_Exceeds_Limit()
    {
        // Arrange
        _archiveUploader._streamSizeLimit = 4;

        const int contentSize = 10;
        var largeContent = new byte[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 };
        using var archiveContent = new MemoryStream(largeContent);
        const string orgDatabaseId = "1";
        const string archiveName = "test-archive";
        const string baseUrl = "https://uploads.github.com";
        const string guid = "c9dbd27b-f190-4fe4-979f-d0b7c9b0fcb3";

        var startUploadBody = new { content_type = "application/octet-stream", name = archiveName, size = contentSize };

        const string initialUploadUrl = $"/organizations/{orgDatabaseId}/gei/archive/blobs/uploads";
        const string firstUploadUrl = $"/organizations/{orgDatabaseId}/gei/archive/blobs/uploads?part_number=1&guid={guid}";
        const string secondUploadUrl = $"/organizations/{orgDatabaseId}/gei/archive/blobs/uploads?part_number=2&guid={guid}";
        const string thirdUploadUrl = $"/organizations/{orgDatabaseId}/gei/archive/blobs/uploads?part_number=3&guid={guid}";
        const string lastUrl = $"/organizations/{orgDatabaseId}/gei/archive/blobs/uploads/last";

        // Mocking the initial POST request to initiate multipart upload
        _githubClientMock
            .Setup(m => m.PostWithFullResponseAsync($"{baseUrl}{initialUploadUrl}", It.Is<object>(x => x.ToJson() == startUploadBody.ToJson()), null))
            .ReturnsAsync((It.IsAny<string>(), new[] { new KeyValuePair<string, IEnumerable<string>>("Location", new[] { firstUploadUrl }) }));

        // Mocking PATCH requests for each part upload
        _githubClientMock // first PATCH request
            .Setup(m => m.PatchWithFullResponseAsync($"{baseUrl}{firstUploadUrl}",
                It.Is<HttpContent>(x => x.ReadAsByteArrayAsync().Result.ToJson() == new byte[] { 1, 2, 3, 4 }.ToJson()), null))
            .ReturnsAsync((It.IsAny<string>(), new[] { new KeyValuePair<string, IEnumerable<string>>("Location", new[] { secondUploadUrl }) }));
        _githubClientMock // second PATCH request
            .Setup(m => m.PatchWithFullResponseAsync($"{baseUrl}{secondUploadUrl}",
                It.Is<HttpContent>(x => x.ReadAsByteArrayAsync().Result.ToJson() == new byte[] { 5, 6, 7, 8 }.ToJson()), null))
            .ReturnsAsync((It.IsAny<string>(), new[] { new KeyValuePair<string, IEnumerable<string>>("Location", new[] { thirdUploadUrl }) }));
        _githubClientMock // third PATCH request
            .Setup(m => m.PatchWithFullResponseAsync($"{baseUrl}{thirdUploadUrl}",
                It.Is<HttpContent>(x => x.ReadAsByteArrayAsync().Result.ToJson() == new byte[] { 9, 10 }.ToJson()), null))
            .ReturnsAsync((It.IsAny<string>(), new[] { new KeyValuePair<string, IEnumerable<string>>("Location", new[] { lastUrl }) }));

        // Mocking the final PUT request to complete the multipart upload
        _githubClientMock
            .Setup(m => m.PutAsync($"{baseUrl}{lastUrl}", "", null))
            .ReturnsAsync(string.Empty);

        // act
        var result = await _archiveUploader.Upload(archiveContent, archiveName, orgDatabaseId);

        // assert
        result.Should().Be($"gei://archive/{guid}");

        _githubClientMock.Verify(m => m.PostWithFullResponseAsync(It.IsAny<string>(), It.IsAny<object>(), null), Times.Once);
        _githubClientMock.Verify(m => m.PatchWithFullResponseAsync(It.IsAny<string>(), It.IsAny<object>(), null), Times.Exactly(3));
        _githubClientMock.Verify(m => m.PutAsync(It.IsAny<string>(), It.IsAny<object>(), null), Times.Once);
        _githubClientMock.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Upload_Should_Retry_Failed_Upload_Part_Patch_Requests()
    {
        // Arrange
        _archiveUploader._streamSizeLimit = 2;

        var largeContent = new byte[] { 1, 2, 3 };
        using var archiveContent = new MemoryStream(largeContent);
        const string orgDatabaseId = "1";
        const string archiveName = "test-archive";
        const string baseUrl = "https://uploads.github.com";
        const string guid = "c9dbd27b-f190-4fe4-979f-d0b7c9b0fcb3";
        const string expectedResult = $"gei://archive/{guid}";

        var startUploadBody = new { content_type = "application/octet-stream", name = archiveName, size = largeContent.Length };

        const string initialUploadUrl = $"/organizations/{orgDatabaseId}/gei/archive/blobs/uploads";
        const string firstUploadUrl = $"/organizations/{orgDatabaseId}/gei/archive/blobs/uploads?part_number=1&guid={guid}";
        const string secondUploadUrl = $"/organizations/{orgDatabaseId}/gei/archive/blobs/uploads?part_number=2&guid={guid}";
        const string lastUrl = $"/organizations/{orgDatabaseId}/gei/archive/blobs/uploads/last";

        // Mocking the initial POST request to initiate multipart upload
        _githubClientMock
            .Setup(m => m.PostWithFullResponseAsync($"{baseUrl}{initialUploadUrl}", It.Is<object>(x => x.ToJson() == startUploadBody.ToJson()), null))
            .ReturnsAsync((It.IsAny<string>(), new[] { new KeyValuePair<string, IEnumerable<string>>("Location", [firstUploadUrl]) }));

        // Mocking PATCH requests for each part upload
        _githubClientMock // first PATCH request
            .SetupSequence(m => m.PatchWithFullResponseAsync($"{baseUrl}{firstUploadUrl}",
                It.Is<HttpContent>(x => x.ReadAsByteArrayAsync().Result.ToJson() == new byte[] { 1, 2 }.ToJson()), null))
            .ThrowsAsync(new TimeoutException("The operation was canceled."))
            .ThrowsAsync(new TimeoutException("The operation was canceled."))
            .ReturnsAsync((It.IsAny<string>(), new[] { new KeyValuePair<string, IEnumerable<string>>("Location", [secondUploadUrl]) }));
        _githubClientMock // second PATCH request
                   .Setup(m => m.PatchWithFullResponseAsync($"{baseUrl}{secondUploadUrl}",
                       It.Is<HttpContent>(x => x.ReadAsByteArrayAsync().Result.ToJson() == new byte[] { 3 }.ToJson()), null))
                   .ReturnsAsync((It.IsAny<string>(), new[] { new KeyValuePair<string, IEnumerable<string>>("Location", [lastUrl]) }));

        // Mocking the final PUT request to complete the multipart upload
        _githubClientMock
            .Setup(m => m.PutAsync($"{baseUrl}{lastUrl}", "", null))
            .ReturnsAsync(string.Empty);

        // act
        var result = await _archiveUploader.Upload(archiveContent, archiveName, orgDatabaseId);

        // assert
        Assert.Equal(expectedResult, result);

        _githubClientMock.Verify(m => m.PostWithFullResponseAsync(It.IsAny<string>(), It.IsAny<object>(), null), Times.Once);
        _githubClientMock.Verify(m => m.PatchWithFullResponseAsync(It.IsAny<string>(), It.IsAny<object>(), null), Times.Exactly(4)); // 2 retries + 2 success
        _githubClientMock.Verify(m => m.PutAsync(It.IsAny<string>(), It.IsAny<object>(), null), Times.Once);
        _githubClientMock.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Upload_Should_Retry_Failed_Start_Upload_Post_Request()
    {
        // Arrange
        _archiveUploader._streamSizeLimit = 2;

        var largeContent = new byte[] { 1, 2, 3 };
        using var archiveContent = new MemoryStream(largeContent);
        const string orgDatabaseId = "1";
        const string archiveName = "test-archive";
        const string baseUrl = "https://uploads.github.com";
        const string guid = "c9dbd27b-f190-4fe4-979f-d0b7c9b0fcb3";
        const string expectedResult = $"gei://archive/{guid}";

        var startUploadBody = new { content_type = "application/octet-stream", name = archiveName, size = largeContent.Length };

        const string initialUploadUrl = $"/organizations/{orgDatabaseId}/gei/archive/blobs/uploads";
        const string firstUploadUrl = $"/organizations/{orgDatabaseId}/gei/archive/blobs/uploads?part_number=1&guid={guid}";
        const string secondUploadUrl = $"/organizations/{orgDatabaseId}/gei/archive/blobs/uploads?part_number=2&guid={guid}";
        const string lastUrl = $"/organizations/{orgDatabaseId}/gei/archive/blobs/uploads/last";

        // Mocking the initial POST request to initiate multipart upload
        _githubClientMock
            .SetupSequence(m => m.PostWithFullResponseAsync($"{baseUrl}{initialUploadUrl}", It.Is<object>(x => x.ToJson() == startUploadBody.ToJson()), null))
            .ThrowsAsync(new TimeoutException("The operation was canceled."))
            .ThrowsAsync(new TimeoutException("The operation was canceled."))
            .ReturnsAsync((It.IsAny<string>(), new[] { new KeyValuePair<string, IEnumerable<string>>("Location", [firstUploadUrl]) }));

        // Mocking PATCH requests for each part upload
        _githubClientMock // first PATCH request
            .Setup(m => m.PatchWithFullResponseAsync($"{baseUrl}{firstUploadUrl}",
                It.Is<HttpContent>(x => x.ReadAsByteArrayAsync().Result.ToJson() == new byte[] { 1, 2 }.ToJson()), null))
                    .ReturnsAsync((It.IsAny<string>(), new[] { new KeyValuePair<string, IEnumerable<string>>("Location", [secondUploadUrl]) }));

        _githubClientMock // second PATCH request
            .Setup(m => m.PatchWithFullResponseAsync($"{baseUrl}{secondUploadUrl}",
                It.Is<HttpContent>(x => x.ReadAsByteArrayAsync().Result.ToJson() == new byte[] { 3 }.ToJson()), null))
            .ReturnsAsync((It.IsAny<string>(), new[] { new KeyValuePair<string, IEnumerable<string>>("Location", [lastUrl]) }));

        // Mocking the final PUT request to complete the multipart upload
        _githubClientMock
            .Setup(m => m.PutAsync($"{baseUrl}{lastUrl}", "", null))
            .ReturnsAsync(string.Empty);

        // act
        var result = await _archiveUploader.Upload(archiveContent, archiveName, orgDatabaseId);

        // assert
        Assert.Equal(expectedResult, result);

        _githubClientMock.Verify(m => m.PostWithFullResponseAsync(It.IsAny<string>(), It.IsAny<object>(), null), Times.Exactly(3)); // 2 retries + 1 success
        _githubClientMock.Verify(m => m.PatchWithFullResponseAsync(It.IsAny<string>(), It.IsAny<object>(), null), Times.Exactly(2));
        _githubClientMock.Verify(m => m.PutAsync(It.IsAny<string>(), It.IsAny<object>(), null), Times.Once);
        _githubClientMock.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Upload_Should_Retry_Failed_Complete_Upload_Put_Request()
    {
        // Arrange
        _archiveUploader._streamSizeLimit = 2;

        var largeContent = new byte[] { 1, 2, 3 };
        using var archiveContent = new MemoryStream(largeContent);
        const string orgDatabaseId = "1";
        const string archiveName = "test-archive";
        const string baseUrl = "https://uploads.github.com";
        const string guid = "c9dbd27b-f190-4fe4-979f-d0b7c9b0fcb3";
        const string expectedResult = $"gei://archive/{guid}";

        var startUploadBody = new { content_type = "application/octet-stream", name = archiveName, size = largeContent.Length };

        const string initialUploadUrl = $"/organizations/{orgDatabaseId}/gei/archive/blobs/uploads";
        const string firstUploadUrl = $"/organizations/{orgDatabaseId}/gei/archive/blobs/uploads?part_number=1&guid={guid}";
        const string secondUploadUrl = $"/organizations/{orgDatabaseId}/gei/archive/blobs/uploads?part_number=2&guid={guid}";
        const string lastUrl = $"/organizations/{orgDatabaseId}/gei/archive/blobs/uploads/last";

        // Mocking the initial POST request to initiate multipart upload
        _githubClientMock
            .Setup(m => m.PostWithFullResponseAsync($"{baseUrl}{initialUploadUrl}", It.Is<object>(x => x.ToJson() == startUploadBody.ToJson()), null))
            .ReturnsAsync((It.IsAny<string>(), new[] { new KeyValuePair<string, IEnumerable<string>>("Location", [firstUploadUrl]) }));

        // Mocking PATCH requests for each part upload
        _githubClientMock // first PATCH request
            .Setup(m => m.PatchWithFullResponseAsync($"{baseUrl}{firstUploadUrl}",
                It.Is<HttpContent>(x => x.ReadAsByteArrayAsync().Result.ToJson() == new byte[] { 1, 2 }.ToJson()), null))
                    .ReturnsAsync((It.IsAny<string>(), new[] { new KeyValuePair<string, IEnumerable<string>>("Location", [secondUploadUrl]) }));

        _githubClientMock // second PATCH request
                   .Setup(m => m.PatchWithFullResponseAsync($"{baseUrl}{secondUploadUrl}",
                       It.Is<HttpContent>(x => x.ReadAsByteArrayAsync().Result.ToJson() == new byte[] { 3 }.ToJson()), null))
                   .ReturnsAsync((It.IsAny<string>(), new[] { new KeyValuePair<string, IEnumerable<string>>("Location", [lastUrl]) }));

        // Mocking the final PUT request to complete the multipart upload
        _githubClientMock
            .SetupSequence(m => m.PutAsync($"{baseUrl}{lastUrl}", "", null))
            .ThrowsAsync(new TimeoutException("The operation was canceled."))
            .ThrowsAsync(new TimeoutException("The operation was canceled."))
            .ReturnsAsync(string.Empty);

        // act
        var result = await _archiveUploader.Upload(archiveContent, archiveName, orgDatabaseId);

        // assert
        Assert.Equal(expectedResult, result);

        _githubClientMock.Verify(m => m.PostWithFullResponseAsync(It.IsAny<string>(), It.IsAny<object>(), null), Times.Once);
        _githubClientMock.Verify(m => m.PatchWithFullResponseAsync(It.IsAny<string>(), It.IsAny<object>(), null), Times.Exactly(2));
        _githubClientMock.Verify(m => m.PutAsync(It.IsAny<string>(), It.IsAny<object>(), null), Times.Exactly(3)); // 2 retries + 1 success
        _githubClientMock.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Upload_Should_Retry_Failed_Post_When_Not_Multipart()
    {
        // Arrange
        _archiveUploader._streamSizeLimit = 3;

        var largeContent = new byte[] { 1, 2, 3 };
        using var archiveContent = new MemoryStream(largeContent);
        const string orgDatabaseId = "1";
        const string archiveName = "test-archive";
        const string uploadUrl = $"https://uploads.github.com/organizations/{orgDatabaseId}/gei/archive?name={archiveName}";
        const string expectedResult = "gei://archive/c9dbd27b-f190-4fe4-979f-d0b7c9b0fcb3";

        _githubClientMock
            .SetupSequence(m => m.PostAsync(uploadUrl,
                It.Is<HttpContent>(x => x.ReadAsByteArrayAsync().Result.ToJson() == new byte[] { 1, 2, 3 }.ToJson()), null))
            .ThrowsAsync(new TimeoutException("The operation was canceled."))
            .ThrowsAsync(new TimeoutException("The operation was canceled."))
            .ReturnsAsync(new { uri = expectedResult }.ToJson());

        // act
        var result = await _archiveUploader.Upload(archiveContent, archiveName, orgDatabaseId);

        // assert
        Assert.Equal(expectedResult, result);

        _githubClientMock.Verify(m => m.PostAsync(It.IsAny<string>(), It.IsAny<object>(), null), Times.Exactly(3)); // 2 retries + 1 success
        _githubClientMock.VerifyNoOtherCalls();
    }
}
