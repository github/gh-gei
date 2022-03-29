using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using FluentAssertions;
using Moq;
using OctoshiftCLI.AdoToGithub;
using OctoshiftCLI.AdoToGithub.Commands;
using Xunit;

namespace OctoshiftCLI.Tests.AdoToGithub.Commands
{
    public class GenerateScriptCommandTests
    {
        [Fact]
        public void Should_Have_Options()
        {
            var command = new GenerateScriptCommand(null, null);
            command.Should().NotBeNull();
            command.Name.Should().Be("generate-script");
            command.Options.Count.Should().Be(9);

            TestHelpers.VerifyCommandOption(command.Options, "github-org", true);
            TestHelpers.VerifyCommandOption(command.Options, "ado-org", false);
            TestHelpers.VerifyCommandOption(command.Options, "output", false);
            TestHelpers.VerifyCommandOption(command.Options, "repos-only", false);
            TestHelpers.VerifyCommandOption(command.Options, "skip-idp", false);
            TestHelpers.VerifyCommandOption(command.Options, "ssh", false, true);
            TestHelpers.VerifyCommandOption(command.Options, "sequential", false);
            TestHelpers.VerifyCommandOption(command.Options, "ado-pat", false);
            TestHelpers.VerifyCommandOption(command.Options, "verbose", false);
        }

        [Fact]
        public void No_Data()
        {
            var githubOrg = "foo-gh-org";

            var command = new GenerateScriptCommand(null, null);
            var script = command.GenerateSequentialScript(null, null, null, githubOrg, false);

            Assert.True(string.IsNullOrWhiteSpace(script));
        }

        [Fact]
        public void Github_SequentialScript_StartsWithShebang()
        {
            var githubOrg = "foo-gh-org";
            var adoOrg = "foo-ado-org";
            var adoTeamProject = "foo-team-project";
            var repo = "foo-repo";

            var repos = new Dictionary<string, IDictionary<string, IEnumerable<string>>>
            {
                { adoOrg, new Dictionary<string, IEnumerable<string>>() }
            };

            repos[adoOrg].Add(adoTeamProject, new List<string>() { repo });

            var command = new GenerateScriptCommand(new Mock<OctoLogger>().Object, null);
            var script = command.GenerateSequentialScript(repos, null, null, githubOrg, false);

            script.Should().StartWith("#!/usr/bin/pwsh");
        }

        [Fact]
        public void Single_Repo()
        {
            var githubOrg = "foo-gh-org";
            var adoOrg = "foo-ado-org";
            var adoTeamProject = "foo-team-project";
            var repo = "foo-repo";

            var repos = new Dictionary<string, IDictionary<string, IEnumerable<string>>>
            {
                { adoOrg, new Dictionary<string, IEnumerable<string>>() }
            };

            repos[adoOrg].Add(adoTeamProject, new List<string>() { repo });

            var command = new GenerateScriptCommand(new Mock<OctoLogger>().Object, null);
            var script = command.GenerateSequentialScript(repos, null, null, githubOrg, false);

            script = TrimNonExecutableLines(script);

            var expected = $"Exec {{ ./ado2gh create-team --github-org \"{githubOrg}\" --team-name \"{adoTeamProject}-Maintainers\" --idp-group \"{adoTeamProject}-Maintainers\" }}";
            expected += Environment.NewLine;
            expected += $"Exec {{ ./ado2gh create-team --github-org \"{githubOrg}\" --team-name \"{adoTeamProject}-Admins\" --idp-group \"{adoTeamProject}-Admins\" }}";
            expected += Environment.NewLine;
            expected += $"Exec {{ ./ado2gh lock-ado-repo --ado-org \"{adoOrg}\" --ado-team-project \"{adoTeamProject}\" --ado-repo \"{repo}\" }}";
            expected += Environment.NewLine;
            expected += $"Exec {{ ./ado2gh migrate-repo --ado-org \"{adoOrg}\" --ado-team-project \"{adoTeamProject}\" --ado-repo \"{repo}\" --github-org \"{githubOrg}\" --github-repo \"{adoTeamProject}-{repo}\" --wait }}";
            expected += Environment.NewLine;
            expected += $"Exec {{ ./ado2gh disable-ado-repo --ado-org \"{adoOrg}\" --ado-team-project \"{adoTeamProject}\" --ado-repo \"{repo}\" }}";
            expected += Environment.NewLine;
            expected += $"Exec {{ ./ado2gh configure-autolink --github-org \"{githubOrg}\" --github-repo \"{adoTeamProject}-{repo}\" --ado-org \"{adoOrg}\" --ado-team-project \"{adoTeamProject}\" }}";
            expected += Environment.NewLine;
            expected += $"Exec {{ ./ado2gh add-team-to-repo --github-org \"{githubOrg}\" --github-repo \"{adoTeamProject}-{repo}\" --team \"{adoTeamProject}-Maintainers\" --role \"maintain\" }}";
            expected += Environment.NewLine;
            expected += $"Exec {{ ./ado2gh add-team-to-repo --github-org \"{githubOrg}\" --github-repo \"{adoTeamProject}-{repo}\" --team \"{adoTeamProject}-Admins\" --role \"admin\" }}";
            expected += Environment.NewLine;
            expected += $"Exec {{ ./ado2gh integrate-boards --ado-org \"{adoOrg}\" --ado-team-project \"{adoTeamProject}\" --github-org \"{githubOrg}\" --github-repo \"{adoTeamProject}-{repo}\" }}";

            Assert.Equal(expected, script);
        }

        [Fact]
        public void Skip_Team_Project_With_No_Repos()
        {
            var githubOrg = "foo-gh-org";
            var adoOrg = "foo-ado-org";
            var adoTeamProject = "foo-team-project";

            var repos = new Dictionary<string, IDictionary<string, IEnumerable<string>>>
            {
                { adoOrg, new Dictionary<string, IEnumerable<string>>() }
            };

            repos[adoOrg].Add(adoTeamProject, new List<string>());

            var command = new GenerateScriptCommand(new Mock<OctoLogger>().Object, null);
            var script = command.GenerateSequentialScript(repos, null, null, githubOrg, false);

            script = TrimNonExecutableLines(script);

            Assert.Equal(string.Empty, script);
        }

