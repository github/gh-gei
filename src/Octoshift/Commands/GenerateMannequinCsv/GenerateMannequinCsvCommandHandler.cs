using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using OctoshiftCLI.Services;

namespace OctoshiftCLI.Commands.GenerateMannequinCsv;

public class GenerateMannequinCsvCommandHandler : ICommandHandler<GenerateMannequinCsvCommandArgs>
{
    internal Func<string, string, Task> WriteToFile = async (path, contents) => await File.WriteAllTextAsync(path, contents);

    private readonly OctoLogger _log;
    private readonly GithubApi _githubApi;

    public GenerateMannequinCsvCommandHandler(OctoLogger log, GithubApi githubApi)
    {
        _log = log;
        _githubApi = githubApi;
    }

    public async Task Handle(GenerateMannequinCsvCommandArgs args)
    {
        if (args is null)
        {
            throw new ArgumentNullException(nameof(args));
        }

        _log.LogInformation("Generating CSV...");

        var githubOrgId = await _githubApi.GetOrganizationId(args.GithubOrg);
        var mannequins = await _githubApi.GetMannequins(githubOrgId);

        _log.LogInformation($"    # Mannequins Found: {mannequins.Count()}");
        _log.LogInformation($"    # Mannequins Previously Reclaimed: {mannequins.Count(x => x.MappedUser is not null)}");

        var contents = new StringBuilder().AppendLine(ReclaimService.CSVHEADER);
        foreach (var mannequin in mannequins.Where(m => args.IncludeReclaimed || m.MappedUser is null))
        {
            contents.AppendLine($"{mannequin.Login},{mannequin.Id},{mannequin.MappedUser?.Login}");
        }

        if (args.Output?.FullName is not null)
        {
            await WriteToFile(args.Output.FullName, contents.ToString());
        }
    }
}
