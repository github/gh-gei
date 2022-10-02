using System.CommandLine;
using System.IO;
using OctoshiftCLI.Contracts;
using OctoshiftCLI.Handlers;

namespace OctoshiftCLI.Commands;

public class GenerateMannequinCsvCommandBase : Command
{
    protected GenerateMannequinCsvCommandHandler BaseHandler { get; init; }

    public GenerateMannequinCsvCommandBase(OctoLogger log, ITargetGithubApiFactory targetGithubApiFactory) : base(
        name: "generate-mannequin-csv",
        description: "Generates a CSV with unreclaimed mannequins to reclaim them in bulk.")
    {
        BaseHandler = new GenerateMannequinCsvCommandHandler(log, targetGithubApiFactory);
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
}

public class GenerateMannequinCsvCommandArgs
{
    public string GithubOrg { get; set; }
    public FileInfo Output { get; set; }
    public bool IncludeReclaimed { get; set; }
    public string GithubPat { get; set; }
    public bool Verbose { get; set; }
}
