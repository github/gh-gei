using System;
using System.CommandLine;
using Microsoft.Extensions.DependencyInjection;
using OctoshiftCLI.Commands;
using OctoshiftCLI.GitlabToGithub.Factories;
using OctoshiftCLI.Services;

namespace OctoshiftCLI.GitlabToGithub.Commands.DiagnoseGitlabExport;

public class DiagnoseGitlabExportCommand : CommandBase<DiagnoseGitlabExportCommandArgs, DiagnoseGitlabExportCommandHandler>
{
    public DiagnoseGitlabExportCommand() : base(
        name: "diagnose-gitlab-export",
        description: "Collects GitLab project export diagnostics and writes a report with GitLab admin log commands.")
    {
        AddOption(GitlabServerUrl);
        AddOption(GitlabGroup);
        AddOption(GitlabProject);
        AddOption(GitlabPat);
        AddOption(Output);
        AddOption(Overwrite);
        AddOption(NoSslVerify);
        AddOption(Verbose);
    }

    public Option<string> GitlabServerUrl { get; } = new(
        name: "--gitlab-server-url",
        description: "The full URL of the GitLab server, e.g. https://gitlab.mycompany.com");

    public Option<string> GitlabGroup { get; } = new(
        name: "--gitlab-group",
        description: "The GitLab group (full namespace path) that contains the project.");

    public Option<string> GitlabProject { get; } = new(
        name: "--gitlab-project",
        description: "The GitLab project to diagnose.");

    public Option<string> GitlabPat { get; } = new(
        name: "--gitlab-pat",
        description: "The GitLab PAT. If not passed, it will read the PAT from the GITLAB_PAT environment variable.");

    public Option<string> Output { get; } = new(
        name: "--output",
        description: "Local Markdown file to write diagnostics to.");

    public Option<bool> Overwrite { get; } = new(
        name: "--overwrite",
        description: "Overwrite the output file if it exists.");

    public Option<bool> NoSslVerify { get; } = new(
        name: "--no-ssl-verify",
        description: "Disables SSL verification when communicating with your GitLab instance.");

    public Option<bool> Verbose { get; } = new("--verbose");

    public override DiagnoseGitlabExportCommandHandler BuildHandler(DiagnoseGitlabExportCommandArgs args, IServiceProvider sp)
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
        var gitlabApiFactory = sp.GetRequiredService<GitlabApiFactory>();
        var gitlabApi = gitlabApiFactory.Create(args.GitlabServerUrl, args.GitlabPat, args.NoSslVerify);
        var fileSystemProvider = sp.GetRequiredService<FileSystemProvider>();

        return new DiagnoseGitlabExportCommandHandler(log, gitlabApi, fileSystemProvider);
    }
}
