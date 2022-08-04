using System.CommandLine;
using OctoshiftCLI.Commands;
using OctoshiftCLI.Contracts;

namespace OctoshiftCLI.GithubEnterpriseImporter.Commands;

public class WaitForMigrationCommand : WaitForMigrationCommandBase
{
    public WaitForMigrationCommand(OctoLogger log, ITargetGithubApiFactory targetGithubApiFactory) : base(log, targetGithubApiFactory) { }

    protected override Option<string> GithubPat { get; } = new("--github-target-pat") { IsRequired = false };
}
