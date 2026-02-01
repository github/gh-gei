using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Azure.Storage.Blobs;
using CliWrap;
using FluentAssertions;
using Newtonsoft.Json.Linq;
using OctoshiftCLI.Services;
using Xunit.Abstractions;

namespace OctoshiftCLI.IntegrationTests
{
    public static class OperationStatus
    {
        public const string NotSet = "notSet";
        public const string Queued = "queued";
        public const string InProgress = "inProgress";
    }

    public static class TeamProjectStatus
    {
        public const string NotSet = "notSet";
        public const string CreatePending = "createPending";
        public const string New = "new";
        public const string WellFormed = "wellFormed";
    }

    public class TestHelper
    {
        private readonly ITestOutputHelper _output;
        private readonly AdoApi _adoApi;
        private readonly GithubApi _githubApi;
        private readonly AdoClient _adoClient;
        private readonly GithubClient _githubClient;
        private readonly BlobServiceClient _blobServiceClient;
        private readonly BbsApi _bbsApi;
        private readonly BbsClient _bbsClient;
        private readonly string _bbsUrl;

        public TestHelper(ITestOutputHelper output, AdoApi adoApi, GithubApi githubApi, AdoClient adoClient, GithubClient githubClient)
        {
            _output = output;
            _adoApi = adoApi;
            _githubApi = githubApi;
            _adoClient = adoClient;
            _githubClient = githubClient;
        }

        public TestHelper(ITestOutputHelper output, GithubApi githubTargetApi, GithubClient githubClient, BlobServiceClient blobServiceClient = null)
        {
            _output = output;
            _githubApi = githubTargetApi;
            _githubClient = githubClient;
            _blobServiceClient = blobServiceClient;
        }

        public TestHelper(ITestOutputHelper output, BbsApi bbsApi, BbsClient bbsClient, string bbsUrl)
        {
            _output = output;
            _bbsApi = bbsApi;
            _bbsClient = bbsClient;
            _bbsUrl = bbsUrl;
        }

        public string GithubApiBaseUrl { get; init; } = "https://api.github.com";

        public async Task ResetAdoTestEnvironment(string adoOrg, string adoServerUrl = "https://dev.azure.com")
        {
            var teamProjects = await _adoApi.GetTeamProjects(adoOrg);

            _output.WriteLine($"Found {teamProjects.Count()} Team Projects");

            foreach (var teamProject in teamProjects.Where(x => x != "service-connection-project-do-not-delete"))
            {
                _output.WriteLine($"Deleting Team Project: {adoOrg}\\{teamProject}...");
                var teamProjectId = await _adoApi.GetTeamProjectId(adoOrg, teamProject);
                var operationId = await DeleteTeamProject(adoOrg, teamProjectId, adoServerUrl);

                while (await GetOperationStatus(adoOrg, operationId, adoServerUrl) is OperationStatus.NotSet or OperationStatus.Queued or OperationStatus.InProgress)
                {
                    await Task.Delay(1000);
                }
            }
        }

        public async Task CreateGithubRepo(string githubOrg, string repo)
        {
            _output.WriteLine($"Creating GitHub repo: {githubOrg}\\{repo}...");
            await CreateRepo(githubOrg, repo, true, true);
        }

        public async Task ResetBbsTestEnvironment(string bbsProjectKey)
        {
            var retryPolicy = new RetryPolicy(null);

            await retryPolicy.Retry(async () =>
            {
                if (await DoesBbsProjectExist(bbsProjectKey))
                {
                    var bbsRepos = (await _bbsApi.GetRepos(bbsProjectKey)).Select(x => x.Slug);

                    foreach (var repo in bbsRepos)
                    {
                        _output.WriteLine($"Deleting BBS repo: {bbsProjectKey}\\{repo}...");
                        await DeleteBbsRepo(bbsProjectKey, repo);
                    }

                    await DeleteBbsProject(bbsProjectKey);
                }
            });
        }

        private async Task<bool> DoesBbsProjectExist(string bbsProjectKey)
        {
            var bbsProjects = await _bbsApi.GetProjects();

            return bbsProjects.Any(x => x.Key == bbsProjectKey);
        }