        [Fact]
        public void Single_Repo_Two_Pipelines()
        {
            var githubOrg = "foo-gh-org";
            var adoOrg = "foo-ado-org";
            var adoTeamProject = "foo-team-project";
            var repo = "foo-repo";
            var pipelineOne = "CICD";
            var pipelineTwo = "Publish";
            var appId = Guid.NewGuid().ToString();

            var repos = new Dictionary<string, IDictionary<string, IEnumerable<string>>>
            {
                { adoOrg, new Dictionary<string, IEnumerable<string>>() }
            };

            repos[adoOrg].Add(adoTeamProject, new List<string>() { repo });

            var pipelines = new Dictionary<string, IDictionary<string, IDictionary<string, IEnumerable<string>>>>
            {
                { adoOrg, new Dictionary<string, IDictionary<string, IEnumerable<string>>>() }
            };

            pipelines[adoOrg].Add(adoTeamProject, new Dictionary<string, IEnumerable<string>>());
            pipelines[adoOrg][adoTeamProject].Add(repo, new List<string>() { pipelineOne, pipelineTwo });

            var appIds = new Dictionary<string, string>
            {
                { adoOrg, appId }
            };

            var command = new GenerateScriptCommand(new Mock<OctoLogger>().Object, null);
            var script = command.GenerateSequentialScript(repos, pipelines, appIds, githubOrg, false);

            script = TrimNonExecutableLines(script);

            var expected = $"Exec {{ ./ado2gh create-team --github-org \"{githubOrg}\" --team-name \"{adoTeamProject}-Maintainers\" --idp-group \"{adoTeamProject}-Maintainers\" }}";
            expected += Environment.NewLine;
            expected += $"Exec {{ ./ado2gh create-team --github-org \"{githubOrg}\" --team-name \"{adoTeamProject}-Admins\" --idp-group \"{adoTeamProject}-Admins\" }}";
            expected += Environment.NewLine;
            expected += $"Exec {{ ./ado2gh share-service-connection --ado-org \"{adoOrg}\" --ado-team-project \"{adoTeamProject}\" --service-connection-id \"{appId}\" }}";
            expected += Environment.NewLine;
            expected += $"Exec {{ ./ado2gh lock-ado-repo --ado-org \"{adoOrg}\" --ado-team-project \"{adoTeamProject}\" --ado-repo \"{repo}\" }}";
            expected += Environment.NewLine;
            expected += $"Exec {{ ./ado2gh migrate-repo --ado-org \"{adoOrg}\" --ado-team-project \"{adoTeamProject}\" --ado-repo \"{repo}\" --github-org \"{githubOrg}\" --github-repo \"{adoTeamProject}-{repo}\" --wait }}";
            expected += Environment.NewLine;
            expected += $"Exec {{ ./ado2gh disable-ado-repo --ado-org \"{adoOrg}\" --ado-team-project \"{adoTeamProject}\" --ado-repo \"{repo}\" }}";
            expected += Environment.NewLine;
            expected += $"Exec {{ ./ado2gh configure-autolink --github-org \"{githubOrg}\" --github-repo \"{adoTeamProject}-{repo}\" --ado-org \"{adoOrg}\" --ado-team-project \"{adoTeamProject}\" }}";
            expected += Environment.NewLine;
            expected += $"Exec {{ ./ado2gh add-team-to-repo --github-org \"{githubOrg}\" --github-repo \"{adoTeamProject}-{repo}\" --team \"{adoTeamProject}-Maintainers\" --role \"maintain\" }}";
            expected += Environment.NewLine;
            expected += $"Exec {{ ./ado2gh add-team-to-repo --github-org \"{githubOrg}\" --github-repo \"{adoTeamProject}-{repo}\" --team \"{adoTeamProject}-Admins\" --role \"admin\" }}";
            expected += Environment.NewLine;
            expected += $"Exec {{ ./ado2gh integrate-boards --ado-org \"{adoOrg}\" --ado-team-project \"{adoTeamProject}\" --github-org \"{githubOrg}\" --github-repo \"{adoTeamProject}-{repo}\" }}";
            expected += Environment.NewLine;
            expected += $"Exec {{ ./ado2gh rewire-pipeline --ado-org \"{adoOrg}\" --ado-team-project \"{adoTeamProject}\" --ado-pipeline \"{pipelineOne}\" --github-org \"{githubOrg}\" --github-repo \"{adoTeamProject}-{repo}\" --service-connection-id \"{appId}\" }}";
            expected += Environment.NewLine;
            expected += $"Exec {{ ./ado2gh rewire-pipeline --ado-org \"{adoOrg}\" --ado-team-project \"{adoTeamProject}\" --ado-pipeline \"{pipelineTwo}\" --github-org \"{githubOrg}\" --github-repo \"{adoTeamProject}-{repo}\" --service-connection-id \"{appId}\" }}";

            Assert.Equal(expected, script);
        }

        [Fact]
        public void Single_Repo_Two_Pipelines_No_Service_Connection()
        {
            var githubOrg = "foo-gh-org";
            var adoOrg = "foo-ado-org";
            var adoTeamProject = "foo-team-project";
            var repo = "foo-repo";
            var pipelineOne = "CICD";
            var pipelineTwo = "Publish";

            var repos = new Dictionary<string, IDictionary<string, IEnumerable<string>>>
            {
                { adoOrg, new Dictionary<string, IEnumerable<string>>() }
            };

            repos[adoOrg].Add(adoTeamProject, new List<string>() { repo });

            var pipelines = new Dictionary<string, IDictionary<string, IDictionary<string, IEnumerable<string>>>>
            {
                { adoOrg, new Dictionary<string, IDictionary<string, IEnumerable<string>>>() }
            };

            pipelines[adoOrg].Add(adoTeamProject, new Dictionary<string, IEnumerable<string>>());
            pipelines[adoOrg][adoTeamProject].Add(repo, new List<string>() { pipelineOne, pipelineTwo });

            var appIds = new Dictionary<string, string>();

            var command = new GenerateScriptCommand(new Mock<OctoLogger>().Object, null);
            var script = command.GenerateSequentialScript(repos, pipelines, appIds, githubOrg, false);

            script = TrimNonExecutableLines(script);

            var expected = $"Exec {{ ./ado2gh create-team --github-org \"{githubOrg}\" --team-name \"{adoTeamProject}-Maintainers\" --idp-group \"{adoTeamProject}-Maintainers\" }}";
            expected += Environment.NewLine;
            expected += $"Exec {{ ./ado2gh create-team --github-org \"{githubOrg}\" --team-name \"{adoTeamProject}-Admins\" --idp-group \"{adoTeamProject}-Admins\" }}";
            expected += Environment.NewLine;
            expected += $"Exec {{ ./ado2gh lock-ado-repo --ado-org \"{adoOrg}\" --ado-team-project \"{adoTeamProject}\" --ado-repo \"{repo}\" }}";
            expected += Environment.NewLine;
            expected += $"Exec {{ ./ado2gh migrate-repo --ado-org \"{adoOrg}\" --ado-team-project \"{adoTeamProject}\" --ado-repo \"{repo}\" --github-org \"{githubOrg}\" --github-repo \"{adoTeamProject}-{repo}\" --wait }}";
            expected += Environment.NewLine;
            expected += $"Exec {{ ./ado2gh disable-ado-repo --ado-org \"{adoOrg}\" --ado-team-project \"{adoTeamProject}\" --ado-repo \"{repo}\" }}";
            expected += Environment.NewLine;
            expected += $"Exec {{ ./ado2gh configure-autolink --github-org \"{githubOrg}\" --github-repo \"{adoTeamProject}-{repo}\" --ado-org \"{adoOrg}\" --ado-team-project \"{adoTeamProject}\" }}";
            expected += Environment.NewLine;
            expected += $"Exec {{ ./ado2gh add-team-to-repo --github-org \"{githubOrg}\" --github-repo \"{adoTeamProject}-{repo}\" --team \"{adoTeamProject}-Maintainers\" --role \"maintain\" }}";
            expected += Environment.NewLine;
            expected += $"Exec {{ ./ado2gh add-team-to-repo --github-org \"{githubOrg}\" --github-repo \"{adoTeamProject}-{repo}\" --team \"{adoTeamProject}-Admins\" --role \"admin\" }}";
            expected += Environment.NewLine;
            expected += $"Exec {{ ./ado2gh integrate-boards --ado-org \"{adoOrg}\" --ado-team-project \"{adoTeamProject}\" --github-org \"{githubOrg}\" --github-repo \"{adoTeamProject}-{repo}\" }}";

            Assert.Equal(expected, script);
        }

