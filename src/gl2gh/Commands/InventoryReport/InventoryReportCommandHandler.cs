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
    private readonly ReposCsvGeneratorService _projectsCsvGenerator;

    public InventoryReportCommandHandler(
        OctoLogger log,
        GitlabApi gitlabApi,
        GitlabInspectorService bbsInspectorService,
        GroupsCsvGeneratorService groupsCsvGeneratorService,
        ReposCsvGeneratorService projectsCsvGeneratorService)
    {
        _log = log;
        _gitlabApi = gitlabApi;
        _bbsInspectorService = bbsInspectorService;
        _groupsCsvGenerator = groupsCsvGeneratorService;
        _projectsCsvGenerator = projectsCsvGeneratorService;
    }

    public async Task Handle(InventoryReportCommandArgs args)
    {
        if (args is null)
        {
            throw new ArgumentNullException(nameof(args));
        }

        _log.LogInformation("Creating inventory report...");

        var groupKeys = Array.Empty<string>();
        if (string.IsNullOrWhiteSpace(args.GitlabGroup))
        {
            _log.LogInformation("Finding Groups...");
            var groups = await _gitlabApi.GetGroups();
            groupKeys = groups.Select(x => x.Key).ToArray();
            _log.LogInformation($"Found {groups.Count()} Groups");
        }

        _log.LogInformation("Finding Projects...");
        var projectCount = string.IsNullOrWhiteSpace(args.GitlabGroup) ? await _bbsInspectorService.GetProjectCount(groupKeys) : await _bbsInspectorService.GetProjectCount(args.GitlabGroup);
        _log.LogInformation($"Found {projectCount} Projects");

        _log.LogInformation("Generating data for groups.csv...");
        var groupsCsvText = await _groupsCsvGenerator.Generate(args.GitlabServerUrl, args.GitlabUsername, args.GitlabPassword, args.NoSslVerify, args.GitlabGroup, args.Minimal);
        await WriteToFile("groups.csv", groupsCsvText);
        _log.LogSuccess("groups.csv generated");

        _log.LogInformation("Generating projects.csv...");
        var projectsCsvText = await _projectsCsvGenerator.Generate(args.GitlabServerUrl, args.GitlabUsername, args.GitlabPassword, args.NoSslVerify, args.GitlabGroup, args.Minimal);
        await WriteToFile("projects.csv", projectsCsvText);
        _log.LogSuccess("projects.csv generated");
    }
}
