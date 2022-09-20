using System;
using System.CommandLine;
using System.CommandLine.NamingConventionBinder;
using System.IO;
using OctoshiftCLI.Contracts;

namespace OctoshiftCLI.AdoToGithub.Commands
{
    public class GenerateScriptCommand : Command
    {
        public GenerateScriptCommand(OctoLogger log, AdoApiFactory adoApiFactory, IVersionProvider versionProvider, AdoInspectorServiceFactory adoInspectorServiceFactory) : base(
            name: "generate-script",
            description: "Generates a migration script. This provides you the ability to review the steps that this tool will take, and optionally modify the script if desired before running it." +
                         Environment.NewLine +
                         "Note: Expects ADO_PAT env variable or --ado-pat option to be set.")
        {
            var githubOrgOption = new Option<string>("--github-org")
            {
                IsRequired = true
            };
            var adoOrgOption = new Option<string>("--ado-org")
            {
                IsRequired = false
            };
            var adoTeamProject = new Option<string>("--ado-team-project")
            {
                IsRequired = false
            };
            var outputOption = new Option<FileInfo>("--output", () => new FileInfo("./migrate.ps1"))
            {
                IsRequired = false
            };
            var sequential = new Option<bool>("--sequential")
            {
                IsRequired = false,
                Description = "Waits for each migration to finish before moving on to the next one."
            };
            var adoPat = new Option<string>("--ado-pat")
            {
                IsRequired = false
            };
            var verbose = new Option<bool>("--verbose")
            {
                IsRequired = false
            };
            var downloadMigrationLogs = new Option<bool>("--download-migration-logs")
            {
                IsRequired = false,
                Description = "Downloads the migration log for each repository migration."
            };

            var createTeams = new Option<bool>("--create-teams")
            {
                IsRequired = false,
                Description = "Includes create-team scripts that creates admins and maintainers teams and adds them to repos."
            };
            var linkIdpGroups = new Option<bool>("--link-idp-groups")
            {
                IsRequired = false,
                Description = "Adds --idp-group to the end of create teams scripts that links the created team to an idP group."
            };
            var lockAdoRepos = new Option<bool>("--lock-ado-repos")
            {
                IsRequired = false,
                Description = "Includes lock-ado-repo scripts that lock repos before migrating them."
            };
            var disableAdoRepos = new Option<bool>("--disable-ado-repos")
            {
                IsRequired = false,
                Description = "Includes disable-ado-repo scripts that disable repos after migrating them."
            };
            var integrateBoards = new Option<bool>("--integrate-boards")
            {
                IsRequired = false,
                Description = "Includes configure-autolink and integrate-boards scripts that configure Azure Boards integrations."
            };
            var rewirePipelines = new Option<bool>("--rewire-pipelines")
            {
                IsRequired = false,
                Description = "Includes share-service-connection and rewire-pipeline scripts that rewire Azure Pipelines to point to GitHub repos."
            };
            var all = new Option<bool>("--all")
            {
                IsRequired = false,
                Description = "Includes all script generation options."
            };
            var repoList = new Option<FileInfo>("--repo-list")
            {
                IsRequired = false,
                Description = "Path to a csv file that contains a list of repos to generate a script for. The CSV file should be generated using the inventory-report command."
            };

            AddOption(githubOrgOption);
            AddOption(adoOrgOption);
            AddOption(adoTeamProject);
            AddOption(outputOption);
            AddOption(sequential);
            AddOption(adoPat);
            AddOption(verbose);
            AddOption(downloadMigrationLogs);
            AddOption(createTeams);
            AddOption(linkIdpGroups);
            AddOption(lockAdoRepos);
            AddOption(disableAdoRepos);
            AddOption(integrateBoards);
            AddOption(rewirePipelines);
            AddOption(all);
            AddOption(repoList);

            var handler = new GenerateScriptCommandHandler(log, adoApiFactory, versionProvider, adoInspectorServiceFactory);
            Handler = CommandHandler.Create<GenerateScriptCommandArgs>(handler.Invoke);
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
