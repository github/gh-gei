using System;
using System.CommandLine;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Octoshift;
using OctoshiftCLI.Contracts;
using OctoshiftCLI.Extensions;

namespace OctoshiftCLI.Commands;

public class GenerateMannequinCsvCommandBase : Command
{
    internal Func<string, string, Task> WriteToFile = async (path, contents) => await File.WriteAllTextAsync(path, contents);

    private readonly OctoLogger _log;
    private readonly ITargetGithubApiFactory _targetGithubApiFactory;

    public GenerateMannequinCsvCommandBase(OctoLogger log, ITargetGithubApiFactory targetGithubApiFactory) : base(
        name: "generate-mannequin-csv",
        description: "Generates a CSV with unreclaimed mannequins to reclaim them in bulk.")
    {
        _log = log;
        _targetGithubApiFactory = targetGithubApiFactory;
    }

    protected virtual Option<string> GithubOrg { get; } = new("--github-org")
    {
        IsRequired = true,
        Description = "Uses GH_PAT env variable."
    };

    protected virtual Option<FileInfo> Output { get; } = new("--output", () => new FileInfo("./mannequins.csv"))
    {
        IsRequired = false,
        Description = "Output filename. Default value mannequins.csv"
    };

    protected virtual Option<bool> IncludeReclaimed { get; } = new("--include-reclaimed")
    {
        IsRequired = false,
        Description = "Include mannequins that have already been reclaimed"
    };

    protected virtual Option<bool> Verbose { get; } = new("--verbose")
    {
        IsRequired = false
    };

    protected virtual Option<string> GithubPat { get; } = new("--github-pat")
    {
        IsRequired = false
    };

    protected void AddOptions()
    {
        AddOption(GithubOrg);
        AddOption(Output);
        AddOption(IncludeReclaimed);
        AddOption(GithubPat);
        AddOption(Verbose);
    }

    public async Task Handle(
        string githubOrg,
        FileInfo output,
        bool includeReclaimed = false,
        string githubPat = null,
        bool verbose = false)
    {
        _log.Verbose = verbose;

        _log.LogInformation("Generating CSV...");

        _log.LogInformation($"{GithubOrg.GetLogFriendlyName()}: {githubOrg}");
        if (githubPat is not null)
        {
            _log.LogInformation($"{GithubPat.GetLogFriendlyName()}: ***");
        }

        _log.LogInformation($"{Output.GetLogFriendlyName()}: {output}");
        if (includeReclaimed)
        {
            _log.LogInformation($"{IncludeReclaimed.GetLogFriendlyName()}: true");
        }

        var githubApi = _targetGithubApiFactory.Create(targetPersonalAccessToken: githubPat);

        var githubOrgId = await githubApi.GetOrganizationId(githubOrg);
        var mannequins = await githubApi.GetMannequins(githubOrgId);

        _log.LogInformation($"    # Mannequins Found: {mannequins.Count()}");
        _log.LogInformation($"    # Mannequins Previously Reclaimed: {mannequins.Count(x => x.MappedUser is not null)}");

        var contents = new StringBuilder().AppendLine(ReclaimService.CSVHEADER);
        foreach (var mannequin in mannequins.Where(m => includeReclaimed || m.MappedUser is null))
        {
            contents.AppendLine($"{mannequin.Login},{mannequin.Id},{mannequin.MappedUser?.Login}");
        }

        if (output?.FullName is not null)
        {
            await WriteToFile(output.FullName, contents.ToString());
        }
    }
}
