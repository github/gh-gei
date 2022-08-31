using System;
using System.CommandLine;
using System.CommandLine.Invocation;
using System.IO;
using System.Threading.Tasks;
using OctoshiftCLI.Contracts;
using OctoshiftCLI.Extensions;

namespace OctoshiftCLI.BbsToGithub.Commands;

public class GenerateScriptCommand : Command
{
    private readonly OctoLogger _log;
    private readonly IVersionProvider _versionProvider;
    private readonly FileSystemProvider _fileSystemProvider;

    public GenerateScriptCommand(OctoLogger log, IVersionProvider versionProvider, FileSystemProvider fileSystemProvider) : base("generate-script")
    {
        _log = log;
        _versionProvider = versionProvider;
        _fileSystemProvider = fileSystemProvider;

        Description = "Generates a migration script. This provides you the ability to review the steps that this tool will take, and optionally modify the script if desired before running it.";

        var bbsServerUrl = new Option<string>("--bbs-server-url") { IsRequired = true, Description = "The full URL of the Bitbucket Server/Data Center to migrate from." };
        var githubOrg = new Option<string>("--github-org") { IsRequired = true };
        var sequential = new Option<bool>("--sequential") { IsRequired = false, Description = "Waits for each migration to finish before moving on to the next one." };
        var output = new Option<FileInfo>("--output", () => new FileInfo("./migrate.ps1")) { IsRequired = false };
        var verbose = new Option<bool>("--verbose") { IsRequired = false };

        AddOption(bbsServerUrl);
        AddOption(githubOrg);
        AddOption(sequential);
        AddOption(output);
        AddOption(verbose);

        Handler = CommandHandler.Create<GenerateScriptCommandArgs>(Invoke);
    }

    public async Task Invoke(GenerateScriptCommandArgs args)
    {
        if (args is null)
        {
            throw new ArgumentNullException(nameof(args));
        }

        _log.Verbose = args.Verbose;

        _log.LogInformation("Generating Script...");

        LogOptions(args);

        var script = "this is a sample script";

        if (script.HasValue() && args.Output.HasValue())
        {
            await _fileSystemProvider.WriteAllTextAsync(args.Output.FullName, script);
        }
    }

    private void LogOptions(GenerateScriptCommandArgs args)
    {
        if (args.BbsServerUrl.HasValue())
        {
            _log.LogInformation($"BBS SERVER URL: {args.BbsServerUrl}");
        }

        if (args.GithubOrg.HasValue())
        {
            _log.LogInformation($"GITHUB ORG: {args.GithubOrg}");
        }

        if (args.Output.HasValue())
        {
            _log.LogInformation($"OUTPUT: {args.Output}");
        }

        if (args.Sequential.HasValue())
        {
            _log.LogInformation($"SEQUENTIAL: {args.Sequential}");
        }
    }
}

public class GenerateScriptCommandArgs
{
    public string BbsServerUrl { get; set; }
    public string GithubOrg { get; set; }
    public bool Sequential { get; set; }
    public FileInfo Output { get; set; }
    public bool Verbose { get; set; }
}
