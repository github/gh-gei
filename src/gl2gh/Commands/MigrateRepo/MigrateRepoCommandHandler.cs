using System;
using System.Threading.Tasks;
using OctoshiftCLI.Commands;
using OctoshiftCLI.Extensions;
using OctoshiftCLI.Services;

namespace OctoshiftCLI.GitlabToGithub.Commands.MigrateRepo;

public class MigrateRepoCommandHandler : ICommandHandler<MigrateRepoCommandArgs>
{
    private readonly OctoLogger _log;
    private readonly GithubApi _githubApi;
    private readonly GitlabApi _gitlabApi;
    private readonly AzureApi _azureApi;
    private readonly AwsApi _awsApi;
    private readonly EnvironmentVariableProvider _environmentVariableProvider;
    private readonly FileSystemProvider _fileSystemProvider;
    private readonly WarningsCountLogger _warningsCountLogger;
    private const int CHECK_EXPORT_STATUS_DELAY_IN_MILLISECONDS = 10000;
    private const int CHECK_MIGRATION_STATUS_DELAY_IN_MILLISECONDS = 60000;

    public MigrateRepoCommandHandler(
        OctoLogger log,
        GithubApi githubApi,
        GitlabApi gitlabApi,
        EnvironmentVariableProvider environmentVariableProvider,
        AzureApi azureApi,
        AwsApi awsApi,
        FileSystemProvider fileSystemProvider,
        WarningsCountLogger warningsCountLogger)
    {
        _log = log;
        _githubApi = githubApi;
        _gitlabApi = gitlabApi;
        _azureApi = azureApi;
        _awsApi = awsApi;
        _environmentVariableProvider = environmentVariableProvider;
        _fileSystemProvider = fileSystemProvider;
        _warningsCountLogger = warningsCountLogger;
    }

    public async Task Handle(MigrateRepoCommandArgs args)
    {
        if (args is null)
        {
            throw new ArgumentNullException(nameof(args));
        }

        ValidateOptions(args);

        if (_gitlabApi is not null)
        {
            await _gitlabApi.LogServerVersion();
        }

        var migrationSourceId = "";

        if (args.ShouldImportArchive())
        {
            var targetRepoExists = await _githubApi.DoesRepoExist(args.GithubOrg, args.GithubRepo);

            if (targetRepoExists)
            {
                throw new OctoshiftCliException($"A repository called {args.GithubOrg}/{args.GithubRepo} already exists");
            }

            migrationSourceId = await CreateMigrationSource(args);
        }

        if (args.ShouldGenerateArchive())
        {
            await GenerateArchive(args);

            _log.LogInformation($"Downloading GitLab archive...");

            args.ArchivePath ??= _fileSystemProvider.GetTempFileName();
            await _gitlabApi.DownloadExportArchive(args.GitlabGroup, args.GitlabProject, args.ArchivePath);

            _log.LogInformation(args.KeepArchive ? $"Archive downloaded to \"{args.ArchivePath}\"" : "Archive download complete");
        }

        if (args.ShouldUploadArchive())
        {
            _log.LogInformation($"Archive path: {args.ArchivePath}");

            try
            {
                if (args.UseGithubStorage)
                {
                    args.ArchiveUrl = await UploadArchiveToGithub(args.GithubOrg, args.ArchivePath);
                }
#pragma warning disable IDE0045
                else if (args.AwsBucketName.HasValue())
#pragma warning restore IDE0045
                {
                    args.ArchiveUrl = await UploadArchiveToAws(args.AwsBucketName, args.ArchivePath);
                }
                else
                {
                    args.ArchiveUrl = await UploadArchiveToAzure(args.ArchivePath);
                }

            }
            finally
            {
                if (!args.KeepArchive)
                {
                    DeleteArchive(args.ArchivePath);
                }
            }
        }

        if (args.ShouldImportArchive())
        {
            await ImportArchive(args, migrationSourceId, args.ArchiveUrl);
        }
    }

    private void DeleteArchive(string path)
    {
        try
        {
            _fileSystemProvider.DeleteIfExists(path);
        }
#pragma warning disable CA1031
        catch (Exception ex)
#pragma warning restore CA1031
        {
            _log.LogWarning($"Couldn't delete the downloaded archive. Error message: \"{ex.Message}\"");
            _log.LogVerbose(ex.ToString());
        }
    }

