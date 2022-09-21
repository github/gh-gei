using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Octoshift;
using OctoshiftCLI.Contracts;

namespace OctoshiftCLI.Commands;

public class GenerateMannequinCsvCommandBaseHandler
{
    internal Func<string, string, Task> WriteToFile = async (path, contents) => await File.WriteAllTextAsync(path, contents);

    private readonly OctoLogger _log;
    private readonly ITargetGithubApiFactory _targetGithubApiFactory;

    public GenerateMannequinCsvCommandBaseHandler(OctoLogger log, ITargetGithubApiFactory targetGithubApiFactory)
    {
        _log = log;
        _targetGithubApiFactory = targetGithubApiFactory;
    }

    public async Task Handle(GenerateMannequinCsvCommandArgs args)
    {
        if (args is null)
        {
            throw new ArgumentNullException(nameof(args));
        }

        _log.Verbose = args.Verbose;

        _log.LogInformation("Generating CSV...");

        _log.LogInformation($"GITHUB ORG: {args.GithubOrg}");
        if (args.GithubPat is not null)
        {
            _log.LogInformation($"GITHUB PAT: ***");
        }

        _log.LogInformation($"OUTPUT: {args.Output}");
        if (args.IncludeReclaimed)
        {
            _log.LogInformation($"INCLUDE RECLAIMED: true");
        }

        var githubApi = _targetGithubApiFactory.Create(targetPersonalAccessToken: args.GithubPat);

        var githubOrgId = await githubApi.GetOrganizationId(args.GithubOrg);
        var mannequins = await githubApi.GetMannequins(githubOrgId);

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
