using System;
using System.IO;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using OctoshiftCLI.Extensions;
using OctoshiftCLI.Services;
using Polly;

[assembly: InternalsVisibleTo("OctoshiftCLI.Tests")]

namespace OctoshiftCLI.Commands.DownloadLogs;

public class DownloadLogsCommandHandler : ICommandHandler<DownloadLogsCommandArgs>
{
    private readonly OctoLogger _log;
    private readonly GithubApi _githubApi;
    private readonly HttpDownloadService _httpDownloadService;
    private readonly RetryPolicy _retryPolicy;

    internal Func<string, bool> FileExists = path => File.Exists(path);

    public DownloadLogsCommandHandler(
        OctoLogger log,
        GithubApi githubApi,
        HttpDownloadService httpDownloadService,
        RetryPolicy retryPolicy)
    {
        _log = log;
        _githubApi = githubApi;
        _httpDownloadService = httpDownloadService;
        _retryPolicy = retryPolicy;
    }

    public async Task Handle(DownloadLogsCommandArgs args)
    {
        if (args is null)
        {
            throw new ArgumentNullException(nameof(args));
        }

        if (args.MigrationId.HasValue())
        {
            if (args.GithubOrg.HasValue() || args.GithubRepo.HasValue())
            {
                _log.LogWarning("--github-org and --github-repo are ignored when --migration-id is specified.");
            }
        }
        else
        {
            if (!args.GithubOrg.HasValue() || !args.GithubRepo.HasValue())
            {
                throw new OctoshiftCliException("Either --migration-id (GraphQL migration ID) or both --github-org and --github-repo must be specified.");
            }
        }

        _log.LogWarning("Migration logs are only available for 24 hours after a migration finishes!");

        _log.LogInformation("Downloading migration logs...");

        // We check again below whether the file exists - but this is an extra check so we can fail early
        // where the user has defined a custom filename, rather than using the auto-generated one
        if (!args.Overwrite && FileExists(args.MigrationLogFile))
        {
            throw new OctoshiftCliException($"File {args.MigrationLogFile} already exists! Use --overwrite to overwrite this file.");
        }

        string logUrl;
        string migrationId;
        string repositoryName;

        if (args.MigrationId.HasValue())
        {
            // Use migration ID directly
            migrationId = args.MigrationId;
            var migrationResult = await _retryPolicy.RetryOnResult(
                async () => await _githubApi.GetMigration(migrationId),
                r => string.IsNullOrWhiteSpace(r.MigrationLogUrl),
                "Waiting for migration log to populate...");

            if (migrationResult.Outcome == OutcomeType.Failure)
            {
                throw new OctoshiftCliException($"Migration log for migration {migrationId} is currently unavailable. Migration logs are only available for 24 hours after a migration finishes. Please ensure the migration ID is correct and the migration has completed recently.");
            }

            var (_, RepositoryName, _, _, MigrationLogUrl) = migrationResult.Result;
            logUrl = MigrationLogUrl;
            repositoryName = RepositoryName;
        }
        else
        {
            // Use org/repo to find migration
            var result = await _retryPolicy.RetryOnResult(async () => await _githubApi.GetMigrationLogUrl(args.GithubOrg, args.GithubRepo), r => r?.MigrationLogUrl.IsNullOrWhiteSpace() ?? false,
                "Waiting for migration log to populate...");

            if (result.Outcome == OutcomeType.Successful && result.Result is null)
            {
                throw new OctoshiftCliException($"Migration for repository {args.GithubRepo} not found!");
            }

            if (result.Outcome == OutcomeType.Failure)
            {
                throw new OctoshiftCliException($"Migration log for repository {args.GithubRepo} unavailable!");
            }

            (logUrl, migrationId) = result.Result.Value;
            repositoryName = args.GithubRepo;
        }

        var defaultFileName = args.MigrationId.HasValue()
            ? $"migration-log-{repositoryName}-{migrationId}.log"
            : $"migration-log-{args.GithubOrg}-{repositoryName}-{migrationId}.log";
        args.MigrationLogFile ??= defaultFileName;

        if (FileExists(args.MigrationLogFile))
        {
            if (!args.Overwrite)
            {
                throw new OctoshiftCliException($"File {args.MigrationLogFile} already exists!  Use --overwrite to overwrite this file.");
            }

            _log.LogWarning($"Overwriting {args.MigrationLogFile} due to --overwrite option.");
        }

        _log.LogInformation($"Downloading log for repository {repositoryName} to {args.MigrationLogFile}...");
        await _httpDownloadService.DownloadToFile(logUrl, args.MigrationLogFile);

        _log.LogSuccess($"Downloaded {repositoryName} log to {args.MigrationLogFile}.");
    }
}