        [Fact]
        public void Single_Repo_Repos_Only()
        {
            var githubOrg = "foo-gh-org";
            var adoOrg = "foo-ado-org";
            var adoTeamProject = "foo-team-project";
            var repo = "foo-repo";

            var repos = new Dictionary<string, IDictionary<string, IEnumerable<string>>>
            {
                { adoOrg, new Dictionary<string, IEnumerable<string>>() }
            };

            repos[adoOrg].Add(adoTeamProject, new List<string>() { repo });

            var command = new GenerateScriptCommand(new Mock<OctoLogger>().Object, null);
            // The way reposOnly is implemented is kind of hacky, this will change when we refactor all the options in issue #21
            // for now going to leave it as is and use reflection to force the test to work
            var reposOnlyField = typeof(GenerateScriptCommand).GetField("_reposOnly", BindingFlags.Instance | BindingFlags.NonPublic);
            reposOnlyField.SetValue(command, true);

            var script = command.GenerateSequentialScript(repos, null, null, githubOrg, false);

            script = TrimNonExecutableLines(script);

            var expected = $"Exec {{ ./ado2gh migrate-repo --ado-org \"{adoOrg}\" --ado-team-project \"{adoTeamProject}\" --ado-repo \"{repo}\" --github-org \"{githubOrg}\" --github-repo \"{adoTeamProject}-{repo}\" --wait }}";

            Assert.Equal(expected, script);
        }

        [Fact]
        public void Single_Repo_Skip_Idp()
        {
            var githubOrg = "foo-gh-org";
            var adoOrg = "foo-ado-org";
            var adoTeamProject = "foo-team-project";
            var repo = "foo-repo";

            var repos = new Dictionary<string, IDictionary<string, IEnumerable<string>>>
            {
                { adoOrg, new Dictionary<string, IEnumerable<string>>() }
            };

            repos[adoOrg].Add(adoTeamProject, new List<string>() { repo });

            var command = new GenerateScriptCommand(new Mock<OctoLogger>().Object, null);
            var script = command.GenerateSequentialScript(repos, null, null, githubOrg, true);

            script = TrimNonExecutableLines(script);

            var expected = $"Exec {{ ./ado2gh create-team --github-org \"{githubOrg}\" --team-name \"{adoTeamProject}-Maintainers\" }}";
            expected += Environment.NewLine;
            expected += $"Exec {{ ./ado2gh create-team --github-org \"{githubOrg}\" --team-name \"{adoTeamProject}-Admins\" }}";
            expected += Environment.NewLine;
            expected += $"Exec {{ ./ado2gh lock-ado-repo --ado-org \"{adoOrg}\" --ado-team-project \"{adoTeamProject}\" --ado-repo \"{repo}\" }}";
            expected += Environment.NewLine;
            expected += $"Exec {{ ./ado2gh migrate-repo --ado-org \"{adoOrg}\" --ado-team-project \"{adoTeamProject}\" --ado-repo \"{repo}\" --github-org \"{githubOrg}\" --github-repo \"{adoTeamProject}-{repo}\" --wait }}";
            expected += Environment.NewLine;
            expected += $"Exec {{ ./ado2gh disable-ado-repo --ado-org \"{adoOrg}\" --ado-team-project \"{adoTeamProject}\" --ado-repo \"{repo}\" }}";
            expected += Environment.NewLine;
            expected += $"Exec {{ ./ado2gh configure-autolink --github-org \"{githubOrg}\" --github-repo \"{adoTeamProject}-{repo}\" --ado-org \"{adoOrg}\" --ado-team-project \"{adoTeamProject}\" }}";
            expected += Environment.NewLine;
            expected += $"Exec {{ ./ado2gh add-team-to-repo --github-org \"{githubOrg}\" --github-repo \"{adoTeamProject}-{repo}\" --team \"{adoTeamProject}-Maintainers\" --role \"maintain\" }}";
            expected += Environment.NewLine;
            expected += $"Exec {{ ./ado2gh add-team-to-repo --github-org \"{githubOrg}\" --github-repo \"{adoTeamProject}-{repo}\" --team \"{adoTeamProject}-Admins\" --role \"admin\" }}";
            expected += Environment.NewLine;
            expected += $"Exec {{ ./ado2gh integrate-boards --ado-org \"{adoOrg}\" --ado-team-project \"{adoTeamProject}\" --github-org \"{githubOrg}\" --github-repo \"{adoTeamProject}-{repo}\" }}";

            Assert.Equal(expected, script);
        }

        [Fact]
        public async Task GetOrgs_All_Orgs()
        {
            var userId = "foo-user";
            var org1 = "foo-1";
            var org2 = "foo-2";
            var orgs = new List<string>() { org1, org2 };

            var mockAdo = new Mock<AdoApi>(null);

            mockAdo.Setup(x => x.GetUserId().Result).Returns(userId);
            mockAdo.Setup(x => x.GetOrganizations(userId).Result).Returns(orgs);

            var command = new GenerateScriptCommand(new Mock<OctoLogger>().Object, null);
            var result = await command.GetOrgs(mockAdo.Object, null);

            Assert.Equal(2, result.Count());
            Assert.Contains(result, x => x == org1);
            Assert.Contains(result, x => x == org2);
        }

        [Fact]
        public async Task GetOrgs_Org_Provided()
        {
            var org1 = "foo-1";

            var command = new GenerateScriptCommand(new Mock<OctoLogger>().Object, null);
            var result = await command.GetOrgs(null, org1);

            Assert.Single(result);
            Assert.Contains(result, x => x == org1);
        }

