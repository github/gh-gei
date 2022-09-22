using System.CommandLine;
using System.CommandLine.NamingConventionBinder;
using System.Threading.Tasks;
using Octoshift;
using OctoshiftCLI.Commands;
using OctoshiftCLI.Contracts;

namespace OctoshiftCLI.GithubEnterpriseImporter.Commands;

public sealed class ReclaimMannequinCommand : ReclaimMannequinCommandBase
{
    public ReclaimMannequinCommand(OctoLogger log, ITargetGithubApiFactory targetGithubApiFactory, ReclaimService reclaimService = null) : base(log, targetGithubApiFactory, reclaimService)
    {
        AddOptions();
        Handler = CommandHandler.Create<ReclaimMannequinCommandArgs>(Invoke);
    }

    protected override Option<string> GithubOrg { get; } = new("--github-target-org")
    {
        IsRequired = true,
        Description = "Uses GH_PAT env variable."
    };

    protected override Option<string> GithubPat { get; } = new("--github-target-pat")
    {
        IsRequired = false
    };

    internal async Task Invoke(ReclaimMannequinCommandArgs args) => await BaseHandler.Handle(new OctoshiftCLI.Commands.ReclaimMannequinCommandArgs
    {
        GithubOrg = args.GithubTargetOrg,
        Csv = args.Csv,
        MannequinUser = args.MannequinUser,
        MannequinId = args.MannequinId,
        TargetUser = args.TargetUser,
        Force = args.Force,
        GithubPat = args.GithubTargetPat,
        Verbose = args.Verbose,
    });
}

public class ReclaimMannequinCommandArgs
{
    public string GithubTargetOrg { get; set; }
    public string Csv { get; set; }
    public string MannequinUser { get; set; }
    public string MannequinId { get; set; }
    public string TargetUser { get; set; }
    public bool Force { get; set; }
    public string GithubTargetPat { get; set; }
    public bool Verbose { get; set; }
}
