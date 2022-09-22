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

    public async Task Handle(GenerateMannequinCsvCommandArgs args)
    {
        if (args is null)
        {
            throw new ArgumentNullException(nameof(args));
        }

        _log.Verbose = args.Verbose;

        _log.LogInformation("Generating CSV...");

        _log.LogInformation($"{GithubOrg.GetLogFriendlyName()}: {args.GithubOrg}");
        if (args.GithubPat is not null)
        {
            _log.LogInformation($"{GithubPat.GetLogFriendlyName()}: ***");
        }

        _log.LogInformation($"{Output.GetLogFriendlyName()}: {args.Output}");
        if (args.IncludeReclaimed)
        {
            _log.LogInformation($"{IncludeReclaimed.GetLogFriendlyName()}: true");
        }

        _log.RegisterSecret(args.GithubPat);

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

public class GenerateMannequinCsvCommandArgs
{
    public string GithubOrg { get; set; }
    public FileInfo Output { get; set; }
    public bool IncludeReclaimed { get; set; }
    public string GithubPat { get; set; }
    public bool Verbose { get; set; }
}
