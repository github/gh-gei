using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using OctoshiftCLI.Commands;
using OctoshiftCLI.Extensions;
using OctoshiftCLI.GithubEnterpriseImporter.Services;
using OctoshiftCLI.Services;

namespace OctoshiftCLI.GithubEnterpriseImporter.Commands.MigrateRepo;

public class MigrateRepoCommandHandler : ICommandHandler<MigrateRepoCommandArgs>
{
    private readonly OctoLogger _log;
    private readonly GithubApi _sourceGithubApi;
    private readonly GithubApi _targetGithubApi;
    private readonly AzureApi _azureApi;
    private readonly AwsApi _awsApi;
    private readonly EnvironmentVariableProvider _environmentVariableProvider;
    private readonly HttpDownloadService _httpDownloadService;
    private readonly FileSystemProvider _fileSystemProvider;
    private readonly GhesVersionChecker _ghesVersionChecker;
    private readonly RetryPolicy _retryPolicy;
    private readonly WarningsCountLogger _warningsCountLogger;
    private const int ARCHIVE_GENERATION_TIMEOUT_IN_HOURS = 20;
    private const int CHECK_STATUS_DELAY_IN_MILLISECONDS = 10000; // 10 seconds
    private const string GIT_ARCHIVE_FILE_NAME = "git_archive.tar.gz";
    private const string METADATA_ARCHIVE_FILE_NAME = "metadata_archive.tar.gz";
    private const string DUPLICATE_ARCHIVE_FILE_NAME = "archive.tar.gz";
    private const string DEFAULT_GITHUB_BASE_URL = "https://github.com";

    public MigrateRepoCommandHandler(
        OctoLogger log,
        GithubApi sourceGithubApi,
        GithubApi targetGithubApi,
        EnvironmentVariableProvider environmentVariableProvider,
        AzureApi azureApi,
        AwsApi awsApi,
        HttpDownloadService httpDownloadService,
        FileSystemProvider fileSystemProvider,
        GhesVersionChecker ghesVersionChecker,
        RetryPolicy retryPolicy,
        WarningsCountLogger warningsCountLogger)
    {
        _log = log;
        _sourceGithubApi = sourceGithubApi;
        _targetGithubApi = targetGithubApi;
        _environmentVariableProvider = environmentVariableProvider;
        _azureApi = azureApi;
        _awsApi = awsApi;
        _httpDownloadService = httpDownloadService;
        _fileSystemProvider = fileSystemProvider;
        _ghesVersionChecker = ghesVersionChecker;
        _retryPolicy = retryPolicy;
        _warningsCountLogger = warningsCountLogger;
    }

