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
        private readonly ITestOutputHelper output;

        public UnitTest1(ITestOutputHelper output)
        {
            this.output = output;
        }

        [Fact]
        public async Task Test1()
        {
            var logger = new OctoLogger(x => { }, x => output.WriteLine(x), x => { }, x => { });

            var adoToken = Environment.GetEnvironmentVariable("ADO_PAT");
            using var adoHttpClient = new HttpClient();
            var adoClient = new AdoClient(logger, adoHttpClient, adoToken);
            var adoApi = new AdoApi(adoClient);

            var githubToken = Environment.GetEnvironmentVariable("GH_PAT");
            using var githubHttpClient = new HttpClient();
            var githubClient = new GithubClient(logger, githubHttpClient, githubToken);
            var githubApi = new GithubApi(githubClient);

            var adoOrg = "gei-e2e-testing";
            var githubOrg = "e2e-testing";

            var teamProjects = await adoApi.GetTeamProjects(adoOrg);

            output.WriteLine($"Found {teamProjects.Count()} Team Projects");

            foreach (var teamProject in teamProjects)
            {
                if (teamProject != "service-connection-project-do-not-delete")
                {
                    output.WriteLine($"Deleting Team Project: {adoOrg}\\{teamProject}...");
                    var teamProjectId = await adoApi.GetTeamProjectId(adoOrg, teamProject);
                    var operationId = await adoApi.DeleteTeamProject(adoOrg, teamProjectId);

                    while (await adoApi.GetOperationStatus(adoOrg, operationId) is "notSet" or "queued" or "inProgress")
                    {
                        await Task.Delay(1000);
                    }
                }
            }

            var githubRepos = await githubApi.GetRepos(githubOrg);

            foreach (var repo in githubRepos)
            {
                output.WriteLine($"Deleting GitHub repo: {githubOrg}\\{repo}...");
                await githubApi.DeleteRepo(githubOrg, repo);
            }

            var githubTeams = await githubApi.GetTeams(githubOrg);

            foreach (var team in githubTeams)
            {
                output.WriteLine($"Deleting GitHub team: {team}");
                await githubApi.DeleteTeam(githubOrg, team);
            }

            var testTeamProjects = new List<string>() { "e2e-1", "e2e-2" };

            foreach (var teamProject in testTeamProjects)
            {
                output.WriteLine($"Creating Team Project: {adoOrg}\\{teamProject}...");
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

                var defaultRepoId = await adoApi.GetRepoId(adoOrg, teamProject, teamProject);
                await adoApi.InitializeRepo(adoOrg, defaultRepoId);
            }

            //var cliPath = Path.Join(Directory.GetCurrentDirectory(), "ado2gh.exe");

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

            startInfo.Arguments = "generate-script --github-org e2e-testing";

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
            cliPath = Path.GetFullPath(cliPath);
            startInfo.FileName = cliPath;
            output.WriteLine($"Starting process {cliPath}");
            var p = Process.Start(startInfo);
            p.WaitForExit();

            p.ExitCode.Should().Be(0, "generate-script should return an exit code of 0");

            startInfo.FileName = "pwsh";
            scriptPath = Path.Join(Directory.GetCurrentDirectory(), scriptPath);
            scriptPath = Path.GetFullPath(scriptPath);
            startInfo.Arguments = $"-File {scriptPath}";

            output.WriteLine($"scriptPath: {scriptPath}");

            p = Process.Start(startInfo);
            p.WaitForExit();

            p.ExitCode.Should().Be(0, "migrate.ps1 should return an exit code of 0");
        }
    }
}