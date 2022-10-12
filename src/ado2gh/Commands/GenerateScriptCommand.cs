using System;
using System.CommandLine;
using System.IO;
using Microsoft.Extensions.DependencyInjection;
using OctoshiftCLI.AdoToGithub.Handlers;
using OctoshiftCLI.Commands;
using OctoshiftCLI.Contracts;

namespace OctoshiftCLI.AdoToGithub.Commands
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
            AddOption(AdoOrg);
            AddOption(AdoTeamProject);
            AddOption(Output);
            AddOption(Sequential);
            AddOption(AdoPat);
            AddOption(Verbose);
            AddOption(DownloadMigrationLogs);
            AddOption(CreateTeams);
            AddOption(LinkIdpGroups);
            AddOption(LockAdoRepos);
            AddOption(DisableAdoRepos);
            AddOption(IntegrateBoards);
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
        public Option<bool> IntegrateBoards { get; } = new("--integrate-boards")
        {
            Description = "Includes configure-autolink and integrate-boards scripts that configure Azure Boards integrations."
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

            var adoApi = adoApiFactory.Create(args.AdoPat);
            var adoInspectorService = adoInspectorServiceFactory.Create(adoApi);

            return new GenerateScriptCommandHandler(log, adoApi, versionProvider, adoInspectorService);
        }
    }

    public class GenerateScriptCommandArgs
    {
        public string GithubOrg { get; set; }
        public string AdoOrg { get; set; }
        public string AdoTeamProject { get; set; }
        public FileInfo Output { get; set; }
        public bool Sequential { get; set; }
        public string AdoPat { get; set; }
        public bool Verbose { get; set; }
        public bool DownloadMigrationLogs { get; set; }
        public bool CreateTeams { get; set; }
        public bool LinkIdpGroups { get; set; }
        public bool LockAdoRepos { get; set; }
        public bool DisableAdoRepos { get; set; }
        public bool IntegrateBoards { get; set; }
        public bool RewirePipelines { get; set; }
        public bool All { get; set; }
        public FileInfo RepoList { get; set; }
    }
}
