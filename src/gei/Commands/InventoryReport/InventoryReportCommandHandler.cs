using System;
using System.Threading.Tasks;
using OctoshiftCLI.Commands;
using OctoshiftCLI.GithubEnterpriseImporter.Services;
using OctoshiftCLI.Services;

namespace OctoshiftCLI.GithubEnterpriseImporter.Commands.InventoryReport;

public class InventoryReportCommandHandler : ICommandHandler<InventoryReportCommandArgs>
{
    private readonly OctoLogger _log;
    private readonly FileSystemProvider _fileSystemProvider;
    private readonly GithubInspectorService _githubInspectorService;
    private readonly ReposCsvGeneratorService _reposCsvGenerator;

    public InventoryReportCommandHandler(
        OctoLogger log,
        FileSystemProvider fileSystemProvider,
        GithubInspectorService githubInspectorService,
        ReposCsvGeneratorService reposCsvGeneratorService)
    {
        _log = log;
        _fileSystemProvider = fileSystemProvider;
        _githubInspectorService = githubInspectorService;
        _reposCsvGenerator = reposCsvGeneratorService;
    }

    public async Task Handle(InventoryReportCommandArgs args)
    {
        if (args is null)
        {
            throw new ArgumentNullException(nameof(args));
        }

        _log.LogInformation("Creating inventory report...");

        _log.LogInformation("Finding Repos...");
        var repoCount = await _githubInspectorService.GetRepoCount(args.GithubOrg);
        _log.LogInformation($"Found {repoCount} Repos");

        _log.LogInformation("Generating repos.csv...");
        var reposCsvText = await _reposCsvGenerator.Generate(args.GhesApiUrl, args.GithubPat, args.GithubOrg, args.Minimal);
        await _fileSystemProvider.WriteAllTextAsync("repos.csv", reposCsvText);
        _log.LogSuccess("repos.csv generated");
    }
}
