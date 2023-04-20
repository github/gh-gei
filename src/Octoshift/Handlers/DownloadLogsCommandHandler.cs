using System;
using System.IO;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using OctoshiftCLI.Commands;
using OctoshiftCLI.Extensions;
using OctoshiftCLI.Services;
using Polly;

[assembly: InternalsVisibleTo("OctoshiftCLI.Tests")]

namespace OctoshiftCLI.Handlers;

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

        _log.Verbose = args.Verbose;
        _log.RegisterSecret(args.GithubPat);

        _log.LogWarning("Migration logs are only available for 24 hours after a migration finishes!");

        _log.LogInformation("Downloading migration logs...");
        _log.LogInformation($"GITHUB ORG: {args.GithubOrg}");
        _log.LogInformation($"GITHUB REPO: {args.GithubRepo}");

        if (args.GithubApiUrl.HasValue())
        {
            _log.LogInformation($"GITHUB API URL: {args.GithubApiUrl}");
        }

        if (args.GithubPat.HasValue())
        {
            _log.LogInformation($"GITHUB PAT: ***");
        }

        if (args.MigrationLogFile.HasValue())
        {
            _log.LogInformation($"MIGRATION LOG FILE: {args.MigrationLogFile}");
        }

        CheckIfOutputFileAlreadyExists(args.MigrationLogFile, args.Overwrite);

        var result = await _retryPolicy.RetryOnResult<(string MigrationLogUrl, string MigrationId)?>(async () => await _githubApi.GetMigrationLogUrl(args.GithubOrg, args.GithubRepo), result => string.IsNullOrEmpty(result.Value.MigrationLogUrl),
            "Waiting for migration log to populate...");

        if (result.Outcome == OutcomeType.Successful && result.Result is null)
        {
            throw new OctoshiftCliException($"Migration for repository {args.GithubRepo} not found!");
        }

        if (result.Outcome == OutcomeType.Failure)
        {
            throw new OctoshiftCliException($"Migration log for repository {args.GithubRepo} unavailable!");
        }

        var (logUrl, migrationId) = result.Result.Value;

        args.MigrationLogFile ??= $"migration-log-{args.GithubOrg}-{args.GithubRepo}-{migrationId}.log";

        // We already checked if the file exists above for the case where the user explicitly picked their own
        // filename. This handles the case where the filename has been set to the default based on the inputs
        // and migration ID.
        CheckIfOutputFileAlreadyExists(args.MigrationLogFile, args.Overwrite);

        _log.LogInformation($"Downloading log for repository {args.GithubRepo} to {args.MigrationLogFile}...");
        await _httpDownloadService.DownloadToFile(logUrl, args.MigrationLogFile);

        _log.LogSuccess($"Downloaded {args.GithubRepo} log to {args.MigrationLogFile}.");
    }

    private void CheckIfOutputFileAlreadyExists(string outputPath, bool shouldOverwrite)
    {
        if (FileExists(outputPath))
        {
            if (shouldOverwrite)
            {
                throw new OctoshiftCliException($"File {outputPath} already exists!  Use --overwrite to overwrite this file.");
            }
            else
            {
                _log.LogWarning($"Overwriting {outputPath} due to --overwrite option.");
            }
        }
    }
}
