using System;
using System.CommandLine;
using Microsoft.Extensions.DependencyInjection;
using OctoshiftCLI.GitlabToGithub.Factories;
using OctoshiftCLI.Commands;
using OctoshiftCLI.Services;

namespace OctoshiftCLI.GitlabToGithub.Commands.InventoryReport
{
    public class InventoryReportCommand : CommandBase<InventoryReportCommandArgs, InventoryReportCommandHandler>
    {
        public InventoryReportCommand() : base(
                name: "inventory-report",
                description: "Generates several CSV files containing lists of BBS projects and repos. Useful for planning large migrations. Personal repositories owned by individual users will not be included." +
                             Environment.NewLine +
                             "Note: Expects BBS_USERNAME and BBS_PASSWORD env variables or --gitlab-username and --gitlab-password options to be set.")
        {
            AddOption(GitlabServerUrl);
            AddOption(GitlabProject);
            AddOption(GitlabUsername);
            AddOption(GitlabPassword);
            AddOption(NoSslVerify);
            AddOption(Minimal);
            AddOption(Verbose);
        }

        public Option<string> GitlabServerUrl { get; } = new(
            name: "--gitlab-server-url",
            description: "The full URL of the Bitbucket Server/Data Center. E.g. http://bitbucket.contoso.com:7990")
        { IsRequired = true };

        public Option<string> GitlabProject { get; } = new(
            name: "--gitlab-project",
            description: "The Bitbucket project key. If not provided will iterate over all projects that the user has access to.");

        public Option<string> GitlabUsername { get; } = new(
            name: "--gitlab-username",
            description: "The Bitbucket username of a user with site admin privileges. If not set will be read from BBS_USERNAME environment variable.");

        public Option<string> GitlabPassword { get; } = new(
            name: "--gitlab-password",
            description: "The Bitbucket password of the user specified by --gitlab-username. If not set will be read from BBS_PASSWORD environment variable.");

        public Option<bool> NoSslVerify { get; } = new(
            name: "--no-ssl-verify",
            description: "Disables SSL verification when communicating with your Bitbucket Server/Data Center instance. " +
                        "If your Bitbucket instance has a self-signed SSL certificate then setting this flag will allow data to be extracted.");

        public Option<bool> Minimal { get; } = new(
            name: "--minimal",
            description: "Significantly speeds up the generation of the CSV files by including the bare minimum info. Will omit the archived state and PR count for repos and the PR count for projects.");

        public Option<bool> Verbose { get; } = new("--verbose");

        public override InventoryReportCommandHandler BuildHandler(InventoryReportCommandArgs args, IServiceProvider sp)
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
            var gitlabApi = gitlabApiFactory.Create(args.GitlabServerUrl, args.GitlabUsername, args.GitlabPassword, args.NoSslVerify);
            var gitlabInspectorServiceFactory = sp.GetRequiredService<GitlabInspectorServiceFactory>();
            var gitlabInspectorService = gitlabInspectorServiceFactory.Create(gitlabApi);
            var groupsCsvGeneratorService = sp.GetRequiredService<GroupsCsvGeneratorService>();
            var projectsCsvGeneratorService = sp.GetRequiredService<ProjectsCsvGeneratorService>();

            return new InventoryReportCommandHandler(
                log,
                gitlabApi,
                gitlabInspectorService,
                groupsCsvGeneratorService,
                projectsCsvGeneratorService);
        }
    }
}
