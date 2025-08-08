using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using FluentAssertions;
using Moq;
using Newtonsoft.Json.Linq;
using OctoshiftCLI.Extensions;
using OctoshiftCLI.Services;
using Xunit;

namespace OctoshiftCLI.Tests.Octoshift.Services;

public class ArchiveUploaderTests
{
    private const string UPLOADS_URL = "https://uploads.github.com";

    private readonly Mock<GithubClient> _githubClientMock;
    private readonly Mock<OctoLogger> _logMock;
    private readonly ArchiveUploader _archiveUploader;

    public ArchiveUploaderTests()
    {
        _logMock = TestHelpers.CreateMock<OctoLogger>();
        _githubClientMock = TestHelpers.CreateMock<GithubClient>();
        var retryPolicy = new RetryPolicy(_logMock.Object) { _httpRetryInterval = 1, _retryInterval = 0 };
        _archiveUploader = new ArchiveUploader(_githubClientMock.Object, UPLOADS_URL, _logMock.Object, retryPolicy);
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
        const string geiUri = $"gei://archive/{guid}";

        var startUploadBody = new { content_type = "application/octet-stream", name = archiveName, size = contentSize };

        var completeUploadResponse = new JObject
        {
            ["guid"] = guid,
            ["node_id"] = "global-relay-id",
            ["name"] = archiveName,
            ["size"] = largeContent.Length,
            ["uri"] = geiUri,
            ["created_at"] = "2025-06-23T17:13:02.818-07:00"
        };

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
            .ReturnsAsync(completeUploadResponse.ToString());

        // act
        var result = await _archiveUploader.Upload(archiveContent, archiveName, orgDatabaseId);

        // assert
        result.Should().Be(geiUri);

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
        const string geiUri = $"gei://archive/{guid}";

        var startUploadBody = new { content_type = "application/octet-stream", name = archiveName, size = largeContent.Length };

        var completeUploadResponse = new JObject
        {
            ["guid"] = guid,
            ["node_id"] = "global-relay-id",
            ["name"] = archiveName,
            ["size"] = largeContent.Length,
            ["uri"] = geiUri,
            ["created_at"] = "2025-06-23T17:13:02.818-07:00"
        };

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
            .ReturnsAsync(completeUploadResponse.ToString());

        // act
        var result = await _archiveUploader.Upload(archiveContent, archiveName, orgDatabaseId);

        // assert
        Assert.Equal(geiUri, result);

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
        const string geiUri = $"gei://archive/{guid}";

        var startUploadBody = new { content_type = "application/octet-stream", name = archiveName, size = largeContent.Length };

        var completeUploadResponse = new JObject
        {
            ["guid"] = guid,
            ["node_id"] = "global-relay-id",
            ["name"] = archiveName,
            ["size"] = largeContent.Length,
            ["uri"] = geiUri,
            ["created_at"] = "2025-06-23T17:13:02.818-07:00"
        };

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
            .ReturnsAsync(completeUploadResponse.ToString());

        // act
        var result = await _archiveUploader.Upload(archiveContent, archiveName, orgDatabaseId);

        // assert
        Assert.Equal(geiUri, result);

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
        const string geiUri = $"gei://archive/{guid}";

        var startUploadBody = new { content_type = "application/octet-stream", name = archiveName, size = largeContent.Length };

        var completeUploadResponse = new JObject
        {
            ["guid"] = guid,
            ["node_id"] = "global-relay-id",
            ["name"] = archiveName,
            ["size"] = largeContent.Length,
            ["uri"] = geiUri,
            ["created_at"] = "2025-06-23T17:13:02.818-07:00"
        };

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
            .ReturnsAsync(completeUploadResponse.ToString());

        // act
        var result = await _archiveUploader.Upload(archiveContent, archiveName, orgDatabaseId);

        // assert
        Assert.Equal(geiUri, result);

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