        private async Task DeleteBbsRepo(string bbsProjectKey, string slug)
        {
            var url = $"{_bbsUrl}/rest/api/1.0/projects/{bbsProjectKey}/repos/{slug}";

            await _bbsClient.DeleteAsync(url);
        }

        private async Task DeleteBbsProject(string bbsProjectKey)
        {
            var url = $"{_bbsUrl}/rest/api/1.0/projects/{bbsProjectKey}";

            await _bbsClient.DeleteAsync(url);
        }

        public async Task ResetGithubTestEnvironment(string githubOrg)
        {
            var githubRepos = await _githubApi.GetRepos(githubOrg);

            foreach (var repo in githubRepos)
            {
                _output.WriteLine($"Deleting migration log for repo: {githubOrg}\\{repo}");
                DeleteMigrationLog(githubOrg, repo.Name);

                _output.WriteLine($"Deleting GitHub repo: {githubOrg}\\{repo}...");
                await _githubApi.DeleteRepo(githubOrg, repo.Name);
            }

            var githubTeams = await GetTeamSlugs(githubOrg);

            foreach (var teamSlug in githubTeams)
            {
                _output.WriteLine($"Deleting GitHub team: {teamSlug}");
                await DeleteTeam(githubOrg, teamSlug);
            }
        }

        public async Task InitializeBbsRepo(string bbsProjectKey, string repoName)
        {
            var repoPath = Path.Join(Path.GetTempPath(), $"{repoName}-{Guid.NewGuid()}");

            Directory.CreateDirectory(repoPath);

            var bbsUri = new Uri(_bbsUrl);
            var bbsUsername = Environment.GetEnvironmentVariable("BBS_USERNAME");
            var bbsPassword = Environment.GetEnvironmentVariable("BBS_PASSWORD");
            var repoUrl = $"{bbsUri.Scheme}://{bbsUsername}:{bbsPassword}@{bbsUri.Authority}/scm/{bbsProjectKey}/{repoName}.git";

            await RunGitCommand($"clone {repoUrl} {repoPath}");
            await File.WriteAllTextAsync(Path.Join(repoPath, "README.md"), "# Test Repo");
            await RunGitCommand("add README.md", repoPath);
            await RunGitCommand("config user.name \"Octoshift\"", repoPath);
            await RunGitCommand("config user.email \"octoshift@github.com\"", repoPath);
            await RunGitCommand("commit -m \"Initial commit\"", repoPath);
            await RunGitCommand("push", repoPath);
        }

        private async Task RunGitCommand(string command, string workingDirectory = null) => await RunShellCommand(command, "git", workingDirectory);

        public async Task CreateBbsRepo(string bbsProjectKey, string repoName)
        {
            var url = $"{_bbsUrl}/rest/api/1.0/projects/{bbsProjectKey}/repos";
            var payload = new
            {
                name = repoName,
                scmId = "git",
                slug = repoName
            };

            await _bbsClient.PostAsync(url, payload);
        }

        public async Task CreateBbsProject(string bbsProjectKey)
        {
            var url = $"{_bbsUrl}/rest/api/1.0/projects";
            var payload = new
            {
                key = bbsProjectKey
            };

            await _bbsClient.PostAsync(url, payload);
        }

        public async Task CreateTeamProject(string adoOrg, string teamProject, string adoServerUrl = "https://dev.azure.com")
        {
            _output.WriteLine($"Creating Team Project: {adoOrg}\\{teamProject}...");
            var operationId = await QueueCreateTeamProject(adoOrg, teamProject, adoServerUrl);

            while (await GetOperationStatus(adoOrg, operationId, adoServerUrl) is OperationStatus.NotSet or OperationStatus.Queued or OperationStatus.InProgress)
            {
                await Task.Delay(1000);
            }

            while (await GetTeamProjectStatus(adoOrg, teamProject, adoServerUrl) is TeamProjectStatus.NotSet or TeamProjectStatus.CreatePending or TeamProjectStatus.New)
            {
                await Task.Delay(1000);
            }

            var teamProjectStatus = await GetTeamProjectStatus(adoOrg, teamProject, adoServerUrl);

            if (teamProjectStatus != TeamProjectStatus.WellFormed)
            {
                throw new InvalidDataException($"Project in unexpected state [{teamProjectStatus}]");
            }
        }

