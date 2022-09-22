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
        Handler = CommandHandler.Create<string, string, string, string, string, bool, string, bool>(Invoke);
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

    public async Task Invoke(
        string githubTargetOrg,
        string csv,
        string mannequinUser,
        string mannequinId,
        string targetUser,
        bool force = false,
        string githubTargetPat = null,
        bool verbose = false) => await Handle(githubTargetOrg, csv, mannequinUser, mannequinId, targetUser, force, githubTargetPat, verbose);
}
