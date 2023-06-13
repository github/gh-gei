using System;
using System.CommandLine;
using Microsoft.Extensions.DependencyInjection;
using OctoshiftCLI.BbsToGithub.Factories;
using OctoshiftCLI.Commands;
using OctoshiftCLI.Services;

namespace OctoshiftCLI.BbsToGithub.Commands.InventoryReport
{
    public class InventoryReportCommand : CommandBase<InventoryReportCommandArgs, InventoryReportCommandHandler>
    {
        public InventoryReportCommand() : base(
                name: "inventory-report",
                description: "Generates several CSV files containing lists of BBS projects and repos. Useful for planning large migrations." +
                             Environment.NewLine +
                             "Note: Expects BBS_USERNAME and BBS_PASSWORD env variables or --bbs-username and --bbs-password options to be set.")
        {
            AddOption(BbsServerUrl);
            AddOption(BbsProject);
            AddOption(BbsUsername);
            AddOption(BbsPassword);
            AddOption(NoSslVerify);
            AddOption(Minimal);
            AddOption(Verbose);
        }

        public Option<string> BbsServerUrl { get; } = new(
            name: "--bbs-server-url",
            description: "The full URL of the Bitbucket Server/Data Center. E.g. http://bitbucket.contoso.com:7990")
        { IsRequired = true };

        public Option<string> BbsProject { get; } = new(
            name: "--bbs-project",
            description: "If not provided will iterate over all projects that the user has access to.");

        public Option<string> BbsUsername { get; } = new(
            name: "--bbs-username",
            description: "The Bitbucket username of a user with site admin privileges. If not set will be read from BBS_USERNAME environment variable.")
        { IsRequired = true };

        public Option<string> BbsPassword { get; } = new(
            name: "--bbs-password",
            description: "The Bitbucket password of the user specified by --bbs-username. If not set will be read from BBS_PASSWORD environment variable.")
        { IsRequired = true };

        public Option<bool> NoSslVerify { get; } = new(
            name: "--no-ssl-verify",
            description: "Disables SSL verification when communicating with your Bitbucket Server/Data Center instance. " +
                        "If your Bitbucket instance has a self-signed SSL certificate then setting this flag will allow data to be extracted.");

        public Option<bool> Minimal { get; } = new(
            name: "--minimal",
            description: "Significantly speeds up the generation of the CSV files by including the bare minimum info.");

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
            var bbsApiFactory = sp.GetRequiredService<BbsApiFactory>();
            var bbsApi = bbsApiFactory.Create(args.BbsServerUrl, args.BbsUsername, args.BbsPassword, args.NoSslVerify);
            var bbsInspectorServiceFactory = sp.GetRequiredService<BbsInspectorServiceFactory>();
            var bbsInspectorService = bbsInspectorServiceFactory.Create(bbsApi);
            var projectsCsvGeneratorService = sp.GetRequiredService<ProjectsCsvGeneratorService>();
            var reposCsvGeneratorService = sp.GetRequiredService<ReposCsvGeneratorService>();

            return new InventoryReportCommandHandler(
                log,
                bbsApi,
                bbsInspectorService,
                projectsCsvGeneratorService,
                reposCsvGeneratorService);
        }
    }
}
