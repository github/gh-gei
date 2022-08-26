using System;
using System.IO;
using System.Threading.Tasks;
using FluentAssertions;
using Moq;
using OctoshiftCLI.BbsToGithub.Services;
using Renci.SshNet;
using Xunit;

namespace OctoshiftCLI.Tests.bbs2gh.Services;

public sealed class BbsArchiveDownloaderTests : IDisposable
{
    private const int EXPORT_JOB_ID = 1;
    private const string BBS_HOME_DIRECTORY = "BBS_HOME";
    private const string TARGET_DIRECTORY = "TARGET";

    private readonly string _exportArchiveFilename = $"Bitbucket_export_{EXPORT_JOB_ID}.tar";
    private readonly Mock<OctoLogger> _mockOctoLogger = TestHelpers.CreateMock<OctoLogger>();
    private readonly Mock<FileSystemProvider> _mockFileSystemProvider = TestHelpers.CreateMock<FileSystemProvider>();
    private readonly Mock<ISftpClient> _mockSftpClient = new();
    private readonly BbsArchiveDownloader _bbsArchiveDownloader;

    public BbsArchiveDownloaderTests()
    {
        _bbsArchiveDownloader = new BbsArchiveDownloader(_mockOctoLogger.Object, _mockSftpClient.Object)
        {
            FileSystemProvider = _mockFileSystemProvider.Object,
            BbsSharedHomeDirectory = BBS_HOME_DIRECTORY
        };

        _mockSftpClient.Setup(m => m.Exists(It.IsAny<string>())).Returns(true);

        var mockAsyncResult = new Mock<IAsyncResult>();
        mockAsyncResult.Setup(m => m.IsCompleted).Returns(true);
        _mockSftpClient
            .Setup(m => m.BeginDownloadFile(It.IsAny<string>(), It.IsAny<Stream>(), It.IsAny<AsyncCallback>(), It.IsAny<object>(), It.IsAny<Action<ulong>>()))
            .Returns(mockAsyncResult.Object);
    }

    [Fact]
    public async Task Download_Calls_SftpClinet_DownloadFile_With_Correct_Params()
    {
        // Arrange
        var expectedSourceArchiveFullName = Path.Combine(BBS_HOME_DIRECTORY, "data/migration/export", _exportArchiveFilename);
        var expectedTargetArchiveFullName = Path.Combine(TARGET_DIRECTORY, _exportArchiveFilename);

        // Act
        await _bbsArchiveDownloader.Download(EXPORT_JOB_ID, TARGET_DIRECTORY);

        // Assert
        _mockSftpClient.Verify(m =>
            m.BeginDownloadFile(
                It.Is<string>(actual => actual == expectedSourceArchiveFullName),
                It.IsAny<Stream>(),
                null,
                null,
                It.IsAny<Action<ulong>>()));

        _mockFileSystemProvider.Verify(m => m.Open(It.Is<string>(actual => actual == expectedTargetArchiveFullName), FileMode.CreateNew));
    }

    [Fact]
    public async Task Downlaod_Throws_When_Target_Export_Archive_Already_Exists()
    {
        // Arrange
        _mockFileSystemProvider.Setup(m => m.FileExists(It.Is<string>(x => x.Contains(_exportArchiveFilename)))).Returns(true);

        // Act, Assert
        await _bbsArchiveDownloader.Invoking(x => x.Download(EXPORT_JOB_ID)).Should().ThrowExactlyAsync<OctoshiftCliException>();
    }

    [Fact]
    public async Task Download_Throws_When_Source_Export_Archive_Does_Not_Exist()
    {
        // Arrange
        _mockSftpClient.Setup(m => m.Exists(It.IsAny<string>())).Returns(false);

        // Act, Assert
        await _bbsArchiveDownloader.Invoking(x => x.Download(EXPORT_JOB_ID)).Should().ThrowExactlyAsync<OctoshiftCliException>();
    }

    [Fact]
    public async Task Download_Creates_Target_Directory()
    {
        // Arrange, Act
        await _bbsArchiveDownloader.Download(EXPORT_JOB_ID, TARGET_DIRECTORY);

        // Assert
        _mockFileSystemProvider.Verify(m => m.CreateDirectory(TARGET_DIRECTORY), Times.Once);
    }

    public void Dispose() => _bbsArchiveDownloader?.Dispose();
}
