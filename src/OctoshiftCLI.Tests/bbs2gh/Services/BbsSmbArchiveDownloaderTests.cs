using System.IO;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Moq;
using OctoshiftCLI.BbsToGithub.Services;
using OctoshiftCLI.Extensions;
using SMBLibrary;
using SMBLibrary.Client;
using Xunit;
using FileAttributes = SMBLibrary.FileAttributes;

namespace OctoshiftCLI.Tests.bbs2gh.Services;

public class BbsSmbArchiveDownloaderTests
{
    private const int EXPORT_JOB_ID = 1;
    private const string SHARE_ROOT = "SHARE_ROOT";
    private const string BBS_HOME_DIRECTORY_FROM_SHARE = "PATH\\TO\\BBS\\HOME\\DIRECTORY";
    private const string BBS_HOME_DIRECTORY = $"{SHARE_ROOT}\\{BBS_HOME_DIRECTORY_FROM_SHARE}";
    private const string TARGET_DIRECTORY = "TARGET";
    private const string HOST = "HOST";
    private const string SMB_USER = "SMB_USER";
    private const string SMB_PASSWORD = "SMB_PASSWORD";
    private const string DOMAIN = "DOMAIN";

    private readonly string _exportArchiveFilename = $"Bitbucket_export_{EXPORT_JOB_ID}.tar";
    private readonly Mock<OctoLogger> _mockOctoLogger = TestHelpers.CreateMock<OctoLogger>();
    private readonly Mock<FileSystemProvider> _mockFileSystemProvider = TestHelpers.CreateMock<FileSystemProvider>();
    private readonly Mock<ISMBClient> _mockSmbClient = new();
    private readonly Mock<ISMBFileStore> _mockSmbFileStore = new();
    private readonly BbsSmbArchiveDownloader _bbsArchiveDownloader;

    public BbsSmbArchiveDownloaderTests()
    {
        _bbsArchiveDownloader = new BbsSmbArchiveDownloader(
            _mockOctoLogger.Object,
            _mockFileSystemProvider.Object,
            _mockSmbClient.Object,
            HOST,
            SMB_USER,
            SMB_PASSWORD,
            DOMAIN)
        { BbsSharedHomeDirectory = BBS_HOME_DIRECTORY };

        _mockSmbClient.Setup(m => m.MaxReadSize).Returns(1048576U);
        _mockSmbClient.Setup(m => m.MaxWriteSize).Returns(1048576U);
    }

    [Fact]
    public async Task Download_Returns_Downloaded_Archive_Full_Name()
    {
        // Arrange
        var expectedSourceArchiveFullName = Path.Join(BBS_HOME_DIRECTORY_FROM_SHARE, "data/migration/export", _exportArchiveFilename).ToWindowsPath();
        var expectedTargetArchiveFullName = Path.Join(TARGET_DIRECTORY, _exportArchiveFilename).ToUnixPath();

        _mockSmbClient.Setup(m => m.Connect(HOST, SMBTransportType.DirectTCPTransport)).Returns(true);
        _mockSmbClient.Setup(m => m.Login(DOMAIN, SMB_USER, SMB_PASSWORD)).Returns(NTStatus.STATUS_SUCCESS);
        var createSmbFileStoreStatus = NTStatus.STATUS_SUCCESS;
        _mockSmbClient.Setup(m => m.TreeConnect(SHARE_ROOT, out createSmbFileStoreStatus)).Returns(_mockSmbFileStore.Object);

        var sharedFileHandle = new object();
        var fileStatus = FileStatus.FILE_OPENED;
        _mockSmbFileStore.Setup(m => m.CreateFile(
                out sharedFileHandle,
                out fileStatus,
                expectedSourceArchiveFullName,
                AccessMask.GENERIC_READ | AccessMask.SYNCHRONIZE,
                FileAttributes.Normal,
                ShareAccess.Read,
                CreateDisposition.FILE_OPEN,
                CreateOptions.FILE_NON_DIRECTORY_FILE | CreateOptions.FILE_SYNCHRONOUS_IO_ALERT,
                null))
            .Returns(NTStatus.STATUS_SUCCESS);

        FileInformation fileStandardInformation = new FileStandardInformation
        {
            AllocationSize = 10 * 1024 * 1024 // 10 MB
        };
        _mockSmbFileStore
            .Setup(m => m.GetFileInformation(out fileStandardInformation, sharedFileHandle, FileInformationClass.FileStandardInformation))
            .Returns(NTStatus.STATUS_SUCCESS);

        var data = new byte[1024];
        _mockSmbFileStore
            .SetupSequence(m => m.ReadFile(out data, sharedFileHandle, It.IsAny<long>(), It.IsAny<int>()))
            .Returns(NTStatus.STATUS_SUCCESS)
            .Returns(NTStatus.STATUS_SUCCESS)
            .Returns(NTStatus.STATUS_END_OF_FILE);

        // Act
        var actualTargetArchiveFullName = await _bbsArchiveDownloader.Download(EXPORT_JOB_ID, TARGET_DIRECTORY);

        // Assert
        _mockSmbClient.Verify(m => m.Connect(HOST, SMBTransportType.DirectTCPTransport), Times.Once);
        _mockSmbClient.Verify(m => m.Login(DOMAIN, SMB_USER, SMB_PASSWORD), Times.Once);
        _mockSmbClient.Verify(m => m.TreeConnect(SHARE_ROOT, out createSmbFileStoreStatus), Times.Once);
        _mockSmbFileStore.Verify(m => m.CreateFile(
                out sharedFileHandle,
                out fileStatus,
                expectedSourceArchiveFullName,
                AccessMask.GENERIC_READ | AccessMask.SYNCHRONIZE,
                FileAttributes.Normal,
                ShareAccess.Read,
                CreateDisposition.FILE_OPEN,
                CreateOptions.FILE_NON_DIRECTORY_FILE | CreateOptions.FILE_SYNCHRONOUS_IO_ALERT,
                null),
            Times.Once);
        _mockSmbFileStore.Verify(m => m.GetFileInformation(out fileStandardInformation, sharedFileHandle, FileInformationClass.FileStandardInformation), Times.Once);
        _mockSmbFileStore.Verify(m => m.ReadFile(out data, sharedFileHandle, It.IsAny<long>(), It.IsAny<int>()), Times.Exactly(3));
        _mockFileSystemProvider.Verify(m => m.Open(expectedTargetArchiveFullName, FileMode.CreateNew), Times.Once);
        _mockFileSystemProvider.Verify(m => m.WriteAsync(It.IsAny<FileStream>(), data, It.IsAny<CancellationToken>()), Times.Exactly(2));

        actualTargetArchiveFullName.Should().Be(expectedTargetArchiveFullName);
    }
}