        public async Task<string> InitializeAdoRepo(string adoOrg, string teamProject, string repo, string adoServerUrl = "https://dev.azure.com")
        {
            _output.WriteLine($"Initializing Repo: {adoOrg}\\{teamProject}\\{repo}...");
            var repoId = await _adoApi.GetRepoId(adoOrg, teamProject, repo);
            return await InitializeRepo(adoOrg, repoId, adoServerUrl);
        }

        public async Task CreatePipeline(string adoOrg, string teamProject, string repo, string pipelineName, string parentCommitId, string adoServerUrl = "https://dev.azure.com")
        {
            _output.WriteLine($"Creating Pipeline: {adoOrg}\\{teamProject}\\{pipelineName}...");
            var repoId = await _adoApi.GetRepoId(adoOrg, teamProject, repo);
            await PushDummyPipelineYaml(adoOrg, repoId, parentCommitId, adoServerUrl);
            await CreatePipeline(adoOrg, teamProject, pipelineName, "/azure-pipelines.yml", repoId, teamProject, adoServerUrl);
        }

        private async Task<string> CreatePipeline(string org, string teamProject, string name, string ymlFile, string repoId, string repoName, string adoServerUrl)
        {
            var url = $"{adoServerUrl}/{org}/{teamProject}/_apis/pipelines?api-version=6.1-preview.1";

            var payload = new
            {
                folder = @"\\",
                name,
                configuration = new
                {
                    type = "yaml",
                    path = ymlFile,
                    repository = new
                    {
                        id = repoId,
                        name = repoName,
                        type = "azureReposGit"
                    }
                }
            };

            var response = await _adoClient.PostAsync(url, payload);

            return (string)JObject.Parse(response)["id"];
        }

        private async Task<string> InitializeRepo(string org, string repoId, string adoServerUrl)
        {
            var url = $"{adoServerUrl}/{org}/_apis/git/repositories/{repoId}/pushes?api-version=6.0";

            var payload = new
            {
                refUpdates = new[]
                {
                    new
                    {
                        name = "refs/heads/main",
                        oldObjectId = "0000000000000000000000000000000000000000"
                    }
                },
                commits = new[]
                {
                    new
                    {
                        comment = "Initial commit.",
                        changes = new []
                        {
                            new
                            {
                                changeType = "add",
                                item = new
                                {
                                    path = "/readme.md"
                                },
                                newContent = new
                                {
                                    content = "My first file!",
                                    contentType = "rawtext"
                                }
                            }
                        }
                    }
                }
            };

            var response = await _adoClient.PostAsync(url, payload);
            return (string)JObject.Parse(response)["refUpdates"].Children().First()["newObjectId"];
        }

        private async Task<string> PushDummyPipelineYaml(string org, string repoId, string parentCommit, string adoServerUrl)
        {
            var url = $"{adoServerUrl}/{org}/_apis/git/repositories/{repoId}/pushes?api-version=6.0";

            var payload = new
            {
                refUpdates = new[]
                {
                    new
                    {
                        name = "refs/heads/main",
                        oldObjectId = parentCommit
                    }
                },
                commits = new[]
                {
                    new
                    {
                        comment = "Initial commit.",
                        changes = new []
                        {
                            new
                            {
                                changeType = "add",
                                item = new
                                {
                                    path = "/azure-pipelines.yml"
                                },
                                newContent = new
                                {
                                    content = @"
trigger:
- main
pool:
  vmImage: ubuntu-latest
steps:
- script: echo Hello, world!",
                                    contentType = "rawtext"
                                }
                            }
                        }
                    }
                }
            };

            var response = await _adoClient.PostAsync(url, payload);
            return (string)JObject.Parse(response)["refUpdates"].Children().First()["newObjectId"];
        }

        private async Task<IEnumerable<(string repo, bool disabled)>> GetReposDisabledState(string org, string teamProject, string adoServerUrl)
        {
            var url = $"{adoServerUrl}/{org}/{teamProject}/_apis/git/repositories?api-version=4.1";
            var response = await _adoClient.GetWithPagingAsync(url);
            return response.Select(x => ((string)x["name"], (bool)x["isDisabled"]));
        }

