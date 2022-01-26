using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using FluentAssertions;
using Xunit;
using Xunit.Abstractions;

namespace OctoshiftCLI.IntegrationTests
{
    public class UnitTest1
    {
        private readonly ITestOutputHelper _output;

        public UnitTest1(ITestOutputHelper output) => _output = output;

        [Fact]
        public async Task AdoToGithubE2ETest()
        {
            var logger = new OctoLogger(x => { }, x => _output.WriteLine(x), x => { }, x => { });
            var adoOrg = "gei-e2e-testing";
            var githubOrg = "e2e-testing";
            var testTeamProjects = new List<string>() { "gei-e2e-1", "gei-e2e-2" };

            var adoToken = Environment.GetEnvironmentVariable("ADO_PAT");
            using var adoHttpClient = new HttpClient();
            var adoClient = new AdoClient(logger, adoHttpClient, adoToken);
            var adoApi = new AdoApi(adoClient);

            var githubToken = Environment.GetEnvironmentVariable("GH_PAT");
            using var githubHttpClient = new HttpClient();
            var githubClient = new GithubClient(logger, githubHttpClient, githubToken);
            var githubApi = new GithubApi(githubClient);

            await ResetTestEnvironment(adoOrg, githubOrg, adoApi, githubApi);
            await CreateTestingData(adoOrg, testTeamProjects, adoApi);
            RunCliMigration(adoOrg, githubOrg, adoToken, githubToken);
            await AssertFinalState(adoOrg, githubOrg, adoApi, githubApi, testTeamProjects);
        }

        private async Task ResetTestEnvironment(string adoOrg, string githubOrg, AdoApi adoApi, GithubApi githubApi)
        {
            var teamProjects = await adoApi.GetTeamProjects(adoOrg);

            _output.WriteLine($"Found {teamProjects.Count()} Team Projects");

            foreach (var teamProject in teamProjects.Where(x => x != "service-connection-project-do-not-delete"))
            {
                _output.WriteLine($"Deleting Team Project: {adoOrg}\\{teamProject}...");
                var teamProjectId = await adoApi.GetTeamProjectId(adoOrg, teamProject);
                var operationId = await adoApi.DeleteTeamProject(adoOrg, teamProjectId);

                while (await adoApi.GetOperationStatus(adoOrg, operationId) is "notSet" or "queued" or "inProgress")
                {
                    await Task.Delay(1000);
                }
            }

            var githubRepos = await githubApi.GetRepos(githubOrg);

            foreach (var repo in githubRepos)
            {
                _output.WriteLine($"Deleting GitHub repo: {githubOrg}\\{repo}...");
                await githubApi.DeleteRepo(githubOrg, repo);
            }

            var githubTeams = await githubApi.GetTeams(githubOrg);

            foreach (var team in githubTeams)
            {
                _output.WriteLine($"Deleting GitHub team: {team}");
                await githubApi.DeleteTeam(githubOrg, team);
            }
        }
        private async Task CreateTestingData(string adoOrg, IEnumerable<string> testTeamProjects, AdoApi adoApi)
        {
            foreach (var teamProject in testTeamProjects)
            {
                _output.WriteLine($"Creating Team Project: {adoOrg}\\{teamProject}...");
                await adoApi.CreateTeamProject(adoOrg, teamProject);

                while (await adoApi.GetTeamProjectStatus(adoOrg, teamProject) is "createPending" or "new")
                {
                    await Task.Delay(1000);
                }

                var teamProjectStatus = await adoApi.GetTeamProjectStatus(adoOrg, teamProject);
                if (teamProjectStatus != "wellFormed")
                {
                    throw new InvalidDataException($"Project in unexpected state [{teamProjectStatus}]");
                }

                _output.WriteLine($"Initialiing Repo: {adoOrg}\\{teamProject}\\{teamProject}...");
                var defaultRepoId = await adoApi.GetRepoId(adoOrg, teamProject, teamProject);
                var commitId = await adoApi.InitializeRepo(adoOrg, defaultRepoId);

                _output.WriteLine($"Creating Pipeline: {adoOrg}\\{teamProject}\\{teamProject}...");
                await adoApi.PushDummyPipelineYaml(adoOrg, defaultRepoId, commitId);
                await adoApi.CreatePipeline(adoOrg, teamProject, teamProject, "/azure-pipelines.yml", defaultRepoId, teamProject);
            }
        }
        private void RunCliMigration(string adoOrg, string githubOrg, string adoToken, string githubToken)
        {
            var startInfo = new ProcessStartInfo();
            var scriptPath = string.Empty;

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                startInfo.FileName = @"../../../../../dist/linux-x64/ado2gh";
                startInfo.WorkingDirectory = @"../../../../../dist/linux-x64";

                scriptPath = Path.Join(@"../../../../../dist/linux-x64", "migrate.ps1");
            }

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                startInfo.FileName = @"../../../../../dist/win-x64/ado2gh.exe";
                startInfo.WorkingDirectory = @"../../../../../dist/win-x64";

