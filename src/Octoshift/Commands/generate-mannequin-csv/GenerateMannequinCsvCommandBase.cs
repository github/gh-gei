using System;
using System.CommandLine;
using System.IO;
using Microsoft.Extensions.DependencyInjection;
using OctoshiftCLI.Contracts;
using OctoshiftCLI.Handlers;

namespace OctoshiftCLI.Commands;

public class GenerateMannequinCsvCommandBase : CommandBase<GenerateMannequinCsvCommandArgs, GenerateMannequinCsvCommandHandler>
{
    public GenerateMannequinCsvCommandBase() : base(
        name: "generate-mannequin-csv",
        description: "Generates a CSV with unreclaimed mannequins to reclaim them in bulk.")
    {
    }

    public virtual Option<string> GithubOrg { get; } = new("--github-org")
    {
        IsRequired = true,
        Description = "Uses GH_PAT env variable."
    };

    public virtual Option<FileInfo> Output { get; } = new("--output", () => new FileInfo("./mannequins.csv"))
    {
        Description = "Output filename. Default value mannequins.csv"
    };

    public virtual Option<bool> IncludeReclaimed { get; } = new("--include-reclaimed")
    {
        Description = "Include mannequins that have already been reclaimed"
    };

    public virtual Option<bool> Verbose { get; } = new("--verbose");

    public virtual Option<string> GithubPat { get; } = new("--github-pat")
    {
        Description = "Personal access token of the GitHub target. Overrides GH_PAT environment variable."
    };

    public override GenerateMannequinCsvCommandHandler BuildHandler(GenerateMannequinCsvCommandArgs args, IServiceProvider sp)
    {
        if (args is null)
        {
            throw new ArgumentNullException(nameof(args));
        }

        if (sp is null)
        {
            throw new ArgumentNullException(nameof(sp));
        }

        var log = sp.GetRequiredService<OctoLogger>();
        var githubApiFactory = sp.GetRequiredService<ITargetGithubApiFactory>();
        var githubApi = githubApiFactory.Create(targetPersonalAccessToken: args.GithubPat);

        return new GenerateMannequinCsvCommandHandler(log, githubApi);
    }

    protected void AddOptions()
    {
        AddOption(GithubOrg);
        AddOption(Output);
        AddOption(IncludeReclaimed);
        AddOption(GithubPat);
        AddOption(Verbose);
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
