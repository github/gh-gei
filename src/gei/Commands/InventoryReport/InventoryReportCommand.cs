using System;
using System.CommandLine;
using Microsoft.Extensions.DependencyInjection;
using OctoshiftCLI.Commands;
using OctoshiftCLI.Contracts;
using OctoshiftCLI.Extensions;
using OctoshiftCLI.GithubEnterpriseImporter.Factories;
using OctoshiftCLI.GithubEnterpriseImporter.Services;
using OctoshiftCLI.Services;

namespace OctoshiftCLI.GithubEnterpriseImporter.Commands.InventoryReport
{
    public class InventoryReportCommand : CommandBase<InventoryReportCommandArgs, InventoryReportCommandHandler>
    {
        public InventoryReportCommand() : base(
                name: "inventory-report",
                description: "Generates a CSV file containing lists of repos and some statistics useful for planning large migrations." +
                             Environment.NewLine +
                             "Note: Expects GH_PAT env variable or --github-pat option to be set.")
        {
            AddOption(GithubOrg);
            AddOption(GithubPat);
            AddOption(Minimal);
            AddOption(GhesApiUrl);
            AddOption(NoSslVerify);
            AddOption(Verbose);
        }

        public Option<string> GithubOrg { get; } = new("--github-org")
        {
            IsRequired = true,
        };
        public Option<string> GithubPat { get; } = new("--github-pat");
        public Option<bool> Minimal { get; } = new("--minimal")
        {
            Description = "Significantly speeds up the generation of the CSV file by including the bare minimum info."
        };
        public Option<string> GhesApiUrl { get; } = new("--ghes-api-url")
        {
            Description = "Required if migrating from GHES. The api endpoint for the hostname of your GHES instance. For example: http(s)://myghes.com/api/v3"
        };
        public Option<bool> NoSslVerify { get; } = new("--no-ssl-verify")
        {
            Description = "Only effective if migrating from GHES. Disables SSL verification when communicating with your GHES instance. All other migration steps will continue to verify SSL. If your GHES instance has a self-signed SSL certificate then setting this flag will allow data to be extracted."
        };
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
            var fileSystemProvider = sp.GetRequiredService<FileSystemProvider>();
            var githubApiFactory = sp.GetRequiredService<ISourceGithubApiFactory>();
            var githubApi = args.GhesApiUrl.HasValue() && args.NoSslVerify ?
                    githubApiFactory.CreateClientNoSsl(args.GhesApiUrl, args.GithubPat) :
                    githubApiFactory.Create(args.GhesApiUrl, args.GithubPat);
            var githubInspectorServiceFactory = sp.GetRequiredService<GithubInspectorServiceFactory>();
            var githubInspectorService = githubInspectorServiceFactory.Create(githubApi);
            var reposCsvGeneratorService = sp.GetRequiredService<ReposCsvGeneratorService>();

            return new InventoryReportCommandHandler(
                log,
                fileSystemProvider,
                githubInspectorService,
                reposCsvGeneratorService);
        }
    }
}
