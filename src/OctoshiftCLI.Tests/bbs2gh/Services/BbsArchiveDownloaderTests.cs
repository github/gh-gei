using System;
using System.IO;
using FluentAssertions;
using Moq;
using OctoshiftCLI.BbsToGithub.Services;
using OctoshiftCLI.Contracts;
using Renci.SshNet;
using Xunit;

namespace OctoshiftCLI.Tests.bbs2gh.Services;

public sealed class BbsArchiveDownloaderTests : IDisposable
{
    private const int EXPORT_JOB_ID = 1;
    private const string BBS_HOME_DIRECTORY = "BBS_HOME";

    private readonly string _exportArchiveFilename = $"Bitbucket_export_{EXPORT_JOB_ID}.tar";
    private readonly Mock<OctoLogger> _mockOctoLogger = TestHelpers.CreateMock<OctoLogger>();
    private readonly Mock<IFileSystemProvider> _mockFileSystemProvider = new();
    private readonly BbsArchiveDownloader _bbsArchiveDownloader;
    private readonly ScpClient _scpClient;

    public BbsArchiveDownloaderTests()
    {
        _scpClient = new ScpClient("host", "username", "password");
        _bbsArchiveDownloader = new BbsArchiveDownloader(_mockOctoLogger.Object, _scpClient)
        {
            FileSystemProvider = _mockFileSystemProvider.Object,
            BbsSharedHomeDirectory = BBS_HOME_DIRECTORY
        };
    }

    [Fact]
    public void Download_Calls_ScpClient_Download_With_Correct_Params()
    {
        // Arrange
        var expectedSourceArchiveFullName = Path.Combine(BBS_HOME_DIRECTORY, "data/migration/export", _exportArchiveFilename);

        string actualSource = null;
        string actualTarget = null;
        _bbsArchiveDownloader.ScpDownload = (source, target) =>
        {
            actualSource = source;
            actualTarget = target.Name;
        };

        // Act
        _bbsArchiveDownloader.Download(EXPORT_JOB_ID);

        // Assert
        actualSource.Should().Be(expectedSourceArchiveFullName);
        actualTarget.Should().Be(_exportArchiveFilename);
    }

    [Fact]
    public void Downlaod_Throws_When_Target_Export_Archive_Already_Exists()
    {
        // Arrange
        _mockFileSystemProvider.Setup(m => m.FileExists(It.Is<string>(x => x.Contains(_exportArchiveFilename)))).Returns(true);

        // Act, Assert
        _bbsArchiveDownloader.Invoking(x => x.Download(EXPORT_JOB_ID)).Should().Throw<OctoshiftCliException>();
    }

    [Fact]
    public void Download_Creates_Target_Directory()
    {
        // Arrange
        const string targetDirectory = "TARGET";
        _bbsArchiveDownloader.ScpDownload = (_, _) => { };

        // Act
        _bbsArchiveDownloader.Download(EXPORT_JOB_ID, targetDirectory);

        // Assert
        _mockFileSystemProvider.Verify(m => m.CreateDirectory(targetDirectory), Times.Once);
    }

    public void Dispose()
    {
        _scpClient?.Dispose();
        _bbsArchiveDownloader?.Dispose();
    }
}