        [Fact]
        public async Task GetRepos_Two_Repos_Two_Team_Projects()
        {
            var org = "foo-org";
            var orgs = new List<string>() { org };
            var teamProject1 = "foo-tp1";
            var teamProject2 = "foo-tp2";
            var teamProjects = new List<string>() { teamProject1, teamProject2 };
            var repo1 = "foo-repo1";
            var repo2 = "foo-repo2";

            var mockAdo = new Mock<AdoApi>(null);

            mockAdo.Setup(x => x.GetTeamProjects(org).Result).Returns(teamProjects);
            mockAdo.Setup(x => x.GetEnabledRepos(org, teamProject1).Result).Returns(new List<string>() { repo1 });
            mockAdo.Setup(x => x.GetEnabledRepos(org, teamProject2).Result).Returns(new List<string>() { repo2 });

            var command = new GenerateScriptCommand(new Mock<OctoLogger>().Object, null);
            var result = await command.GetRepos(mockAdo.Object, orgs);

            Assert.Single(result[org][teamProject1]);
            Assert.Single(result[org][teamProject2]);
            Assert.Contains(result[org][teamProject1], x => x == repo1);
            Assert.Contains(result[org][teamProject2], x => x == repo2);
        }

        [Fact]
        public async Task GetPipelines_One_Repo_Two_Pipelines()
        {
            var org = "foo-org";
            var teamProject = "foo-tp";
            var repo = "foo-repo";
            var repoId = Guid.NewGuid().ToString();
            var repos = new Dictionary<string, IDictionary<string, IEnumerable<string>>>();
            var pipeline1 = "foo-pipeline-1";
            var pipeline2 = "foo-pipeline-2";

            repos.Add(org, new Dictionary<string, IEnumerable<string>>());
            repos[org].Add(teamProject, new List<string>() { repo });

            var mockAdo = new Mock<AdoApi>(null);

            mockAdo.Setup(x => x.GetRepoId(org, teamProject, repo).Result).Returns(repoId);
            mockAdo.Setup(x => x.GetPipelines(org, teamProject, repoId).Result).Returns(new List<string>() { pipeline1, pipeline2 });

            var command = new GenerateScriptCommand(new Mock<OctoLogger>().Object, null);
            var result = await command.GetPipelines(mockAdo.Object, repos);

            Assert.Equal(2, result[org][teamProject][repo].Count());
            Assert.Contains(result[org][teamProject][repo], x => x == pipeline1);
            Assert.Contains(result[org][teamProject][repo], x => x == pipeline2);
        }

        [Fact]
        public async Task GetAppIds_Service_Connect_Exists()
        {
            var org = "foo-org";
            var orgs = new List<string>() { org };
            var githubOrg = "foo-gh-org";
            var teamProject1 = "foo-tp1";
            var teamProject2 = "foo-tp2";
            var teamProjects = new List<string>() { teamProject1, teamProject2 };
            var appId = Guid.NewGuid().ToString();

            var mockAdo = new Mock<AdoApi>(null);

            mockAdo.Setup(x => x.GetTeamProjects(org).Result).Returns(teamProjects);
            mockAdo.Setup(x => x.GetGithubAppId(org, githubOrg, teamProjects).Result).Returns(appId);

            var command = new GenerateScriptCommand(new Mock<OctoLogger>().Object, null);
            var result = await command.GetAppIds(mockAdo.Object, orgs, githubOrg);

            Assert.Equal(appId, result[org]);
        }