    public async Task Handle(MigrateRepoCommandArgs args)
    {
        if (args is null)
        {
            throw new ArgumentNullException(nameof(args));
        }

        _log.LogInformation("Migrating Repo...");

        // Process skip options
        if (args.SkipTags)
        {
            _log.LogInformation("Skipping tags as per configuration.");
        }

        if (args.SkipBranches)
        {
            _log.LogInformation("Skipping branches as per configuration.");
        }

        if (args.SkipPullRequests)
        {
            _log.LogInformation("Skipping pull requests as per configuration.");
        }

        var blobCredentialsRequired = args.GitArchivePath.HasValue() || await _ghesVersionChecker.AreBlobCredentialsRequired(args.GhesApiUrl);

        if (args.GhesApiUrl.HasValue() || args.GitArchivePath.HasValue())
        {
            ValidateUploadOptions(args, blobCredentialsRequired);
        }

        if (args.GhesApiUrl.HasValue())
        {
            var targetRepoExists = await _targetGithubApi.DoesRepoExist(args.GithubTargetOrg, args.TargetRepo);
            var targetOrgExists = await _targetGithubApi.DoesOrgExist(args.GithubTargetOrg);

            if (targetRepoExists)
            {
                throw new OctoshiftCliException($"A repository called {args.GithubTargetOrg}/{args.TargetRepo} already exists");
            }

            if (!targetOrgExists)
            {
                throw new OctoshiftCliException($"The target org \"{args.GithubTargetOrg}\" does not exist.");
            }
        }

        string migrationSourceId;

        var githubOrgId = await _targetGithubApi.GetOrganizationId(args.GithubTargetOrg);

        try
        {
            migrationSourceId = await _targetGithubApi.CreateGhecMigrationSource(githubOrgId);
        }
        catch (OctoshiftCliException ex) when (ex.Message.Contains("not have the correct permissions to execute"))
        {
            var insufficientPermissionsMessage = InsufficientPermissionsMessageGenerator.Generate(args.GithubTargetOrg);
            var message = $"{ex.Message}{insufficientPermissionsMessage}";
            throw new OctoshiftCliException(message, ex);
        }

        if (args.GhesApiUrl.HasValue())
        {
            (args.GitArchiveUrl, args.MetadataArchiveUrl) = await GenerateAndUploadArchive(
                args.GithubSourceOrg,
                args.GithubTargetOrg,
                args.SourceRepo,
                args.AwsBucketName,
                args.SkipReleases,
                args.LockSourceRepo,
                blobCredentialsRequired,
                args.KeepArchive,
                args.UseGithubStorage,
                args.SkipTags,
                args.SkipBranches,
                args.SkipPullRequests
            );

            if (args.UseGithubStorage || blobCredentialsRequired)
            {
                _log.LogInformation("Archives uploaded to blob storage, now starting migration...");
            }
        }

        // Proceed with existing migration logic...
    }

    private async Task<(string GitArchiveUrl, string MetadataArchiveUrl, int GitArchiveId, int MetadataArchiveId)> GenerateAndUploadArchive(
        string githubSourceOrg,
        string githubTargetOrg,
        string sourceRepo,
        string awsBucketName,
        bool skipReleases,
        bool lockSourceRepo,
        bool blobCredentialsRequired,
        bool keepArchive,
        bool useGithubStorage,
        bool skipTags,
        bool skipBranches,
        bool skipPullRequests)
    {
        var (gitArchiveUrl, metadataArchiveUrl, gitArchiveId, metadataArchiveId) = await GenerateArchives(
            githubSourceOrg,
            sourceRepo,
            skipReleases,
            lockSourceRepo,
            skipTags,
            skipBranches,
            skipPullRequests
        );

        if (!useGithubStorage && !blobCredentialsRequired)
        {
            return (gitArchiveUrl, metadataArchiveUrl);
        }

        // Additional archive upload logic...
        return (gitArchiveUrl, metadataArchiveUrl);
    }

    private async Task<(string GitArchiveUrl, string MetadataArchiveUrl, int GitArchiveId, int MetadataArchiveId)> GenerateArchives(
        string githubSourceOrg,
        string sourceRepo,
        bool skipReleases,
        bool lockSourceRepo,
        bool skipTags,
        bool skipBranches,
        bool skipPullRequests)
    {
        var gitArchiveId = await _sourceGithubApi.StartGitArchiveGeneration(
            githubSourceOrg,
            sourceRepo,
            skipTags: skipTags,
            skipBranches: skipBranches,
            skipPullRequests: skipPullRequests
        );

        _log.LogInformation($"Archive generation of git data started with id: {gitArchiveId}");

        var metadataArchiveId = await _sourceGithubApi.StartMetadataArchiveGeneration(
            githubSourceOrg,
            sourceRepo,
            skipReleases,
            lockSourceRepo,
            skipTags: skipTags,
            skipBranches: skipBranches,
            skipPullRequests: skipPullRequests
        );

        _log.LogInformation($"Archive generation of metadata started with id: {metadataArchiveId}");

        // Waiting for archive generation to complete
        var gitArchiveUrl = await WaitForArchiveGeneration(_sourceGithubApi, githubSourceOrg, gitArchiveId);
        var metadataArchiveUrl = await WaitForArchiveGeneration(_sourceGithubApi, githubSourceOrg, metadataArchiveId);

        return (gitArchiveUrl, metadataArchiveUrl, gitArchiveId, metadataArchiveId);
    }
}
