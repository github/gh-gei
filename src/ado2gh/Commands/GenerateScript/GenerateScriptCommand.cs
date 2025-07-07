using System;
using System.CommandLine;
using System.IO;
using Microsoft.Extensions.DependencyInjection;
using OctoshiftCLI.AdoToGithub.Factories;
using OctoshiftCLI.Commands;
using OctoshiftCLI.Contracts;
using OctoshiftCLI.Services;

namespace OctoshiftCLI.AdoToGithub.Commands.GenerateScript
{
    public class GenerateScriptCommand : CommandBase<GenerateScriptCommandArgs, GenerateScriptCommandHandler>
    {
        public GenerateScriptCommand() : base(
            name: "generate-script",
            description: "Generates a migration script. This provides you the ability to review the steps that this tool will take, and optionally modify the script if desired before running it." +
                         Environment.NewLine +
                         "Note: Expects ADO_PAT env variable or --ado-pat option to be set.")
        {
            AddOption(GithubOrg);
            AddOption(TargetApiUrl);
            AddOption(AdoOrg);
            AddOption(AdoTeamProject);
            AddOption(AdoServerUrl);
            AddOption(Output);
            AddOption(Sequential);
            AddOption(AdoPat);
            AddOption(Verbose);
            AddOption(DownloadMigrationLogs);
            AddOption(CreateTeams);
            AddOption(LinkIdpGroups);
            AddOption(LockAdoRepos);
            AddOption(DisableAdoRepos);
            AddOption(RewirePipelines);
            AddOption(All);
            AddOption(RepoList);
        }

        public Option<string> GithubOrg { get; } = new("--github-org")
        {
            IsRequired = true
        };
        public Option<string> AdoOrg { get; } = new("--ado-org");
        public Option<string> AdoTeamProject { get; } = new("--ado-team-project");
        public Option<string> AdoServerUrl { get; } = new("--ado-server-url")
        {
            IsHidden = true,
            Description = "Required if migrating from ADO Server. E.g. https://myadoserver.contoso.com. When migrating from ADO Server, --ado-source-org represents the collection name."
        };
        public Option<FileInfo> Output { get; } = new("--output", () => new FileInfo("./migrate.ps1"));
        public Option<bool> Sequential { get; } = new("--sequential")
        {
            Description = "Waits for each migration to finish before moving on to the next one."
        };
        public Option<string> AdoPat { get; } = new("--ado-pat");
        public Option<bool> Verbose { get; } = new("--verbose");
        public Option<bool> DownloadMigrationLogs { get; } = new("--download-migration-logs")
        {
            Description = "Downloads the migration log for each repository migration."
        };

        public Option<bool> CreateTeams { get; } = new("--create-teams")
        {
            Description = "Includes create-team scripts that creates admins and maintainers teams and adds them to repos."
        };
        public Option<bool> LinkIdpGroups { get; } = new("--link-idp-groups")
        {
            Description = "Adds --idp-group to the end of create teams scripts that links the created team to an idP group."
        };
        public Option<bool> LockAdoRepos { get; } = new("--lock-ado-repos")
        {
            Description = "Includes lock-ado-repo scripts that lock repos before migrating them."
        };
        public Option<bool> DisableAdoRepos { get; } = new("--disable-ado-repos")
        {
            Description = "Includes disable-ado-repo scripts that disable repos after migrating them."
        };
        public Option<bool> RewirePipelines { get; } = new("--rewire-pipelines")
        {
            Description = "Includes share-service-connection and rewire-pipeline scripts that rewire Azure Pipelines to point to GitHub repos."
        };
        public Option<bool> All { get; } = new("--all")
        {
            Description = "Includes all script generation options."
        };
        public Option<FileInfo> RepoList { get; } = new("--repo-list")
        {
            Description = "Path to a csv file that contains a list of repos to generate a script for. The CSV file should be generated using the inventory-report command."
        };
        public Option<string> TargetApiUrl { get; } = new("--target-api-url")
        {
            Description = "The URL of the target API, if not migrating to github.com. Defaults to https://api.github.com"
        };

        public override GenerateScriptCommandHandler BuildHandler(GenerateScriptCommandArgs args, IServiceProvider sp)
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
            var adoApiFactory = sp.GetRequiredService<AdoApiFactory>();
            var versionProvider = sp.GetRequiredService<IVersionProvider>();
            var adoInspectorServiceFactory = sp.GetRequiredService<AdoInspectorServiceFactory>();

            var adoApi = adoApiFactory.Create(args.AdoServerUrl, args.AdoPat);
            var adoInspectorService = adoInspectorServiceFactory.Create(adoApi);

            return new GenerateScriptCommandHandler(log, adoApi, versionProvider, adoInspectorService);
        }
    }
}