        [Fact]
        public void GenerateParallelScript_One_Team_Projects_Two_Repos()
        {
            // Arrange
            const string adoOrg = "ADO_ORG";
            const string adoTeamProject = "ADO_TEAM_PROJECT";
            const string fooRepo = "FOO_REPO";
            const string fooPipeline = "FOO_PIPELINE";
            const string barRepo = "BAR_REPO";
            const string barPipeline = "BAR_PIPELINE";
            const string appId = "d9edf292-c6fd-4440-af2b-d08fcc9c9dd1";
            const string githubOrg = "GITHUB_ORG";

            var repos = new Dictionary<string, IDictionary<string, IEnumerable<string>>>
            {
                {
                    adoOrg,
                    new Dictionary<string, IEnumerable<string>>
                    {
                        { adoTeamProject, new[] { fooRepo, barRepo } }
                    }
                }
            };

            var pipelines = new Dictionary<string, IDictionary<string, IDictionary<string, IEnumerable<string>>>>
            {
                {
                    adoOrg,
                    new Dictionary<string, IDictionary<string, IEnumerable<string>>>
                    {
                        {
                            adoTeamProject,
                            new Dictionary<string, IEnumerable<string>>
                            {
                                { fooRepo, new[] { fooPipeline } }, { barRepo, new[] { barPipeline } }
                            }
                        }
                    }
                }
            };

            var appIds = new Dictionary<string, string> { { adoOrg, appId } };

            var expected = new StringBuilder();
            expected.AppendLine("#!/usr/bin/pwsh");
            expected.AppendLine(@"
function Exec {
    param (
        [scriptblock]$ScriptBlock
    )
    & @ScriptBlock
    if ($lastexitcode -ne 0) {
        exit $lastexitcode
    }
}");
            expected.AppendLine(@"
function ExecAndGetMigrationID {
    param (
        [scriptblock]$ScriptBlock
    )
    $MigrationID = Exec $ScriptBlock | ForEach-Object {
        Write-Host $_
        $_
    } | Select-String -Pattern ""\(ID: (.+)\)"" | ForEach-Object { $_.matches.groups[1] }
    return $MigrationID
}");
            expected.AppendLine(@"
function ExecBatch {
    param (
        [scriptblock[]]$ScriptBlocks
    )
    $Global:LastBatchFailures = 0
    foreach ($ScriptBlock in $ScriptBlocks)
    {
        & @ScriptBlock
        if ($lastexitcode -ne 0) {
            $Global:LastBatchFailures++
        }
    }
}");
            expected.AppendLine();
            expected.AppendLine("$Succeeded = 0");
            expected.AppendLine("$Failed = 0");
            expected.AppendLine("$RepoMigrations = [ordered]@{}");
            expected.AppendLine();
            expected.AppendLine($"# =========== Queueing migration for Organization: {adoOrg} ===========");
            expected.AppendLine();
            expected.AppendLine($"# === Queueing repo migrations for Team Project: {adoOrg}/{adoTeamProject} ===");
            expected.AppendLine($"Exec {{ ./ado2gh create-team --github-org \"{githubOrg}\" --team-name \"{adoTeamProject}-Maintainers\" --idp-group \"{adoTeamProject}-Maintainers\" }}");
            expected.AppendLine($"Exec {{ ./ado2gh create-team --github-org \"{githubOrg}\" --team-name \"{adoTeamProject}-Admins\" --idp-group \"{adoTeamProject}-Admins\" }}");
            expected.AppendLine($"Exec {{ ./ado2gh share-service-connection --ado-org \"{adoOrg}\" --ado-team-project \"{adoTeamProject}\" --service-connection-id \"{appId}\" }}");
            expected.AppendLine();
            expected.AppendLine($"Exec {{ ./ado2gh lock-ado-repo --ado-org \"{adoOrg}\" --ado-team-project \"{adoTeamProject}\" --ado-repo \"{fooRepo}\" }}");
            expected.AppendLine($"$MigrationID = ExecAndGetMigrationID {{ ./ado2gh migrate-repo --ado-org \"{adoOrg}\" --ado-team-project \"{adoTeamProject}\" --ado-repo \"{fooRepo}\" --github-org \"{githubOrg}\" --github-repo \"{adoTeamProject}-{fooRepo}\" }}");
            expected.AppendLine($"$RepoMigrations[\"{adoOrg}/{adoTeamProject}-{fooRepo}\"] = $MigrationID");
            expected.AppendLine();
            expected.AppendLine($"Exec {{ ./ado2gh lock-ado-repo --ado-org \"{adoOrg}\" --ado-team-project \"{adoTeamProject}\" --ado-repo \"{barRepo}\" }}");
            expected.AppendLine($"$MigrationID = ExecAndGetMigrationID {{ ./ado2gh migrate-repo --ado-org \"{adoOrg}\" --ado-team-project \"{adoTeamProject}\" --ado-repo \"{barRepo}\" --github-org \"{githubOrg}\" --github-repo \"{adoTeamProject}-{barRepo}\" }}");
            expected.AppendLine($"$RepoMigrations[\"{adoOrg}/{adoTeamProject}-{barRepo}\"] = $MigrationID");
            expected.AppendLine();
            expected.AppendLine($"# =========== Waiting for all migrations to finish for Organization: {adoOrg} ===========");
            expected.AppendLine();
            expected.AppendLine($"# === Waiting for repo migration to finish for Team Project: {adoTeamProject} and Repo: {fooRepo}. Will then complete the below post migration steps. ===");
            expected.AppendLine($"./ado2gh wait-for-migration --migration-id $RepoMigrations[\"{adoOrg}/{adoTeamProject}-{fooRepo}\"]");
            expected.AppendLine("if ($lastexitcode -eq 0) {");
            expected.AppendLine("    ExecBatch @(");
            expected.AppendLine($"        {{ ./ado2gh disable-ado-repo --ado-org \"{adoOrg}\" --ado-team-project \"{adoTeamProject}\" --ado-repo \"{fooRepo}\" }}");
            expected.AppendLine($"        {{ ./ado2gh configure-autolink --github-org \"{githubOrg}\" --github-repo \"{adoTeamProject}-{fooRepo}\" --ado-org \"{adoOrg}\" --ado-team-project \"{adoTeamProject}\" }}");
            expected.AppendLine($"        {{ ./ado2gh add-team-to-repo --github-org \"{githubOrg}\" --github-repo \"{adoTeamProject}-{fooRepo}\" --team \"{adoTeamProject}-Maintainers\" --role \"maintain\" }}");
            expected.AppendLine($"        {{ ./ado2gh add-team-to-repo --github-org \"{githubOrg}\" --github-repo \"{adoTeamProject}-{fooRepo}\" --team \"{adoTeamProject}-Admins\" --role \"admin\" }}");
            expected.AppendLine($"        {{ ./ado2gh integrate-boards --ado-org \"{adoOrg}\" --ado-team-project \"{adoTeamProject}\" --github-org \"{githubOrg}\" --github-repo \"{adoTeamProject}-{fooRepo}\" }}");
            expected.AppendLine($"        {{ ./ado2gh rewire-pipeline --ado-org \"{adoOrg}\" --ado-team-project \"{adoTeamProject}\" --ado-pipeline \"{fooPipeline}\" --github-org \"{githubOrg}\" --github-repo \"{adoTeamProject}-{fooRepo}\" --service-connection-id \"{appId}\" }}");
            expected.AppendLine("    )");
            expected.AppendLine("    if ($Global:LastBatchFailures -eq 0) { $Succeeded++ }");
            expected.AppendLine("} else {");
            expected.AppendLine("    $Failed++");
            expected.AppendLine("}");
            expected.AppendLine();
            expected.AppendLine($"# === Waiting for repo migration to finish for Team Project: {adoTeamProject} and Repo: {barRepo}. Will then complete the below post migration steps. ===");
            expected.AppendLine($"./ado2gh wait-for-migration --migration-id $RepoMigrations[\"{adoOrg}/{adoTeamProject}-{barRepo}\"]");
            expected.AppendLine("if ($lastexitcode -eq 0) {");
            expected.AppendLine("    ExecBatch @(");
            expected.AppendLine($"        {{ ./ado2gh disable-ado-repo --ado-org \"{adoOrg}\" --ado-team-project \"{adoTeamProject}\" --ado-repo \"{barRepo}\" }}");
            expected.AppendLine($"        {{ ./ado2gh configure-autolink --github-org \"{githubOrg}\" --github-repo \"{adoTeamProject}-{barRepo}\" --ado-org \"{adoOrg}\" --ado-team-project \"{adoTeamProject}\" }}");
            expected.AppendLine($"        {{ ./ado2gh add-team-to-repo --github-org \"{githubOrg}\" --github-repo \"{adoTeamProject}-{barRepo}\" --team \"{adoTeamProject}-Maintainers\" --role \"maintain\" }}");
            expected.AppendLine($"        {{ ./ado2gh add-team-to-repo --github-org \"{githubOrg}\" --github-repo \"{adoTeamProject}-{barRepo}\" --team \"{adoTeamProject}-Admins\" --role \"admin\" }}");
            expected.AppendLine($"        {{ ./ado2gh integrate-boards --ado-org \"{adoOrg}\" --ado-team-project \"{adoTeamProject}\" --github-org \"{githubOrg}\" --github-repo \"{adoTeamProject}-{barRepo}\" }}");
            expected.AppendLine($"        {{ ./ado2gh rewire-pipeline --ado-org \"{adoOrg}\" --ado-team-project \"{adoTeamProject}\" --ado-pipeline \"{barPipeline}\" --github-org \"{githubOrg}\" --github-repo \"{adoTeamProject}-{barRepo}\" --service-connection-id \"{appId}\" }}");
            expected.AppendLine("    )");
            expected.AppendLine("    if ($Global:LastBatchFailures -eq 0) { $Succeeded++ }");
            expected.AppendLine("} else {");
            expected.AppendLine("    $Failed++");
            expected.AppendLine("}");
            expected.AppendLine();
            expected.AppendLine("Write-Host =============== Summary ===============");
            expected.AppendLine("Write-Host Total number of successful migrations: $Succeeded");
            expected.AppendLine("Write-Host Total number of failed migrations: $Failed");
            expected.AppendLine(@"
if ($Failed -ne 0) {
    exit 1
}");
            expected.AppendLine();
            expected.AppendLine();

            // Act
            var command = new GenerateScriptCommand(new Mock<OctoLogger>().Object, null);
            var actual = command.GenerateParallelScript(repos, pipelines, appIds, githubOrg, false);

            // Assert
            actual.Should().Be(expected.ToString());
        }

        [Fact]
        public void GenerateParallelScript_Single_Repo_Repos_Only()
        {
            // Arrange
            const string adoOrg = "ADO_ORG";
            const string adoTeamProject = "ADO_TEAM_PROJECT";
            const string fooRepo = "FOO_REPO";
            const string fooPipeline = "FOO_PIPELINE";
            const string appId = "d9edf292-c6fd-4440-af2b-d08fcc9c9dd1";
            const string githubOrg = "GITHUB_ORG";

            var repos = new Dictionary<string, IDictionary<string, IEnumerable<string>>>
            {
                {
                    adoOrg,
                    new Dictionary<string, IEnumerable<string>>
                    {
                        { adoTeamProject, new[] { fooRepo } }
                    }
                }
            };

            var pipelines = new Dictionary<string, IDictionary<string, IDictionary<string, IEnumerable<string>>>>
            {
                {
                    adoOrg,
                    new Dictionary<string, IDictionary<string, IEnumerable<string>>>
                    {
                        {
                            adoTeamProject,
                            new Dictionary<string, IEnumerable<string>>
                            {
                                { fooRepo, new[] { fooPipeline } }
                            }
                        }
                    }
                }
            };

            var appIds = new Dictionary<string, string> { { adoOrg, appId } };

            var expected = new StringBuilder();
            expected.AppendLine("#!/usr/bin/pwsh");
            expected.AppendLine(@"
function Exec {
    param (
        [scriptblock]$ScriptBlock
    )
    & @ScriptBlock
    if ($lastexitcode -ne 0) {
        exit $lastexitcode
    }
}");
            expected.AppendLine(@"
function ExecAndGetMigrationID {
    param (
        [scriptblock]$ScriptBlock
    )
    $MigrationID = Exec $ScriptBlock | ForEach-Object {
        Write-Host $_
        $_
    } | Select-String -Pattern ""\(ID: (.+)\)"" | ForEach-Object { $_.matches.groups[1] }
    return $MigrationID
}");
            expected.AppendLine(@"
function ExecBatch {
    param (
        [scriptblock[]]$ScriptBlocks
    )
    $Global:LastBatchFailures = 0
    foreach ($ScriptBlock in $ScriptBlocks)
    {
        & @ScriptBlock
        if ($lastexitcode -ne 0) {
            $Global:LastBatchFailures++
        }
    }
}");
            expected.AppendLine();
            expected.AppendLine("$Succeeded = 0");
            expected.AppendLine("$Failed = 0");
            expected.AppendLine("$RepoMigrations = [ordered]@{}");
            expected.AppendLine();
            expected.AppendLine($"# =========== Queueing migration for Organization: {adoOrg} ===========");
            expected.AppendLine();
            expected.AppendLine($"# === Queueing repo migrations for Team Project: {adoOrg}/{adoTeamProject} ===");
            expected.AppendLine();
            expected.AppendLine();
            expected.AppendLine();
            expected.AppendLine();
            expected.AppendLine($"$MigrationID = ExecAndGetMigrationID {{ ./ado2gh migrate-repo --ado-org \"{adoOrg}\" --ado-team-project \"{adoTeamProject}\" --ado-repo \"{fooRepo}\" --github-org \"{githubOrg}\" --github-repo \"{adoTeamProject}-{fooRepo}\" }}");
            expected.AppendLine($"$RepoMigrations[\"{adoOrg}/{adoTeamProject}-{fooRepo}\"] = $MigrationID");
            expected.AppendLine();
            expected.AppendLine($"# =========== Waiting for all migrations to finish for Organization: {adoOrg} ===========");
            expected.AppendLine();
            expected.AppendLine($"# === Waiting for repo migration to finish for Team Project: {adoTeamProject} and Repo: {fooRepo}. Will then complete the below post migration steps. ===");
            expected.AppendLine($"./ado2gh wait-for-migration --migration-id $RepoMigrations[\"{adoOrg}/{adoTeamProject}-{fooRepo}\"]");
            expected.AppendLine("if ($lastexitcode -eq 0) {");
            expected.AppendLine("    $Succeeded++");
            expected.AppendLine("} else {");
            expected.AppendLine("    $Failed++");
            expected.AppendLine("}");
            expected.AppendLine();
            expected.AppendLine("Write-Host =============== Summary ===============");
            expected.AppendLine("Write-Host Total number of successful migrations: $Succeeded");
            expected.AppendLine("Write-Host Total number of failed migrations: $Failed");
            expected.AppendLine(@"
if ($Failed -ne 0) {
    exit 1
}");
            expected.AppendLine();
            expected.AppendLine();

            // Act
            var command = new GenerateScriptCommand(new Mock<OctoLogger>().Object, null);
            // The way reposOnly is implemented is kind of hacky, this will change when we refactor all the options in issue #21
            // for now going to leave it as is and use reflection to force the test to work
            var reposOnlyField = typeof(GenerateScriptCommand).GetField("_reposOnly", BindingFlags.Instance | BindingFlags.NonPublic);
            reposOnlyField.SetValue(command, true);

            var actual = command.GenerateParallelScript(repos, pipelines, appIds, githubOrg, false);

            // Assert
            actual.Should().Be(expected.ToString());
        }

        [Fact]
        public void GenerateParallelScript_No_Data()
        {
            // Arrange
            var command = new GenerateScriptCommand(null, null);

            // Act
            var script = command.GenerateParallelScript(null, null, null, null, false);

            // Assert
            script.Should().BeEmpty();
        }

        [Fact]
        public void GenerateParallelScript_Single_Repo_Skip_Idp()
        {
            // Arrange
            const string adoOrg = "ADO_ORG";
            const string adoTeamProject = "ADO_TEAM_PROJECT";
            const string fooRepo = "FOO_REPO";
            const string fooPipeline = "FOO_PIPELINE";
            const string appId = "d9edf292-c6fd-4440-af2b-d08fcc9c9dd1";
            const string githubOrg = "GITHUB_ORG";

            var repos = new Dictionary<string, IDictionary<string, IEnumerable<string>>>
            {
                {
                    adoOrg,
                    new Dictionary<string, IEnumerable<string>>
                    {
                        { adoTeamProject, new[] { fooRepo } }
                    }
                }
            };

            var pipelines = new Dictionary<string, IDictionary<string, IDictionary<string, IEnumerable<string>>>>
            {
                {
                    adoOrg,
                    new Dictionary<string, IDictionary<string, IEnumerable<string>>>
                    {
                        {
                            adoTeamProject,
                            new Dictionary<string, IEnumerable<string>>
                            {
                                { fooRepo, new[] { fooPipeline } }
                            }
                        }
                    }
                }
            };

            var appIds = new Dictionary<string, string> { { adoOrg, appId } };

            var expected = new StringBuilder();
            expected.AppendLine("#!/usr/bin/pwsh");
            expected.AppendLine(@"
function Exec {
    param (
        [scriptblock]$ScriptBlock
    )
    & @ScriptBlock
    if ($lastexitcode -ne 0) {
        exit $lastexitcode
    }
}");
            expected.AppendLine(@"
function ExecAndGetMigrationID {
    param (
        [scriptblock]$ScriptBlock
    )
    $MigrationID = Exec $ScriptBlock | ForEach-Object {
        Write-Host $_
        $_
    } | Select-String -Pattern ""\(ID: (.+)\)"" | ForEach-Object { $_.matches.groups[1] }
    return $MigrationID
}");
            expected.AppendLine(@"
function ExecBatch {
    param (
        [scriptblock[]]$ScriptBlocks
    )
    $Global:LastBatchFailures = 0
    foreach ($ScriptBlock in $ScriptBlocks)
    {
        & @ScriptBlock
        if ($lastexitcode -ne 0) {
            $Global:LastBatchFailures++
        }
    }
}");
            expected.AppendLine();
            expected.AppendLine("$Succeeded = 0");
            expected.AppendLine("$Failed = 0");
            expected.AppendLine("$RepoMigrations = [ordered]@{}");
            expected.AppendLine();
            expected.AppendLine($"# =========== Queueing migration for Organization: {adoOrg} ===========");
            expected.AppendLine();
            expected.AppendLine($"# === Queueing repo migrations for Team Project: {adoOrg}/{adoTeamProject} ===");
            expected.AppendLine($"Exec {{ ./ado2gh create-team --github-org \"{githubOrg}\" --team-name \"{adoTeamProject}-Maintainers\" }}");
            expected.AppendLine($"Exec {{ ./ado2gh create-team --github-org \"{githubOrg}\" --team-name \"{adoTeamProject}-Admins\" }}");
            expected.AppendLine($"Exec {{ ./ado2gh share-service-connection --ado-org \"{adoOrg}\" --ado-team-project \"{adoTeamProject}\" --service-connection-id \"{appId}\" }}");
            expected.AppendLine();
            expected.AppendLine($"Exec {{ ./ado2gh lock-ado-repo --ado-org \"{adoOrg}\" --ado-team-project \"{adoTeamProject}\" --ado-repo \"{fooRepo}\" }}");
            expected.AppendLine($"$MigrationID = ExecAndGetMigrationID {{ ./ado2gh migrate-repo --ado-org \"{adoOrg}\" --ado-team-project \"{adoTeamProject}\" --ado-repo \"{fooRepo}\" --github-org \"{githubOrg}\" --github-repo \"{adoTeamProject}-{fooRepo}\" }}");
            expected.AppendLine($"$RepoMigrations[\"{adoOrg}/{adoTeamProject}-{fooRepo}\"] = $MigrationID");
            expected.AppendLine();
            expected.AppendLine($"# =========== Waiting for all migrations to finish for Organization: {adoOrg} ===========");
            expected.AppendLine();
            expected.AppendLine($"# === Waiting for repo migration to finish for Team Project: {adoTeamProject} and Repo: {fooRepo}. Will then complete the below post migration steps. ===");
            expected.AppendLine($"./ado2gh wait-for-migration --migration-id $RepoMigrations[\"{adoOrg}/{adoTeamProject}-{fooRepo}\"]");
            expected.AppendLine("if ($lastexitcode -eq 0) {");
            expected.AppendLine("    ExecBatch @(");
            expected.AppendLine($"        {{ ./ado2gh disable-ado-repo --ado-org \"{adoOrg}\" --ado-team-project \"{adoTeamProject}\" --ado-repo \"{fooRepo}\" }}");
            expected.AppendLine($"        {{ ./ado2gh configure-autolink --github-org \"{githubOrg}\" --github-repo \"{adoTeamProject}-{fooRepo}\" --ado-org \"{adoOrg}\" --ado-team-project \"{adoTeamProject}\" }}");
            expected.AppendLine($"        {{ ./ado2gh add-team-to-repo --github-org \"{githubOrg}\" --github-repo \"{adoTeamProject}-{fooRepo}\" --team \"{adoTeamProject}-Maintainers\" --role \"maintain\" }}");
            expected.AppendLine($"        {{ ./ado2gh add-team-to-repo --github-org \"{githubOrg}\" --github-repo \"{adoTeamProject}-{fooRepo}\" --team \"{adoTeamProject}-Admins\" --role \"admin\" }}");
            expected.AppendLine($"        {{ ./ado2gh integrate-boards --ado-org \"{adoOrg}\" --ado-team-project \"{adoTeamProject}\" --github-org \"{githubOrg}\" --github-repo \"{adoTeamProject}-{fooRepo}\" }}");
            expected.AppendLine($"        {{ ./ado2gh rewire-pipeline --ado-org \"{adoOrg}\" --ado-team-project \"{adoTeamProject}\" --ado-pipeline \"{fooPipeline}\" --github-org \"{githubOrg}\" --github-repo \"{adoTeamProject}-{fooRepo}\" --service-connection-id \"{appId}\" }}");
            expected.AppendLine("    )");
            expected.AppendLine("    if ($Global:LastBatchFailures -eq 0) { $Succeeded++ }");
            expected.AppendLine("} else {");
            expected.AppendLine("    $Failed++");
            expected.AppendLine("}");
            expected.AppendLine();
            expected.AppendLine("Write-Host =============== Summary ===============");
            expected.AppendLine("Write-Host Total number of successful migrations: $Succeeded");
            expected.AppendLine("Write-Host Total number of failed migrations: $Failed");
            expected.AppendLine(@"
if ($Failed -ne 0) {
    exit 1
}");
            expected.AppendLine();
            expected.AppendLine();

            // Act
            var command = new GenerateScriptCommand(new Mock<OctoLogger>().Object, null);
            var actual = command.GenerateParallelScript(repos, pipelines, appIds, githubOrg, true);

            // Assert
            actual.Should().Be(expected.ToString());
        }

        [Fact]
        public void GenerateParallelScript_Single_Repo_No_Service_Connection()
        {
            // Arrange
            const string adoOrg = "ADO_ORG";
            const string adoTeamProject = "ADO_TEAM_PROJECT";
            const string fooRepo = "FOO_REPO";
            const string fooPipeline = "FOO_PIPELINE";
            const string barPipeline = "BAR_PIPELINE";
            const string githubOrg = "GITHUB_ORG";

            var repos = new Dictionary<string, IDictionary<string, IEnumerable<string>>>
            {
                {
                    adoOrg,
                    new Dictionary<string, IEnumerable<string>>
                    {
                        { adoTeamProject, new[] { fooRepo } }
                    }
                }
            };

            var pipelines = new Dictionary<string, IDictionary<string, IDictionary<string, IEnumerable<string>>>>
            {
                {
                    adoOrg,
                    new Dictionary<string, IDictionary<string, IEnumerable<string>>>
                    {
                        {
                            adoTeamProject,
                            new Dictionary<string, IEnumerable<string>>
                            {
                                { fooRepo, new[] { fooPipeline, barPipeline } }
                            }
                        }
                    }
                }
            };

            var appIds = new Dictionary<string, string>();

            var expected = new StringBuilder();
            expected.AppendLine("#!/usr/bin/pwsh");
            expected.AppendLine(@"
function Exec {
    param (
        [scriptblock]$ScriptBlock
    )
    & @ScriptBlock
    if ($lastexitcode -ne 0) {
        exit $lastexitcode
    }
}");
            expected.AppendLine(@"
function ExecAndGetMigrationID {
    param (
        [scriptblock]$ScriptBlock
    )
    $MigrationID = Exec $ScriptBlock | ForEach-Object {
        Write-Host $_
        $_
    } | Select-String -Pattern ""\(ID: (.+)\)"" | ForEach-Object { $_.matches.groups[1] }
    return $MigrationID
}");
            expected.AppendLine(@"
function ExecBatch {
    param (
        [scriptblock[]]$ScriptBlocks
    )
    $Global:LastBatchFailures = 0
    foreach ($ScriptBlock in $ScriptBlocks)
    {
        & @ScriptBlock
        if ($lastexitcode -ne 0) {
            $Global:LastBatchFailures++
        }
    }
}");
            expected.AppendLine();
            expected.AppendLine("$Succeeded = 0");
            expected.AppendLine("$Failed = 0");
            expected.AppendLine("$RepoMigrations = [ordered]@{}");
            expected.AppendLine();
            expected.AppendLine($"# =========== Queueing migration for Organization: {adoOrg} ===========");
            expected.AppendLine();
            expected.AppendLine("# No GitHub App in this org, skipping the re-wiring of Azure Pipelines to GitHub repos");
            expected.AppendLine();
            expected.AppendLine($"# === Queueing repo migrations for Team Project: {adoOrg}/{adoTeamProject} ===");
            expected.AppendLine($"Exec {{ ./ado2gh create-team --github-org \"{githubOrg}\" --team-name \"{adoTeamProject}-Maintainers\" --idp-group \"{adoTeamProject}-Maintainers\" }}");
            expected.AppendLine($"Exec {{ ./ado2gh create-team --github-org \"{githubOrg}\" --team-name \"{adoTeamProject}-Admins\" --idp-group \"{adoTeamProject}-Admins\" }}");
            expected.AppendLine();
            expected.AppendLine($"Exec {{ ./ado2gh lock-ado-repo --ado-org \"{adoOrg}\" --ado-team-project \"{adoTeamProject}\" --ado-repo \"{fooRepo}\" }}");
            expected.AppendLine($"$MigrationID = ExecAndGetMigrationID {{ ./ado2gh migrate-repo --ado-org \"{adoOrg}\" --ado-team-project \"{adoTeamProject}\" --ado-repo \"{fooRepo}\" --github-org \"{githubOrg}\" --github-repo \"{adoTeamProject}-{fooRepo}\" }}");
            expected.AppendLine($"$RepoMigrations[\"{adoOrg}/{adoTeamProject}-{fooRepo}\"] = $MigrationID");
            expected.AppendLine();
            expected.AppendLine($"# =========== Waiting for all migrations to finish for Organization: {adoOrg} ===========");
            expected.AppendLine();
            expected.AppendLine($"# === Waiting for repo migration to finish for Team Project: {adoTeamProject} and Repo: {fooRepo}. Will then complete the below post migration steps. ===");
            expected.AppendLine($"./ado2gh wait-for-migration --migration-id $RepoMigrations[\"{adoOrg}/{adoTeamProject}-{fooRepo}\"]");
            expected.AppendLine("if ($lastexitcode -eq 0) {");
            expected.AppendLine("    ExecBatch @(");
            expected.AppendLine($"        {{ ./ado2gh disable-ado-repo --ado-org \"{adoOrg}\" --ado-team-project \"{adoTeamProject}\" --ado-repo \"{fooRepo}\" }}");
            expected.AppendLine($"        {{ ./ado2gh configure-autolink --github-org \"{githubOrg}\" --github-repo \"{adoTeamProject}-{fooRepo}\" --ado-org \"{adoOrg}\" --ado-team-project \"{adoTeamProject}\" }}");
            expected.AppendLine($"        {{ ./ado2gh add-team-to-repo --github-org \"{githubOrg}\" --github-repo \"{adoTeamProject}-{fooRepo}\" --team \"{adoTeamProject}-Maintainers\" --role \"maintain\" }}");
            expected.AppendLine($"        {{ ./ado2gh add-team-to-repo --github-org \"{githubOrg}\" --github-repo \"{adoTeamProject}-{fooRepo}\" --team \"{adoTeamProject}-Admins\" --role \"admin\" }}");
            expected.AppendLine($"        {{ ./ado2gh integrate-boards --ado-org \"{adoOrg}\" --ado-team-project \"{adoTeamProject}\" --github-org \"{githubOrg}\" --github-repo \"{adoTeamProject}-{fooRepo}\" }}");
            expected.AppendLine("    )");
            expected.AppendLine("    if ($Global:LastBatchFailures -eq 0) { $Succeeded++ }");
            expected.AppendLine("} else {");
            expected.AppendLine("    $Failed++");
            expected.AppendLine("}");
            expected.AppendLine();
            expected.AppendLine("Write-Host =============== Summary ===============");
            expected.AppendLine("Write-Host Total number of successful migrations: $Succeeded");
            expected.AppendLine("Write-Host Total number of failed migrations: $Failed");
            expected.AppendLine(@"
if ($Failed -ne 0) {
    exit 1
}");
            expected.AppendLine();
            expected.AppendLine();

            // Act
            var command = new GenerateScriptCommand(new Mock<OctoLogger>().Object, null);
            var actual = command.GenerateParallelScript(repos, pipelines, appIds, githubOrg, false);

            // Assert
            actual.Should().Be(expected.ToString());
        }

        [Fact]
        public async Task It_Uses_The_Ado_Pat_When_Provided()
        {
            // Arrange
            const string adoPat = "ado-pat";

            var mockAdoApi = new Mock<AdoApi>(null);
            var mockAdoApiFactory = new Mock<AdoApiFactory>(null, null, null);
            mockAdoApiFactory.Setup(m => m.Create(adoPat)).Returns(mockAdoApi.Object);

            // Act
            var command = new GenerateScriptCommand(new Mock<OctoLogger>().Object, mockAdoApiFactory.Object);
            await command.Invoke("githubOrg", "adoOrg", null, false, false, adoPat: adoPat);

            // Assert
            mockAdoApiFactory.Verify(m => m.Create(adoPat));
        }

        private string TrimNonExecutableLines(string script)
        {
            var lines = script.Split(new string[] { Environment.NewLine, "\n" }, StringSplitOptions.RemoveEmptyEntries).AsEnumerable();

            lines = lines.Where(x => !string.IsNullOrWhiteSpace(x)).Where(x => !x.Trim().StartsWith("#"));
            // This skips the Exec function definition
            lines = lines.Skip(9);

            return string.Join(Environment.NewLine, lines);
        }
    }
}
