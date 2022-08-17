using System.CommandLine.Invocation;
using Octoshift;
using OctoshiftCLI.Commands;
using OctoshiftCLI.Contracts;

namespace OctoshiftCLI.AdoToGithub.Commands;

public sealed class ReclaimMannequinCommand : ReclaimMannequinCommandBase
{
    public ReclaimMannequinCommand(OctoLogger log, ITargetGithubApiFactory githubApiFactory, ReclaimService reclaimService = null) : base(log, githubApiFactory, reclaimService)
    {
        AddOptions();
        Handler = CommandHandler.Create<string, string, string, string, string, bool, string, bool>(Handle);
    }
}
