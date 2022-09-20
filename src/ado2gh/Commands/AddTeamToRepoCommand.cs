﻿using System;
using System.CommandLine;
using System.CommandLine.NamingConventionBinder;
using OctoshiftCLI.AdoToGithub.Handlers;

namespace OctoshiftCLI.AdoToGithub.Commands
{
    public sealed class AddTeamToRepoCommand : Command
    {
        public AddTeamToRepoCommand(OctoLogger log, GithubApiFactory githubApiFactory) : base(
            name: "add-team-to-repo",
            description: "Adds a team to a repo with a specific role/permission" +
                         Environment.NewLine +
                         "Note: Expects GH_PAT env variable or --github-pat option to be set.")
        {
            var githubOrg = new Option<string>("--github-org")
            {
                IsRequired = true
            };
            var githubRepo = new Option<string>("--github-repo")
            {
                IsRequired = true
            };
            var team = new Option<string>("--team")
            {
                IsRequired = true
            };
            var role = new Option<string>("--role")
            {
                IsRequired = true,
                Description = "The only valid values are: pull, push, admin, maintain, triage. For more details see https://docs.github.com/en/rest/reference/teams#add-or-update-team-repository-permissions, custom repository roles are not currently supported."
            };
            var githubPat = new Option<string>("--github-pat")
            {
                IsRequired = false
            };
            var verbose = new Option<bool>("--verbose")
            {
                IsRequired = false
            };

            AddOption(githubOrg);
            AddOption(githubRepo);
            AddOption(team);
            AddOption(role.FromAmong("pull", "push", "admin", "maintain", "triage"));
            AddOption(githubPat);
            AddOption(verbose);

            var handler = new AddTeamToRepoCommandHandler(log, githubApiFactory);
            Handler = CommandHandler.Create<AddTeamToRepoCommandArgs>(handler.Invoke);
        }
    }

    public class AddTeamToRepoCommandArgs
    {
        public string GithubOrg { get; set; }
        public string GithubRepo { get; set; }
        public string Team { get; set; }
        public string Role { get; set; }
        public string GithubPat { get; set; }
        public bool Verbose { get; set; }
    }
}