        private async Task<(int allow, int deny)> GetRepoPermissions(string org, string teamProjectId, string repoId, string identityDescriptor, string adoServerUrl)
        {
            var gitReposNamespace = "2e9eb7ed-3c0a-47d4-87c1-0ffdd275fd87";
            var token = $"repoV2/{teamProjectId}/{repoId}";

            var url = $"{adoServerUrl}/{org}/_apis/accesscontrollists/{gitReposNamespace}?token={token}&descriptors={identityDescriptor}&api-version=6.0";

            var response = await _adoClient.GetAsync(url);

            var allow = (int)JObject.Parse(response)["value"].First()["acesDictionary"][identityDescriptor]["allow"];
            var deny = (int)JObject.Parse(response)["value"].First()["acesDictionary"][identityDescriptor]["deny"];

            return (allow, deny);
        }

        private async Task<IEnumerable<(string Id, string Name, string Type)>> GetServiceConnections(string org, string teamProject, string adoServerUrl)
        {
            var url = $"{adoServerUrl}/{org}/{teamProject}/_apis/serviceendpoint/endpoints?api-version=6.0-preview.4";
            var response = await _adoClient.GetWithPagingAsync(url);
            return response.Select(x => ((string)x["id"], (string)x["name"], (string)x["type"]));
        }

        private async Task<(string Id, string Type, string ConnectedServiceId)> GetPipelineRepo(string org, string teamProject, int pipelineId, string adoServerUrl)
        {
            var url = $"{adoServerUrl}/{org}/{teamProject}/_apis/build/definitions/{pipelineId}?api-version=6.0";
            var response = await _adoClient.GetAsync(url);
            var result = JObject.Parse(response)["repository"];
            return ((string)result["id"], (string)result["type"], (string)result["properties"]["connectedServiceId"]);
        }

        private async Task<string> DeleteTeamProject(string org, string teamProjectId, string adoServerUrl)
        {
            var url = $"{adoServerUrl}/{org}/_apis/projects/{teamProjectId}?api-version=6.0";

            var response = await _adoClient.DeleteAsync(url);
            var result = JObject.Parse(response);

            return (string)result["id"];
        }

        private async Task<string> QueueCreateTeamProject(string org, string teamProject, string adoServerUrl)
        {
            var url = $"{adoServerUrl}/{org}/_apis/projects?api-version=6.0";

            var payload = new
            {
                name = teamProject,
                capabilities = new
                {
                    versioncontrol = new
                    {
                        sourceControlType = "Git"
                    },
                    processTemplate = new
                    {
                        templateTypeId = "6b724908-ef14-45cf-84f8-768b5384da45"
                    }
                }
            };

            var response = await _adoClient.PostAsync(url, payload);
            var result = JObject.Parse(response);

            return (string)result["id"];
        }

        private async Task<string> GetTeamProjectStatus(string org, string teamProjectId, string adoServerUrl)
        {
            var url = $"{adoServerUrl}/{org}/_apis/projects/{teamProjectId}?api-version=6.0";
            var response = await _adoClient.GetAsync(url);
            return (string)JObject.Parse(response)["state"];
        }

        private async Task<string> GetOperationStatus(string org, string operationId, string adoServerUrl)
        {
            var url = $"{adoServerUrl}/{org}/_apis/operations/{operationId}?api-version=6.0";
            var response = await _adoClient.GetAsync(url);
            return (string)JObject.Parse(response)["status"];
        }

        private async Task<IEnumerable<string>> GetTeamSlugs(string org)
        {
            var url = $"{GithubApiBaseUrl}/orgs/{org}/teams";

            var response = await _githubClient.GetAsync(url);
            var data = JArray.Parse(response);

            return data.Children().Select(x => (string)x["slug"]).ToList();
        }

        private async Task<IEnumerable<string>> GetTeamNames(string org)
        {
            var url = $"{GithubApiBaseUrl}/orgs/{org}/teams";

            var response = await _githubClient.GetAsync(url);
            var data = JArray.Parse(response);

            return data.Children().Select(x => (string)x["name"]).ToList();
        }

