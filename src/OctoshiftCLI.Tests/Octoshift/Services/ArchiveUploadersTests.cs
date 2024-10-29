using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using FluentAssertions;
using Moq;
using OctoshiftCLI.Extensions;
using OctoshiftCLI.Services;
using OctoshiftCLI.Tests;
using Xunit;

public class ArchiveUploaderTests
{
    private readonly Mock<GithubClient> _githubClientMock;
    private readonly Mock<OctoLogger> _logMock;
    private readonly ArchiveUploader _archiveUploader;

    public ArchiveUploaderTests()
    {
        _logMock = TestHelpers.CreateMock<OctoLogger>();
        _githubClientMock = TestHelpers.CreateMock<GithubClient>();
        _archiveUploader = new ArchiveUploader(_githubClientMock.Object, _logMock.Object);
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
        const string baseUrl = "https://uploads.github.com/organizations";
        const string guid = "c9dbd27b-f190-4fe4-979f-d0b7c9b0fcb3";

        var startUploadBody = new { content_type = "application/octet-stream", name = archiveName, size = contentSize };

        const string initialUploadUrl = $"{orgDatabaseId}/gei/archive/blobs/uploads";
        const string firstUploadUrl = $"{orgDatabaseId}/gei/archive/blobs/uploads?part_number=1&guid={guid}";
        const string secondUploadUrl = $"{orgDatabaseId}/gei/archive/blobs/uploads?part_number=2&guid={guid}";
        const string thirdUploadUrl = $"{orgDatabaseId}/gei/archive/blobs/uploads?part_number=3&guid={guid}";
        const string lastUrl = $"{orgDatabaseId}/gei/archive/blobs/uploads/last";

        // Mocking the initial POST request to initiate multipart upload
        _githubClientMock
            .Setup(m => m.PostWithFullResponseAsync($"{baseUrl}/{initialUploadUrl}", It.Is<object>(x => x.ToJson() == startUploadBody.ToJson()), null))
            .ReturnsAsync((It.IsAny<string>(), new[] { new KeyValuePair<string, IEnumerable<string>>("Location", new[] { firstUploadUrl }) }));

        // Mocking PATCH requests for each part upload
        _githubClientMock // first PATCH request
            .Setup(m => m.PatchWithFullResponseAsync($"{baseUrl}/{firstUploadUrl}",
                It.Is<HttpContent>(x => x.ReadAsByteArrayAsync().Result.ToJson() == new byte[] { 1, 2, 3, 4 }.ToJson()), null))
            .ReturnsAsync((It.IsAny<string>(), new[] { new KeyValuePair<string, IEnumerable<string>>("Location", new[] { secondUploadUrl }) }));
        _githubClientMock // second PATCH request
            .Setup(m => m.PatchWithFullResponseAsync($"{baseUrl}/{secondUploadUrl}",
                It.Is<HttpContent>(x => x.ReadAsByteArrayAsync().Result.ToJson() == new byte[] { 5, 6, 7, 8 }.ToJson()), null))
            .ReturnsAsync((It.IsAny<string>(), new[] { new KeyValuePair<string, IEnumerable<string>>("Location", new[] { thirdUploadUrl }) }));
        _githubClientMock // third PATCH request
            .Setup(m => m.PatchWithFullResponseAsync($"{baseUrl}/{thirdUploadUrl}",
                It.Is<HttpContent>(x => x.ReadAsByteArrayAsync().Result.ToJson() == new byte[] { 9, 10 }.ToJson()), null))
            .ReturnsAsync((It.IsAny<string>(), new[] { new KeyValuePair<string, IEnumerable<string>>("Location", new[] { lastUrl }) }));

        // Mocking the final PUT request to complete the multipart upload
        _githubClientMock
            .Setup(m => m.PutAsync($"{baseUrl}/{lastUrl}", "", null))
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
}