                scriptPath = Path.Join(@"../../../../../dist/win-x64", "migrate.ps1");
            }

            if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                startInfo.FileName = @"../../../../../dist/osx-x64/ado2gh";
                startInfo.WorkingDirectory = @"../../../../../dist/osx-x64";

                scriptPath = Path.Join(@"../../../../../dist/osx-x64", "migrate.ps1");
            }

            startInfo.Arguments = $"generate-script --github-org {githubOrg} --ado-org {adoOrg}";

            if (startInfo.EnvironmentVariables.ContainsKey("ADO_PAT"))
            {
                startInfo.EnvironmentVariables["ADO_PAT"] = adoToken;
            }
            else
            {
                startInfo.EnvironmentVariables.Add("ADO_PAT", adoToken);
            }

            if (startInfo.EnvironmentVariables.ContainsKey("GH_PAT"))
            {
                startInfo.EnvironmentVariables["GH_PAT"] = githubToken;
            }
            else
            {
                startInfo.EnvironmentVariables.Add("GH_PAT", githubToken);
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
        private async Task AssertFinalState(string adoOrg, string githubOrg, AdoApi adoApi, GithubApi githubApi, IEnumerable<string> testTeamProjects)
        {
            _output.WriteLine("Checking that the repos in GitHub exist...");
            var repos = await githubApi.GetRepos(githubOrg);

            var expectedRepos = testTeamProjects.Select(x => $"{x}-{x}");

            repos.Should().Contain(expectedRepos);
            repos.Count().Should().Be(expectedRepos.Count());

            _output.WriteLine("Checking that the repos in GitHub are initialized...");

            foreach (var commits in repos.Select(x => githubApi.GetRepoCommitShas(githubOrg, x).Result))
            {
                commits.Count().Should().BeGreaterThan(0);
            }

            _output.WriteLine("Checking that the Autolinks have been configured...");

            foreach (var repo in repos)
            {
                var autolinks = await githubApi.GetAutolinks(githubOrg, repo);
                autolinks.Where(x => x.key == "AB#" && x.url == $"https://dev.azure.com/{adoOrg}/{GithubRepoToTeamProject(repo)}/_workitems/edit/<num>/")
                         .Count()
                         .Should().Be(1);
            }

            _output.WriteLine("Checking that the ADO repos have been disabled...");


            foreach (var teamProject in testTeamProjects)
            {
                var reposDisabled = await adoApi.GetReposDisabledState(adoOrg, teamProject);
                reposDisabled.Should().Contain(x => x.repo == teamProject && x.disabled);
            }

            _output.WriteLine("Checking that the ADO repos were locked...");

            foreach (var teamProject in testTeamProjects)
            {
                var teamProjectId = await adoApi.GetTeamProjectId(adoOrg, teamProject);
                var repoId = await adoApi.GetRepoId(adoOrg, teamProject, teamProject);
                var identityDescriptor = await adoApi.GetIdentityDescriptor(adoOrg, teamProjectId, "Project Valid Users");
                var (allow, deny) = await adoApi.GetRepoPermissions(adoOrg, teamProjectId, repoId, identityDescriptor);

                deny.Should().Be(56828);
            }

            _output.WriteLine("Checking that the GitHub teams were created...");
            var githubTeams = await githubApi.GetTeams(githubOrg);

            foreach (var teamProject in testTeamProjects)
            {
                githubTeams.Should().Contain($"{teamProject}-maintainers");
                githubTeams.Should().Contain($"{teamProject}-admins");
            }

            _output.WriteLine("Checking that the GitHub teams are linked to IdP groups...");

            foreach (var team in githubTeams)
            {
                var idpGroup = await githubApi.GetTeamIdPGroup(githubOrg, team);
                idpGroup.ToLower().Should().Be(team);
            }

            _output.WriteLine("Checking that the GitHub teams have repo permissions...");

            foreach (var teamProject in testTeamProjects)
            {
                var adminRole = await githubApi.GetTeamRepoRole(githubOrg, $"{teamProject}-admins", $"{teamProject}-{teamProject}");
                adminRole.Should().Be("admin");

                var maintainRole = await githubApi.GetTeamRepoRole(githubOrg, $"{teamProject}-maintainers", $"{teamProject}-{teamProject}");
                maintainRole.Should().Be("maintain");
            }

            _output.WriteLine("Checking that the service connection was shared...");

            foreach (var teamProject in testTeamProjects)
            {
                var serviceConnection = await adoApi.GetServiceConnections(adoOrg, teamProject);
            }


            // is boards integration configured
            // are pipelines rewired (run a pipeline?)
            // service connection shared


            // Are the repos in GH
            // Do they have the latest commit SHA
            // Is autolink configured (create a commit and link?)
            // Is the repo disabled on ADO
            // Are the deny permissions set
            // are the GH teams created
            // do the GH teams have Idp linked
            // do the GH teams have permissions on the repo
        }

        private string GithubRepoToTeamProject(string repo) => repo[(((repo.Length - 1) / 2) + 1)..];
    }
}