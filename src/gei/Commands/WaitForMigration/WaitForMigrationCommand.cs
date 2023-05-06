using System;
using System.CommandLine;
using OctoshiftCLI.Commands.WaitForMigration;

namespace OctoshiftCLI.GithubEnterpriseImporter.Commands.WaitForMigration;

public sealed class WaitForMigrationCommand : WaitForMigrationCommandBase
{
    public WaitForMigrationCommand() : base()
    {
        Description += Environment.NewLine;
        Description += $"Note: Expects GH_PAT env variable or --{GithubPat.Name} option to be set.";

        AddOptions();
    }

    public override Option<string> GithubPat { get; } = new("--github-target-pat") { IsRequired = false };
}
