using System;
using System.IO;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using OctoshiftCLI.Commands;
using OctoshiftCLI.Contracts;
using OctoshiftCLI.Extensions;
using Polly;

[assembly: InternalsVisibleTo("OctoshiftCLI.Tests")]

namespace OctoshiftCLI.Handlers;

public class DownloadLogsCommandHandler
{
    private readonly OctoLogger _log;
    private readonly ITargetGithubApiFactory _githubApiFactory;
    private readonly HttpDownloadService _httpDownloadService;
    private readonly RetryPolicy _retryPolicy;

    internal Func<string, bool> FileExists = path => File.Exists(path);

    public DownloadLogsCommandHandler(
        OctoLogger log,
        ITargetGithubApiFactory githubApiFactory,
        HttpDownloadService httpDownloadService,
        RetryPolicy retryPolicy)
    {
        _log = log;
        _githubApiFactory = githubApiFactory;
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

        _log.RegisterSecret(args.GithubPat);

        args.MigrationLogFile ??= $"migration-log-{args.GithubOrg}-{args.GithubRepo}.log";

        if (FileExists(args.MigrationLogFile))
        {
            if (!args.Overwrite)
            {
                throw new OctoshiftCliException($"File {args.MigrationLogFile} already exists!  Use --overwrite to overwrite this file.");
            }

            _log.LogWarning($"Overwriting {args.MigrationLogFile} due to --overwrite option.");
        }

        var githubApi = _githubApiFactory.Create(args.GithubApiUrl, args.GithubPat);

        var result = await _retryPolicy.RetryOnResult(async () => await githubApi.GetMigrationLogUrl(args.GithubOrg, args.GithubRepo), string.Empty,
            "Waiting for migration log to populate...");

        if (result.Outcome == OutcomeType.Successful && result.Result is null)
        {
            throw new OctoshiftCliException($"Migration for repository {args.GithubRepo} not found!");
        }

        if (result.Outcome == OutcomeType.Failure)
        {
            throw new OctoshiftCliException($"Migration log for repository {args.GithubRepo} unavailable!");
        }

        var logUrl = result.Result;

        _log.LogInformation($"Downloading log for repository {args.GithubRepo} to {args.MigrationLogFile}...");
        await _httpDownloadService.Download(logUrl, args.MigrationLogFile);

        _log.LogSuccess($"Downloaded {args.GithubRepo} log to {args.MigrationLogFile}.");
    }
}