        private async Task DeleteTeam(string org, string teamSlug)
        {
            var url = $"{GithubApiBaseUrl}/orgs/{org}/teams/{teamSlug}";
            await _githubClient.DeleteAsync(url);
        }

        private async Task CreateRepo(string org, string repo, bool isPrivate, bool isInitialized)
        {
            var url = $"{GithubApiBaseUrl}/orgs/{org}/repos";

            var payload = new
            {
                name = repo,
                @private = isPrivate,
                auto_init = isInitialized
            };

            _ = await _githubClient.PostAsync(url, payload);
        }

        private async Task<IEnumerable<string>> GetRepoCommitShas(string org, string repo)
        {
            var retryPolicy = new RetryPolicy(null);

            var url = $"{GithubApiBaseUrl}/repos/{org}/{repo}/commits";
            var commits = await retryPolicy.Retry(async () => await _githubClient.GetAllAsync(url).ToListAsync());
            return commits.Select(x => (string)x["sha"]).ToList();
        }

        private async Task<IEnumerable<(string id, string key, string url)>> GetAutolinks(string org, string repo)
        {
            var url = $"{GithubApiBaseUrl}/repos/{org}/{repo}/autolinks";
            var autolinks = await _githubClient.GetAllAsync(url).ToListAsync();
            return autolinks.Select(x => ((string)x["id"], (string)x["key_prefix"], (string)x["url_template"])).ToList();
        }

        private async Task<string> GetTeamIdPGroup(string org, string teamSlug)
        {
            var url = $"{GithubApiBaseUrl}/orgs/{org}/teams/{teamSlug}/external-groups";
            var response = await _githubClient.GetAsync(url);
            return (string)JObject.Parse(response)["groups"].Single()["group_name"];
        }

        private async Task<string> GetTeamRepoRole(string org, string teamSlug, string repo)
        {
            var url = $"{GithubApiBaseUrl}/orgs/{org}/teams/{teamSlug}/repos";
            var response = await _githubClient.GetAllAsync(url).ToListAsync();
            return (string)response.Single(x => (string)x["name"] == repo)["role_name"];
        }

        public static string GetOsName()
        {
            return RuntimeInformation.IsOSPlatform(OSPlatform.Linux)
                ? "linux"
                : RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
                ? "windows"
                : RuntimeInformation.IsOSPlatform(OSPlatform.OSX)
                ? "macos"
                : throw new InvalidOperationException("Could not determine OS");
        }

        public async Task RunCliMigration(string generateScriptCommand, string cliName, IReadOnlyDictionary<string, string> tokens)
        {
            await RunCliCommand(generateScriptCommand, cliName, tokens);
            LogMigrationScript("migrate.ps1");
            await RunPowershellScript("migrate.ps1", tokens);
        }

        private void LogMigrationScript(string filename)
        {
            var filePath = Path.Join(GetOsDistPath(), filename);
            var scriptContents = File.ReadAllText(filePath);
            _output.WriteLine($"Contents of {filename}:");
            _output.WriteLine(scriptContents);
        }

        public async Task RunPowershellScript(string script, IReadOnlyDictionary<string, string> tokens) =>
            await RunShellCommand($"-File {Path.Join(GetOsDistPath(), script)}", "pwsh", GetOsDistPath(), tokens);

        public async Task RunCliCommand(string command, string cliName, IReadOnlyDictionary<string, string> tokens) =>
            await RunShellCommand(command, cliName, GetOsDistPath(), tokens);

        private async Task RunShellCommand(string command, string fileName, string workingDirectory = null, IReadOnlyDictionary<string, string> environmentVariables = null)
        {
            _output.WriteLine($"Running command: {fileName} {command}");

            var result = await Cli.Wrap(fileName)
                .WithArguments(command)
                .WithWorkingDirectory(workingDirectory ?? Directory.GetCurrentDirectory())
                .WithEnvironmentVariables(environmentVariables ?? new Dictionary<string, string>())
                .WithStandardOutputPipe(PipeTarget.ToDelegate(line => _output.WriteLine(line)))
                .WithStandardErrorPipe(PipeTarget.ToDelegate(line => _output.WriteLine(line)))
                .ExecuteAsync();

            result.ExitCode.Should().Be(0, $"{fileName} should return an exit code of 0.");
        }

