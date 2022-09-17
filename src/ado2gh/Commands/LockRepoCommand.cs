﻿using System;
using System.CommandLine;
using System.CommandLine.NamingConventionBinder;
using System.Threading.Tasks;

namespace OctoshiftCLI.AdoToGithub.Commands
{
    public class LockRepoCommand : Command
    {
        private readonly OctoLogger _log;
        private readonly AdoApiFactory _adoApiFactory;

        public LockRepoCommand(OctoLogger log, AdoApiFactory adoApiFactory) : base(
            name: "lock-ado-repo",
            description: "Makes the ADO repo read-only for all users. It does this by adding Deny permissions for the Project Valid Users group on the repo." +
                         Environment.NewLine +
                         "Note: Expects ADO_PAT env variable or --ado-pat option to be set.")
        {
            _log = log;
            _adoApiFactory = adoApiFactory;

            var adoOrg = new Option<string>("--ado-org")
            {
                IsRequired = true
            };
            var adoTeamProject = new Option<string>("--ado-team-project")
            {
                IsRequired = true
            };
            var adoRepo = new Option<string>("--ado-repo")
            {
                IsRequired = true
            };
            var adoPat = new Option<string>("--ado-pat")
            {
                IsRequired = false
            };
            var verbose = new Option<bool>("--verbose")
            {
                IsRequired = false
            };

            AddOption(adoOrg);
            AddOption(adoTeamProject);
            AddOption(adoRepo);
            AddOption(adoPat);
            AddOption(verbose);

            Handler = CommandHandler.Create<string, string, string, string, bool>(Invoke);
        }

        public async Task Invoke(string adoOrg, string adoTeamProject, string adoRepo, string adoPat = null, bool verbose = false)
        {
            _log.Verbose = verbose;

            _log.LogInformation("Locking repo...");
            _log.LogInformation($"ADO ORG: {adoOrg}");
            _log.LogInformation($"ADO TEAM PROJECT: {adoTeamProject}");
            _log.LogInformation($"ADO REPO: {adoRepo}");
            if (adoPat is not null)
            {
                _log.LogInformation("ADO PAT: ***");
            }

            var ado = _adoApiFactory.Create(adoPat);

            var teamProjectId = await ado.GetTeamProjectId(adoOrg, adoTeamProject);
            var repoId = await ado.GetRepoId(adoOrg, adoTeamProject, adoRepo);

            var identityDescriptor = await ado.GetIdentityDescriptor(adoOrg, teamProjectId, "Project Valid Users");
            await ado.LockRepo(adoOrg, teamProjectId, repoId, identityDescriptor);

            _log.LogSuccess("Repo successfully locked");
        }
    }
}
