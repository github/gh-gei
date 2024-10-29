using System;
using System.IO;
using System.Threading.Tasks;
using System.Net.Http;
using Moq;
using Moq.Protected;
using Xunit;
using OctoshiftCLI.Services;
using OctoshiftCLI.Tests;


public class ArchiveUploaderTests
{
    private readonly Mock<GithubClient> _clientMock;
    private readonly Mock<OctoLogger> _logMock;
    private readonly ArchiveUploader _archiveUploader;

    public ArchiveUploaderTests()
    {
        _logMock = TestHelpers.CreateMock<OctoLogger>();
        _clientMock = TestHelpers.CreateMock<GithubClient>();
        _archiveUploader = new ArchiveUploader(_clientMock.Object, _logMock.Object);
    }

    [Fact]
    public async Task Upload_ShouldThrowArgumentNullException_WhenArchiveContentIsNull()
    {
        // Arrange
        Stream nullStream = null;
        var archiveName = "test-archive.zip";
        var orgDatabaseId = "12345";

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(() => _archiveUploader.Upload(nullStream, archiveName, orgDatabaseId));
    }

    [Fact]
    public async Task Upload_ShouldCallPostAsync_WithSinglePartUpload_WhenStreamIsUnderLimit()
    {
        // Arrange
        using var smallStream = new MemoryStream(new byte[50 * 1024 * 1024]); // 50 MB, under limit
        var archiveName = "test-archive.zip";
        var orgDatabaseId = "12345";
        var expectedUri = "gei://archive/singlepart";
        var expectedResponse = $"{{ \"uri\": \"{expectedUri}\" }}";

        _clientMock
            .Setup(m => m.PostAsync(It.IsAny<string>(), It.IsAny<StreamContent>(), null))
            .ReturnsAsync(expectedResponse);

        // Act
        var result = await _archiveUploader.Upload(smallStream, archiveName, orgDatabaseId);

        // Assert
        _clientMock.Verify(c => c.PostAsync(It.Is<string>(url => url.Contains(orgDatabaseId)), It.IsAny<StreamContent>(), null), Times.Once);
        Assert.Equal(expectedUri, result);
    }

    [Fact]
    public async Task Upload_ShouldCallUploadMultipart_WhenStreamExceedsLimit()
    {
        // Arrange
        var largeStream = new MemoryStream(new byte[150 * 1024 * 1024]); // 150 MB, over the limit
        var archiveName = "test-archive.zip";
        var orgDatabaseId = "12345";
        var expectedMultipartResponse = "gei://archive/multipart";

        // Mock the ArchiveUploader to allow indirect testing of the protected UploadMultipart method
        var archiveUploaderMock = new Mock<ArchiveUploader>(_clientMock.Object, _logMock.Object) { CallBase = true };

        // Set up UploadMultipart to return the expected response when called
        archiveUploaderMock
            .Protected()
            .Setup<Task<string>>("UploadMultipart", largeStream, archiveName, ItExpr.IsAny<string>())
            .ReturnsAsync(expectedMultipartResponse);

        // Act
        var result = await archiveUploaderMock.Object.Upload(largeStream, archiveName, orgDatabaseId);

        // Assert
        archiveUploaderMock.Protected().Verify<Task<string>>(
            "UploadMultipart",
            Times.Once(),
            ItExpr.Is<Stream>(s => s == largeStream),
            ItExpr.Is<string>(name => name == archiveName),
            ItExpr.Is<string>(url => url.Contains(orgDatabaseId))
        );
        Assert.Equal(expectedMultipartResponse, result);
    }
}
