using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using OctoshiftCLI.Commands;
using OctoshiftCLI.Services;

namespace OctoshiftCLI.BbsToGithub.Commands.InventoryReport;

public class InventoryReportCommandHandler : ICommandHandler<InventoryReportCommandArgs>
{
    internal Func<string, string, Task> WriteToFile = async (path, contents) => await File.WriteAllTextAsync(path, contents);

    private readonly OctoLogger _log;
    private readonly BbsApi _bbsApi;
    private readonly BbsInspectorService _bbsInspectorService;
    private readonly ProjectsCsvGeneratorService _projectsCsvGenerator;
    private readonly ReposCsvGeneratorService _reposCsvGenerator;

    public InventoryReportCommandHandler(
        OctoLogger log,
        BbsApi bbsApi,
        BbsInspectorService bbsInspectorService,
        ProjectsCsvGeneratorService projectsCsvGeneratorService,
        ReposCsvGeneratorService reposCsvGeneratorService)
    {
        _log = log;
        _bbsApi = bbsApi;
        _bbsInspectorService = bbsInspectorService;
        _projectsCsvGenerator = projectsCsvGeneratorService;
        _reposCsvGenerator = reposCsvGeneratorService;
    }

    public async Task Handle(InventoryReportCommandArgs args)
    {
        if (args is null)
        {
            throw new ArgumentNullException(nameof(args));
        }

        _log.LogInformation("Creating inventory report...");

        _bbsInspectorService.ProjectFilter = args.BbsProject;

        _log.LogInformation("Finding Projects...");
        var projects = await _bbsApi.GetProjects();
        _log.LogInformation($"Found {projects.Count()} Projects");

        _log.LogInformation("Finding Repos...");
        var repoCount = await _bbsInspectorService.GetRepoCount();
        _log.LogInformation($"Found {repoCount} Repos");

        _log.LogInformation("Generating projects.csv...");
        var projectsCsvText = await _projectsCsvGenerator.Generate(args.BbsServerUrl, args.BbsProject, args.Minimal);
        await WriteToFile("projects.csv", projectsCsvText);
        _log.LogSuccess("projects.csv generated");

        _log.LogInformation("Generating repos.csv...");
        var reposCsvText = await _reposCsvGenerator.Generate(args.BbsServerUrl, args.BbsProject, args.Minimal);
        await WriteToFile("repos.csv", reposCsvText);
        _log.LogSuccess("repos.csv generated");
    }
}
