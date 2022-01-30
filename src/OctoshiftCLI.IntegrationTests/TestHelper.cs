using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using FluentAssertions;
using Xunit.Abstractions;

namespace OctoshiftCLI.IntegrationTests
{
    public class TestHelper
    {
        private readonly ITestOutputHelper _output;
        private readonly AdoApi _adoApi;
        private readonly GithubApi _githubSourceApi;
        private readonly GithubApi _githubTargetApi;

        public TestHelper(ITestOutputHelper output, AdoApi adoApi, GithubApi githubApi)
        {
            _output = output;
            _adoApi = adoApi;
            _githubTargetApi = githubApi;
        }

        public TestHelper(ITestOutputHelper output, GithubApi githubSourceApi, GithubApi githubTargetApi)
        {
            _output = output;
            _githubSourceApi = githubSourceApi;
            _githubTargetApi = githubTargetApi;
        }

        public async Task ResetAdoTestEnvironment(string adoOrg)
        {
            var teamProjects = await _adoApi.GetTeamProjects(adoOrg);

            _output.WriteLine($"Found {teamProjects.Count()} Team Projects");

            foreach (var teamProject in teamProjects.Where(x => x != "service-connection-project-do-not-delete"))
            {
                _output.WriteLine($"Deleting Team Project: {adoOrg}\\{teamProject}...");
                var teamProjectId = await _adoApi.GetTeamProjectId(adoOrg, teamProject);
                var operationId = await _adoApi.DeleteTeamProject(adoOrg, teamProjectId);

                while (await _adoApi.GetOperationStatus(adoOrg, operationId) is "notSet" or "queued" or "inProgress")
                {
                    await Task.Delay(1000);
                }
            }
        }

        public async Task CreateGithubRepo(string githubOrg, string repo)
        {
            _output.WriteLine($"Creating Github repo: {githubOrg}\\{repo}...");
            await _githubSourceApi.CreateRepo(githubOrg, repo, true, true);
        }

        public async Task ResetGithubTestEnvironment(string githubOrg)
        {
            var githubRepos = await _githubTargetApi.GetRepos(githubOrg);

            foreach (var repo in githubRepos)
            {
                _output.WriteLine($"Deleting GitHub repo: {githubOrg}\\{repo}...");
                await _githubTargetApi.DeleteRepo(githubOrg, repo);
            }

            var githubTeams = await _githubTargetApi.GetTeams(githubOrg);

            foreach (var team in githubTeams)
            {
                _output.WriteLine($"Deleting GitHub team: {team}");
                await _githubTargetApi.DeleteTeam(githubOrg, team);
            }
        }

        public async Task CreateTeamProject(string adoOrg, string teamProject)
        {
            _output.WriteLine($"Creating Team Project: {adoOrg}\\{teamProject}...");
            await _adoApi.CreateTeamProject(adoOrg, teamProject);

            while (await _adoApi.GetTeamProjectStatus(adoOrg, teamProject) is "createPending" or "new")
            {
                await Task.Delay(1000);
            }

            var teamProjectStatus = await _adoApi.GetTeamProjectStatus(adoOrg, teamProject);

            if (teamProjectStatus != "wellFormed")
            {
                throw new InvalidDataException($"Project in unexpected state [{teamProjectStatus}]");
            }
        }

        public async Task<string> InitializeAdoRepo(string adoOrg, string teamProject, string repo)
        {
            _output.WriteLine($"Initializing Repo: {adoOrg}\\{teamProject}\\{repo}...");
            var repoId = await _adoApi.GetRepoId(adoOrg, teamProject, repo);
            return await _adoApi.InitializeRepo(adoOrg, repoId);
        }

        public async Task CreatePipeline(string adoOrg, string teamProject, string repo, string pipelineName, string parentCommitId)
        {
            _output.WriteLine($"Creating Pipeline: {adoOrg}\\{teamProject}\\{pipelineName}...");
            var repoId = await _adoApi.GetRepoId(adoOrg, teamProject, repo);
            await _adoApi.PushDummyPipelineYaml(adoOrg, repoId, parentCommitId);
            await _adoApi.CreatePipeline(adoOrg, teamProject, pipelineName, "/azure-pipelines.yml", repoId, teamProject);
        }

