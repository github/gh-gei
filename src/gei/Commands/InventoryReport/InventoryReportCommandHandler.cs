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
    private readonly ReposCsvGeneratorService _reposCsvGenerator;

    public InventoryReportCommandHandler(
        OctoLogger log,
        FileSystemProvider fileSystemProvider,
        ReposCsvGeneratorService reposCsvGeneratorService)
    {
        _log = log;
        _fileSystemProvider = fileSystemProvider;
        _reposCsvGenerator = reposCsvGeneratorService;
    }

    public async Task Handle(InventoryReportCommandArgs args)
    {
        if (args is null)
        {
            throw new ArgumentNullException(nameof(args));
        }

        _log.LogInformation("Creating inventory report...");
        _log.LogInformation("This may take a long time, from several minutes to many hours depending on the number and size of repos");

        _log.LogInformation("Generating repos.csv...");
        var reposCsvText = await _reposCsvGenerator.Generate(args.GhesApiUrl, args.GithubOrg, args.Minimal);
        await _fileSystemProvider.WriteAllTextAsync("repos.csv", reposCsvText);
        _log.LogSuccess("repos.csv generated");
    }
}