    private async Task<string> GenerateArchive(MigrateRepoCommandArgs args)
    {
        await _gitlabApi.StartExport(args.GitlabGroup, args.GitlabProject);

        _log.LogInformation($"Export started.");

        var (exportState, archiveUrl) = await _gitlabApi.GetExport(args.GitlabGroup, args.GitlabProject);

        while (ExportState.IsInProgress(exportState))
        {
            _log.LogInformation($"Export status: {exportState}.");
            await Task.Delay(CHECK_EXPORT_STATUS_DELAY_IN_MILLISECONDS);
            (exportState, archiveUrl) = await _gitlabApi.GetExport(args.GitlabGroup, args.GitlabProject);
        }

        if (ExportState.IsError(exportState))
        {
            throw new OctoshiftCliException($"GitLab archive export failed!");
        }

        _log.LogInformation($"Archive export completed.");

        return archiveUrl;
    }

    private async Task<string> UploadArchiveToAzure(string archivePath)
    {
        _log.LogInformation("Uploading Archive to Azure...");

#pragma warning disable IDE0063
        await using (var archiveData = _fileSystemProvider.OpenRead(archivePath))
#pragma warning restore IDE0063
        {
            var archiveName = GenerateArchiveName();
            var archiveBlobUrl = await _azureApi.UploadToBlob(archiveName, archiveData);
            return archiveBlobUrl.ToString();
        }
    }

    private string GenerateArchiveName() => $"{Guid.NewGuid()}.tar";

    private async Task<string> UploadArchiveToAws(string bucketName, string archivePath)
    {
        _log.LogInformation("Uploading Archive to AWS...");

        var keyName = GenerateArchiveName();
        var archiveBlobUrl = await _awsApi.UploadToBucket(bucketName, archivePath, keyName);

        return archiveBlobUrl;
    }

    private async Task<string> UploadArchiveToGithub(string org, string archivePath)
    {
        await using var archiveData = _fileSystemProvider.OpenRead(archivePath);
        var githubOrgDatabaseId = await _githubApi.GetOrganizationDatabaseId(org);

        _log.LogInformation("Uploading archive to GitHub Storage");
        var keyName = GenerateArchiveName();
        var authenticatedGitArchiveUri = await _githubApi.UploadArchiveToGithubStorage(githubOrgDatabaseId, keyName, archiveData);

        return authenticatedGitArchiveUri;
    }

    private async Task<string> CreateMigrationSource(MigrateRepoCommandArgs args)
    {
        _log.LogInformation("Creating Migration Source...");

        args.GithubPat ??= _environmentVariableProvider.TargetGithubPersonalAccessToken();
        var githubOrgId = await _githubApi.GetOrganizationId(args.GithubOrg);

        try
        {
            return await _githubApi.CreateGitlabMigrationSource(githubOrgId);
        }
        catch (OctoshiftCliException ex) when (ex.Message.Contains("not have the correct permissions to execute"))
        {
            var insufficientPermissionsMessage = InsufficientPermissionsMessageGenerator.Generate(args.GithubOrg);
            var message = $"{ex.Message}{insufficientPermissionsMessage}";
            throw new OctoshiftCliException(message, ex);
        }
    }

    private async Task ImportArchive(MigrateRepoCommandArgs args, string migrationSourceId, string archiveUrl = null)
    {
        _log.LogInformation("Importing Archive...");

        archiveUrl ??= args.ArchiveUrl;

        var gitlabRepoUrl = GetGitlabProjectUrl(args);

        args.GithubPat ??= _environmentVariableProvider.TargetGithubPersonalAccessToken();
        var githubOrgId = await _githubApi.GetOrganizationId(args.GithubOrg);

        string migrationId;

        try
        {
            migrationId = await _githubApi.StartGitlabMigration(migrationSourceId, gitlabRepoUrl, githubOrgId, args.GithubRepo, args.GithubPat, archiveUrl, args.TargetRepoVisibility);
        }
        catch (OctoshiftCliException ex) when (ex.Message == $"A repository called {args.GithubOrg}/{args.GithubRepo} already exists")
        {
            _log.LogWarning($"The Org '{args.GithubOrg}' already contains a repository with the name '{args.GithubRepo}'. No operation will be performed");
            return;
        }

        if (args.QueueOnly)
        {
            _log.LogInformation($"A repository migration (ID: {migrationId}) was successfully queued.");
            return;
        }

        var (migrationState, _, warningsCount, failureReason, migrationLogUrl) = await _githubApi.GetMigration(migrationId);

        while (RepositoryMigrationStatus.IsPending(migrationState))
        {
            _log.LogInformation($"Migration in progress (ID: {migrationId}). State: {migrationState}. Waiting 60 seconds...");
            await Task.Delay(CHECK_MIGRATION_STATUS_DELAY_IN_MILLISECONDS);
            (migrationState, _, warningsCount, failureReason, migrationLogUrl) = await _githubApi.GetMigration(migrationId);
        }

        var migrationLogAvailableMessage = $"Migration log available at {migrationLogUrl} or by running `gh {CliContext.RootCommand} download-logs --github-org {args.GithubOrg} --github-repo {args.GithubRepo}`";

        if (RepositoryMigrationStatus.IsFailed(migrationState))
        {
            _log.LogError($"Migration Failed. Migration ID: {migrationId}");
            _warningsCountLogger.LogWarningsCount(warningsCount);
            _log.LogInformation(migrationLogAvailableMessage);
            throw new OctoshiftCliException(failureReason);
        }

        _log.LogSuccess($"Migration completed (ID: {migrationId})! State: {migrationState}");
        _warningsCountLogger.LogWarningsCount(warningsCount);
        _log.LogInformation(migrationLogAvailableMessage);
    }