        public async Task RunAdoToGithubCliMigration(string generateScriptCommand, IReadOnlyDictionary<string, string> tokens) =>
            await RunCliMigration($"ado2gh {generateScriptCommand}", "gh", tokens);

        public async Task RunGeiCliMigration(string generateScriptCommand, IReadOnlyDictionary<string, string> tokens) =>
            await RunCliMigration($"gei {generateScriptCommand}", "gh", tokens);

        public async Task RunBbsCliMigration(string generateScriptCommand, IReadOnlyDictionary<string, string> tokens) =>
            await RunCliMigration($"bbs2gh {generateScriptCommand}", "gh", tokens);

        public static string GetOsDistPath()
        {
            return RuntimeInformation.IsOSPlatform(OSPlatform.Linux)
                ? Path.Join(Directory.GetCurrentDirectory(), "../../../../../dist/linux-x64")
                : RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
                ? Path.Join(Directory.GetCurrentDirectory(), "../../../../../dist/win-x64")
                : RuntimeInformation.IsOSPlatform(OSPlatform.OSX)
                ? Path.Join(Directory.GetCurrentDirectory(), "../../../../../dist/osx-x64")
                : throw new InvalidOperationException("Could not determine OS");
        }

        public async Task AssertGithubRepoExists(string githubOrg, string repo)
        {
            _output.WriteLine("Checking that the repos in GitHub exist...");
            var repos = await _githubApi.GetRepos(githubOrg);
            repos.Select(x => x.Name).Should().Contain(repo);
        }

        public async Task AssertGithubRepoInitialized(string githubOrg, string repo)
        {
            _output.WriteLine("Checking that the repos in GitHub are initialized...");

            var commits = await GetRepoCommitShas(githubOrg, repo);
            commits.Count().Should().BeGreaterThan(0);
        }

        public async Task AssertAutolinkConfigured(string githubOrg, string repo, string urlTemplate)
        {
            _output.WriteLine("Checking that the Autolinks have been configured...");

            var autolinks = await GetAutolinks(githubOrg, repo);
            autolinks.Where(x => x.key == "AB#" && x.url == urlTemplate)
                     .Count()
                     .Should().Be(1);
        }

        public async Task AssertAdoRepoDisabled(string adoOrg, string teamProject, string repo, string adoServerUrl = "https://dev.azure.com")
        {
            _output.WriteLine("Checking that the ADO repos have been disabled...");

            var reposDisabled = await GetReposDisabledState(adoOrg, teamProject, adoServerUrl);
            reposDisabled.Should().Contain(x => x.repo == repo && x.disabled);
        }
        public async Task AssertAdoRepoLocked(string adoOrg, string teamProject, string repo, string adoServerUrl = "https://dev.azure.com")
        {
            _output.WriteLine("Checking that the ADO repos were locked...");

            var teamProjectId = await _adoApi.GetTeamProjectId(adoOrg, teamProject);
            var repoId = await _adoApi.GetRepoId(adoOrg, teamProject, repo);
            var identityDescriptor = await _adoApi.GetIdentityDescriptor(adoOrg, teamProjectId, "Project Valid Users");
            var (_, deny) = await GetRepoPermissions(adoOrg, teamProjectId, repoId, identityDescriptor, adoServerUrl);

            deny.Should().Be(56828);
        }

        public async Task AssertGithubTeamCreated(string githubOrg, string teamName)
        {
            _output.WriteLine("Checking that the GitHub teams were created...");
            var githubTeams = await GetTeamNames(githubOrg);

            githubTeams.Should().Contain(teamName);
        }

        public async Task AssertGithubTeamIdpLinked(string githubOrg, string teamName, string idpGroup)
        {
            _output.WriteLine("Checking that the GitHub teams are linked to IdP groups...");

            var teamSlug = await _githubApi.GetTeamSlug(githubOrg, teamName);
            var idp = await GetTeamIdPGroup(githubOrg, teamSlug);
            idp.ToLower().Should().Be(idpGroup?.ToLower());
        }