        public void RunCliMigration(string generateScriptCommand, string cliName, IDictionary<string, string> tokens)
        {
            var startInfo = new ProcessStartInfo();
            var scriptPath = string.Empty;

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                startInfo.FileName = $"../../../../../dist/linux-x64/{cliName}";
                startInfo.WorkingDirectory = "../../../../../dist/linux-x64";

                scriptPath = Path.Join("../../../../../dist/linux-x64", "migrate.ps1");
            }

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                startInfo.FileName = $"../../../../../dist/win-x64/{cliName}.exe";
                startInfo.WorkingDirectory = @"../../../../../dist/win-x64";

                scriptPath = Path.Join(@"../../../../../dist/win-x64", "migrate.ps1");
            }

            if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                startInfo.FileName = $"../../../../../dist/osx-x64/{cliName}";
                startInfo.WorkingDirectory = $"../../../../../dist/osx-x64";

                scriptPath = Path.Join($"../../../../../dist/osx-x64", "migrate.ps1");
            }

            startInfo.Arguments = generateScriptCommand;

            if (tokens != null)
            {
                foreach (var token in tokens)
                {
                    if (startInfo.EnvironmentVariables.ContainsKey(token.Key))
                    {
                        startInfo.EnvironmentVariables[token.Key] = token.Value;
                    }
                    else
                    {
                        startInfo.EnvironmentVariables.Add("ADO_PAT", token.Value);
                    }
                }
            }

            var cliPath = Path.Join(Directory.GetCurrentDirectory(), startInfo.FileName);
            startInfo.FileName = cliPath;
            _output.WriteLine($"Running command: {startInfo.FileName} {startInfo.Arguments}");
            var p = Process.Start(startInfo);
            p.WaitForExit();

            p.ExitCode.Should().Be(0, "generate-script should return an exit code of 0");

            startInfo.FileName = "pwsh";
            scriptPath = Path.Join(Directory.GetCurrentDirectory(), scriptPath);
            startInfo.Arguments = $"-File {scriptPath}";

            _output.WriteLine($"Running command: {startInfo.FileName} {startInfo.Arguments}");
            p = Process.Start(startInfo);
            p.WaitForExit();

            p.ExitCode.Should().Be(0, "migrate.ps1 should return an exit code of 0");
        }

        public void RunAdoToGithubCliMigration(string generateScriptCommand)
        {
            var adoToken = Environment.GetEnvironmentVariable("ADO_PAT");
            var githubToken = Environment.GetEnvironmentVariable("GH_PAT");
            var tokens = new Dictionary<string, string>() { { "ADO_PAT", adoToken }, { "GH_PAT", githubToken } };

            RunCliMigration(generateScriptCommand, "ado2gh", tokens);
        }

        public void RunGeiCliMigration(string generateScriptCommand)
        {
            var githubToken = Environment.GetEnvironmentVariable("GH_PAT");
            var tokens = new Dictionary<string, string>() { { "GH_PAT", githubToken } };

            RunCliMigration(generateScriptCommand, "gh gei", tokens);
        }

        public async Task AssertGithubRepoExists(string githubOrg, string repo)
        {
            _output.WriteLine("Checking that the repos in GitHub exist...");
            var repos = await _githubTargetApi.GetRepos(githubOrg);
            repos.Should().Contain(repo);
        }

        public async Task AssertGithubRepoInitialized(string githubOrg, string repo)
        {
            _output.WriteLine("Checking that the repos in GitHub are initialized...");

            var commits = await _githubTargetApi.GetRepoCommitShas(githubOrg, repo);
            commits.Count().Should().BeGreaterThan(0);
        }

        public async Task AssertAutolinkConfigured(string githubOrg, string repo, string urlTemplate)
        {
            _output.WriteLine("Checking that the Autolinks have been configured...");

            var autolinks = await _githubTargetApi.GetAutolinks(githubOrg, repo);
            autolinks.Where(x => x.key == "AB#" && x.url == urlTemplate)
                     .Count()
                     .Should().Be(1);
        }

        public async Task AssertAdoRepoDisabled(string adoOrg, string teamProject, string repo)
        {
            _output.WriteLine("Checking that the ADO repos have been disabled...");

            var reposDisabled = await _adoApi.GetReposDisabledState(adoOrg, teamProject);
            reposDisabled.Should().Contain(x => x.repo == repo && x.disabled);
        }
        public async Task AssertAdoRepoLocked(string adoOrg, string teamProject, string repo)
        {
            _output.WriteLine("Checking that the ADO repos were locked...");

            var teamProjectId = await _adoApi.GetTeamProjectId(adoOrg, teamProject);
            var repoId = await _adoApi.GetRepoId(adoOrg, teamProject, repo);
            var identityDescriptor = await _adoApi.GetIdentityDescriptor(adoOrg, teamProjectId, "Project Valid Users");
            var (_, deny) = await _adoApi.GetRepoPermissions(adoOrg, teamProjectId, repoId, identityDescriptor);

            deny.Should().Be(56828);
        }

        public async Task AssertGithubTeamCreated(string githubOrg, string teamSlug)
        {
            _output.WriteLine("Checking that the GitHub teams were created...");
            var githubTeams = await _githubTargetApi.GetTeams(githubOrg);

            githubTeams.Should().Contain(teamSlug);
        }

        public async Task AssertGithubTeamIdpLinked(string githubOrg, string teamSlug, string idpGroup)
        {
            _output.WriteLine("Checking that the GitHub teams are linked to IdP groups...");

            var idp = await _githubTargetApi.GetTeamIdPGroup(githubOrg, teamSlug);
            idp.ToLower().Should().Be(idpGroup);
        }

        public async Task AssertGithubTeamHasRepoRole(string githubOrg, string teamSlug, string repo, string role)
        {
            _output.WriteLine("Checking that the GitHub teams have repo permissions...");

            var actualRole = await _githubTargetApi.GetTeamRepoRole(githubOrg, teamSlug, repo);
            actualRole.Should().Be(role);
        }

        public async Task AssertServiceConnectionWasShared(string adoOrg, string teamProject)
        {
            _output.WriteLine("Checking that the service connection was shared...");

            var serviceConnections = await _adoApi.GetServiceConnections(adoOrg, teamProject);
            serviceConnections.Should().Contain(x => x.Type == "GitHub");
        }

        public async Task AssertPipelineRewired(string adoOrg, string teamProject, string pipeline, string githubOrg, string githubRepo)
        {
            _output.WriteLine("Checking that the pipelines are rewired...");

            var pipelineId = await _adoApi.GetPipelineId(adoOrg, teamProject, pipeline);
            var pipelineRepo = await _adoApi.GetPipelineRepo(adoOrg, teamProject, pipelineId);

            var serviceConnectionId = (await _adoApi.GetServiceConnections(adoOrg, teamProject)).First(x => x.Type == "GitHub");
            pipelineRepo.Type.Should().Be("GitHub");
            pipelineRepo.Id.Should().Be($"{githubOrg}/{githubRepo}");
            pipelineRepo.ConnectedServiceId.Should().Be(serviceConnectionId.Id);
        }

        public async Task AssertBoardsIntegrationConfigured(string adoOrg, string teamProject)
        {
            _output.WriteLine("Checking that the boards integration is configured...");

            var userId = await _adoApi.GetUserId();
            var adoOrgId = await _adoApi.GetOrganizationId(userId, adoOrg);
            var boardsConnection = await _adoApi.GetBoardsGithubConnection(adoOrg, adoOrgId, teamProject);

            boardsConnection.Should().NotBeNull();
            boardsConnection.repoIds.Count().Should().Be(1);
        }
    }
}