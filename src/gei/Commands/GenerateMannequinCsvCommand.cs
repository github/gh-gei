﻿using System.CommandLine;
using System.CommandLine.NamingConventionBinder;
using System.IO;
using System.Threading.Tasks;
using OctoshiftCLI.Commands;
using OctoshiftCLI.Contracts;

namespace OctoshiftCLI.GithubEnterpriseImporter.Commands;

public sealed class GenerateMannequinCsvCommand : GenerateMannequinCsvCommandBase
{
    public GenerateMannequinCsvCommand(OctoLogger log, ITargetGithubApiFactory targetGithubApiFactory) : base(log, targetGithubApiFactory)
    {
        AddOptions();
        Handler = CommandHandler.Create<GenerateMannequinCsvCommandArgs>(Invoke);
    }

    protected override Option<string> GithubOrg { get; } = new("--github-target-org")
    {
        IsRequired = true,
        Description = "Uses GH_PAT env variable."
    };

    protected override Option<string> GithubPat { get; } = new("--github-target-pat") { IsRequired = false };

    internal async Task Invoke(GenerateMannequinCsvCommandArgs args) =>
        await BaseHandler.Handle(new OctoshiftCLI.Commands.GenerateMannequinCsvCommandArgs
        {
            GithubOrg = args.GithubTargetOrg,
            Output = args.Output,
            IncludeReclaimed = args.IncludeReclaimed,
            GithubPat = args.GithubTargetPat,
            Verbose = args.Verbose
        });
}

public class GenerateMannequinCsvCommandArgs
{
    public string GithubTargetOrg { get; set; }
    public FileInfo Output { get; set; }
    public bool IncludeReclaimed { get; set; }
    public string GithubTargetPat { get; set; }
    public bool Verbose { get; set; }
}