    private string GetAwsAccessKey(MigrateRepoCommandArgs args) => args.AwsAccessKey.HasValue() ? args.AwsAccessKey : _environmentVariableProvider.AwsAccessKeyId(false);

    private string GetAwsSecretKey(MigrateRepoCommandArgs args) => args.AwsSecretKey.HasValue() ? args.AwsSecretKey : _environmentVariableProvider.AwsSecretAccessKey(false);

    private string GetAwsRegion(MigrateRepoCommandArgs args) => args.AwsRegion.HasValue() ? args.AwsRegion : _environmentVariableProvider.AwsRegion(false);

    private string GetAzureStorageConnectionString(MigrateRepoCommandArgs args) => args.AzureStorageConnectionString.HasValue()
        ? args.AzureStorageConnectionString
        : _environmentVariableProvider.AzureStorageConnectionString(false);

    private string GetGitlabPat(MigrateRepoCommandArgs args) => args.GitlabPat.HasValue() ? args.GitlabPat : _environmentVariableProvider.GitlabPat(false);

    private string GetGitlabProjectUrl(MigrateRepoCommandArgs args)
    {
        return args.GitlabServerUrl.HasValue() && args.GitlabGroup.HasValue() && args.GitlabProject.HasValue()
            ? $"{args.GitlabServerUrl.TrimEnd('/')}/{args.GitlabGroup}/{args.GitlabProject}"
            : "https://not-used";
    }

    private void ValidateOptions(MigrateRepoCommandArgs args)
    {
        if (args.ShouldGenerateArchive() && GetGitlabPat(args).IsNullOrWhiteSpace())
        {
            throw new OctoshiftCliException("GitLab PAT must be either set as GITLAB_PAT environment variable or passed as --gitlab-pat.");
        }

        // Validate --archive-path if provided as an input (i.e. not generating a new archive)
        if (!args.ShouldGenerateArchive() && args.ArchivePath.HasValue() && !_fileSystemProvider.FileExists(args.ArchivePath))
        {
            throw new OctoshiftCliException($"The archive file provided with --archive-path does not exist or is not accessible: {args.ArchivePath}");
        }

        if (args.ShouldUploadArchive())
        {
            ValidateUploadOptions(args);
        }
    }

    private void ValidateUploadOptions(MigrateRepoCommandArgs args)
    {
        var shouldUseAzureStorage = GetAzureStorageConnectionString(args).HasValue();
        var shouldUseAwsS3 = args.AwsBucketName.HasValue();
        if (!shouldUseAzureStorage && !shouldUseAwsS3 && !args.UseGithubStorage)
        {
            throw new OctoshiftCliException(
                "Either Azure storage connection (--azure-storage-connection-string or AZURE_STORAGE_CONNECTION_STRING env. variable) or " +
                "AWS S3 connection (--aws-bucket-name, --aws-access-key (or AWS_ACCESS_KEY_ID env. variable), --aws-secret-key (or AWS_SECRET_ACCESS_KEY env.variable)) or " +
                "GitHub Storage Option (--use-github-storage) " +
                "must be provided.");
        }

        if (shouldUseAzureStorage && shouldUseAwsS3)
        {
            throw new OctoshiftCliException(
                "Azure storage connection (--azure-storage-connection-string or AZURE_STORAGE_CONNECTION_STRING env. variable) and " +
                "AWS S3 connection (--aws-bucket-name, --aws-access-key (or AWS_ACCESS_KEY_ID env. variable), --aws-secret-key (or AWS_SECRET_ACCESS_KEY env.variable)) cannot be " +
                "specified together.");
        }

        if (shouldUseAwsS3)
        {
            if (!GetAwsAccessKey(args).HasValue())
            {
                throw new OctoshiftCliException("Either --aws-access-key or AWS_ACCESS_KEY_ID environment variable must be set.");
            }

            if (!GetAwsSecretKey(args).HasValue())
            {
                throw new OctoshiftCliException("Either --aws-secret-key or AWS_SECRET_ACCESS_KEY environment variable must be set.");
            }

            if (GetAwsRegion(args).IsNullOrWhiteSpace())
            {
                throw new OctoshiftCliException("Either --aws-region or AWS_REGION environment variable must be set.");
            }
        }
    }
}
