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
    private readonly GitlabInspectorService _gitlabInspectorService;
    private readonly GroupsCsvGeneratorService _groupsCsvGenerator;
    private readonly ProjectsCsvGeneratorService _projectsCsvGenerator;

    public InventoryReportCommandHandler(
        OctoLogger log,
        GitlabApi gitlabApi,
        GitlabInspectorService gitlabInspectorService,
        GroupsCsvGeneratorService groupsCsvGeneratorService,
        ProjectsCsvGeneratorService projectsCsvGeneratorService)
    {
        _log = log;
        _gitlabApi = gitlabApi;
        _gitlabInspectorService = gitlabInspectorService;
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

        await _gitlabApi.LogServerVersion();

        var groupPaths = Array.Empty<string>();
        if (string.IsNullOrWhiteSpace(args.GitlabGroup))
        {
            _log.LogInformation("Finding Groups...");
            var groups = await _gitlabApi.GetGroups();
            groupPaths = groups.Select(x => x.Path).ToArray();
            _log.LogInformation($"Found {groups.Count()} Groups");
        }

        _log.LogInformation("Finding Projects...");
        var projectCount = string.IsNullOrWhiteSpace(args.GitlabGroup) ? await _gitlabInspectorService.GetProjectCount(groupPaths) : await _gitlabInspectorService.GetProjectCount(args.GitlabGroup);
        _log.LogInformation($"Found {projectCount} Projects");

        _log.LogInformation("Generating data for groups.csv...");
        var groupsCsvText = await _groupsCsvGenerator.Generate(args.GitlabServerUrl, args.GitlabPat, args.NoSslVerify, args.GitlabGroup, args.Minimal);
        await WriteToFile("groups.csv", groupsCsvText);
        _log.LogSuccess("groups.csv generated");

        _log.LogInformation("Generating projects.csv...");
        var projectsCsvText = await _projectsCsvGenerator.Generate(args.GitlabServerUrl, args.GitlabPat, args.NoSslVerify, args.GitlabGroup, args.Minimal);
        await WriteToFile("projects.csv", projectsCsvText);
        _log.LogSuccess("projects.csv generated");
    }
}