        public async Task AssertGithubTeamHasRepoRole(string githubOrg, string teamName, string repo, string role)
        {
            _output.WriteLine("Checking that the GitHub teams have repo permissions...");

            var teamSlug = await _githubApi.GetTeamSlug(githubOrg, teamName);
            var actualRole = await GetTeamRepoRole(githubOrg, teamSlug, repo);
            actualRole.Should().Be(role);
        }

        public async Task AssertServiceConnectionWasShared(string adoOrg, string teamProject, string adoServerUrl = "https://dev.azure.com")
        {
            _output.WriteLine("Checking that the service connection was shared...");

            var serviceConnections = await GetServiceConnections(adoOrg, teamProject, adoServerUrl);
            serviceConnections.Should().Contain(x => x.Type == "GitHub");
        }

        public async Task AssertPipelineRewired(string adoOrg, string teamProject, string pipeline, string githubOrg, string githubRepo, string adoServerUrl = "https://dev.azure.com")
        {
            _output.WriteLine("Checking that the pipelines are rewired...");

            var pipelineId = await _adoApi.GetPipelineId(adoOrg, teamProject, pipeline);
            var pipelineRepo = await GetPipelineRepo(adoOrg, teamProject, pipelineId, adoServerUrl);

            var serviceConnectionId = (await GetServiceConnections(adoOrg, teamProject, adoServerUrl)).First(x => x.Type == "GitHub");
            pipelineRepo.Type.Should().Be("GitHub");
            pipelineRepo.Id.Should().Be($"{githubOrg}/{githubRepo}");
            pipelineRepo.ConnectedServiceId.Should().Be(serviceConnectionId.Id);
        }

        public async Task AssertBoardsIntegrationConfigured(string adoOrg, string teamProject)
        {
            _output.WriteLine("Checking that the boards integration is configured...");

            var boardsConnection = await _adoApi.GetBoardsGithubConnection(adoOrg, teamProject);

            boardsConnection.Should().NotBeNull();
            boardsConnection.repoIds.Count().Should().Be(1);
        }

        public void AssertMigrationLogFileExists(string githubOrg, string repo)
        {
            _output.WriteLine("Checking that the migration log was downloaded...");

            var logFiles = new DirectoryInfo(GetOsDistPath()).GetFiles("*.log");

            var matchingLogFiles = logFiles.Where(fi => fi.Name.StartsWith($"migration-log-{githubOrg}-{repo}-") && fi.Name.EndsWith(".log"));

            matchingLogFiles.Any().Should().BeTrue();
        }

        public void AssertNoErrorInLogs(DateTime after)
        {
            _output.WriteLine("Checking that CLI logs have no errors...");

            var directoryInfo = new DirectoryInfo(GetOsDistPath());

            var firstLogFileWithError = directoryInfo.GetFiles("*.octoshift.log")
                .Select(fi => (Timestamp: DateTime.ParseExact(fi.Name.Split('.').First().Split('-').First(), "yyyyMMddHHmmss", null), FileInfo: fi))
                .Where(x => x.Timestamp >= after)
                .OrderBy(x => x.Timestamp)
                .Select(x => x.FileInfo)
                .FirstOrDefault(fi => File.ReadAllLines(fi.FullName).Any(line => line.Contains("[ERROR]")));

            var firstError = firstLogFileWithError is not null
                ? File.ReadAllLines(firstLogFileWithError.FullName).First(line => line.Contains("[ERROR]"))
                : null;

            firstError.Should().BeNull();
        }

        public async Task ResetBlobContainers()
        {
            _output.WriteLine($"Deleting all blob containers...");
            await foreach (var blobContainer in _blobServiceClient.GetBlobContainersAsync())
            {
                _output.WriteLine($"Deleting blob container: {blobContainer.Name}");
                await _blobServiceClient.DeleteBlobContainerAsync(blobContainer.Name);
            }
        }

        private static void DeleteMigrationLog(string githubOrg, string githubRepo)
        {
            var migrationLogFileFullName = Path.Join(GetOsDistPath(), $"migration-log-{githubOrg}-{githubRepo}.log");
            if (File.Exists(migrationLogFileFullName))
            {
                File.Delete(migrationLogFileFullName);
            }
        }
    }
}
