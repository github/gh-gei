using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using OctoshiftCLI.Commands;
using OctoshiftCLI.Services;

namespace OctoshiftCLI.GitlabToGithub.Commands.InventoryReport;

public class InventoryReportCommandHandler : ICommandHandler<InventoryReportCommandArgs>
{
    internal Func<string, string, Task> WriteToFile = async (path, contents) => await File.WriteAllTextAsync(path, contents);

    private readonly OctoLogger _log;
    private readonly GitlabApi _gitlabApi;
    private readonly GitlabInspectorService _bbsInspectorService;
    private readonly GroupsCsvGeneratorService _groupsCsvGenerator;
    private readonly ReposCsvGeneratorService _reposCsvGenerator;

    public InventoryReportCommandHandler(
        OctoLogger log,
        GitlabApi gitlabApi,
        GitlabInspectorService bbsInspectorService,
        GroupsCsvGeneratorService groupsCsvGeneratorService,
        ReposCsvGeneratorService reposCsvGeneratorService)
    {
        _log = log;
        _gitlabApi = gitlabApi;
        _bbsInspectorService = bbsInspectorService;
        _groupsCsvGenerator = groupsCsvGeneratorService;
        _reposCsvGenerator = reposCsvGeneratorService;
    }

    public async Task Handle(InventoryReportCommandArgs args)
    {
        if (args is null)
        {
            throw new ArgumentNullException(nameof(args));
        }

        _log.LogInformation("Creating inventory report...");

        var projectKeys = Array.Empty<string>();
        if (string.IsNullOrWhiteSpace(args.GitlabProject))
        {
            _log.LogInformation("Finding Projects...");
            var projects = await _gitlabApi.GetProjects();
            projectKeys = projects.Select(x => x.Key).ToArray();
            _log.LogInformation($"Found {projects.Count()} Projects");
        }

        _log.LogInformation("Finding Repos...");
        var repoCount = string.IsNullOrWhiteSpace(args.GitlabProject) ? await _bbsInspectorService.GetRepoCount(projectKeys) : await _bbsInspectorService.GetRepoCount(args.GitlabProject);
        _log.LogInformation($"Found {repoCount} Repos");

        _log.LogInformation("Generating data for projects.csv...");
        var groupsCsvText = await _groupsCsvGenerator.Generate(args.GitlabServerUrl, args.GitlabUsername, args.GitlabPassword, args.NoSslVerify, args.GitlabProject, args.Minimal);
        await WriteToFile("projects.csv", groupsCsvText);
        _log.LogSuccess("projects.csv generated");

        _log.LogInformation("Generating repos.csv...");
        var reposCsvText = await _reposCsvGenerator.Generate(args.GitlabServerUrl, args.GitlabUsername, args.GitlabPassword, args.NoSslVerify, args.GitlabProject, args.Minimal);
        await WriteToFile("repos.csv", reposCsvText);
        _log.LogSuccess("repos.csv generated");
    }
}
