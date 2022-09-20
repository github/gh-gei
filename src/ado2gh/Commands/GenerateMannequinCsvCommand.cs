﻿using System.CommandLine.NamingConventionBinder;
using OctoshiftCLI.Commands;
using OctoshiftCLI.Contracts;

namespace OctoshiftCLI.AdoToGithub.Commands;

public sealed class GenerateMannequinCsvCommand : GenerateMannequinCsvCommandBase
{
    public GenerateMannequinCsvCommand(OctoLogger log, ITargetGithubApiFactory githubApiFactory) : base(log, githubApiFactory)
    {
        AddOptions();
        Handler = CommandHandler.Create<GenerateMannequinCsvCommandArgs>(Handle);
    }
}
