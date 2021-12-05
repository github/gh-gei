using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Moq;
using OctoshiftCLI.Commands;
using Xunit;

namespace OctoshiftCLI.Tests.Commands
{
    public class GenerateScriptCommandTests
    {
        [Fact]
        public void ShouldHaveOptions()
        {
            var command = new GenerateScriptCommand(null, null);
            Assert.NotNull(command);
            Assert.Equal("generate-script", command.Name);
            Assert.Equal(6, command.Options.Count);

            TestHelpers.VerifyCommandOption(command.Options, "github-org", true);
            TestHelpers.VerifyCommandOption(command.Options, "ado-org", false);
            TestHelpers.VerifyCommandOption(command.Options, "output", false);
            TestHelpers.VerifyCommandOption(command.Options, "repos-only", false);
            TestHelpers.VerifyCommandOption(command.Options, "skip-idp", false);
            TestHelpers.VerifyCommandOption(command.Options, "verbose", false);
        }

        [Fact]
        public void NoData()
        {
            var githubOrg = "foo-gh-org";

            var command = new GenerateScriptCommand(null, null);
            var script = command.GenerateScript(null, null, null, githubOrg, false);

            Assert.True(string.IsNullOrWhiteSpace(script));
        }

        [Fact]
        public void SingleRepo()
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
            var script = command.GenerateScript(repos, null, null, githubOrg, false);

            script = TrimNonExecutableLines(script);

            var expected = $"./octoshift create-team --github-org \"{githubOrg}\" --team-name \"{adoTeamProject}-Maintainers\" --idp-group \"{adoTeamProject}-Maintainers\"";
            expected += Environment.NewLine;
            expected += $"./octoshift create-team --github-org \"{githubOrg}\" --team-name \"{adoTeamProject}-Admins\" --idp-group \"{adoTeamProject}-Admins\"";
            expected += Environment.NewLine;
            expected += $"./octoshift lock-ado-repo --ado-org \"{adoOrg}\" --ado-team-project \"{adoTeamProject}\" --ado-repo \"{repo}\"";
            expected += Environment.NewLine;
            expected += $"./octoshift migrate-repo --ado-org \"{adoOrg}\" --ado-team-project \"{adoTeamProject}\" --ado-repo \"{repo}\" --github-org \"{githubOrg}\" --github-repo \"{adoTeamProject}-{repo}\"";
            expected += Environment.NewLine;
            expected += $"./octoshift disable-ado-repo --ado-org \"{adoOrg}\" --ado-team-project \"{adoTeamProject}\" --ado-repo \"{repo}\"";
            expected += Environment.NewLine;
            expected += $"./octoshift configure-autolink --github-org \"{githubOrg}\" --github-repo \"{adoTeamProject}-{repo}\" --ado-org \"{adoOrg}\" --ado-team-project \"{adoTeamProject}\"";
            expected += Environment.NewLine;
            expected += $"./octoshift add-team-to-repo --github-org \"{githubOrg}\" --github-repo \"{adoTeamProject}-{repo}\" --team \"{adoTeamProject}-Maintainers\" --role \"maintain\"";
            expected += Environment.NewLine;
            expected += $"./octoshift add-team-to-repo --github-org \"{githubOrg}\" --github-repo \"{adoTeamProject}-{repo}\" --team \"{adoTeamProject}-Admins\" --role \"admin\"";
            expected += Environment.NewLine;
            expected += $"./octoshift integrate-boards --ado-org \"{adoOrg}\" --ado-team-project \"{adoTeamProject}\" --github-org \"{githubOrg}\" --github-repo \"{adoTeamProject}-{repo}\"";

            Assert.Equal(expected, script);
        }

        [Fact]
        public void SkipTeamProjectWithNoRepos()
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
            var script = command.GenerateScript(repos, null, null, githubOrg, false);

            script = TrimNonExecutableLines(script);

            Assert.Equal(string.Empty, script);
        }

        [Fact]
        public void SingleRepoTwoPipelines()
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
            var script = command.GenerateScript(repos, pipelines, appIds, githubOrg, false);

            script = TrimNonExecutableLines(script);

            var expected = $"./octoshift create-team --github-org \"{githubOrg}\" --team-name \"{adoTeamProject}-Maintainers\" --idp-group \"{adoTeamProject}-Maintainers\"";
            expected += Environment.NewLine;
            expected += $"./octoshift create-team --github-org \"{githubOrg}\" --team-name \"{adoTeamProject}-Admins\" --idp-group \"{adoTeamProject}-Admins\"";
            expected += Environment.NewLine;
            expected += $"./octoshift share-service-connection --ado-org \"{adoOrg}\" --ado-team-project \"{adoTeamProject}\" --service-connection-id \"{appId}\"";
            expected += Environment.NewLine;
            expected += $"./octoshift lock-ado-repo --ado-org \"{adoOrg}\" --ado-team-project \"{adoTeamProject}\" --ado-repo \"{repo}\"";
            expected += Environment.NewLine;
            expected += $"./octoshift migrate-repo --ado-org \"{adoOrg}\" --ado-team-project \"{adoTeamProject}\" --ado-repo \"{repo}\" --github-org \"{githubOrg}\" --github-repo \"{adoTeamProject}-{repo}\"";
            expected += Environment.NewLine;
            expected += $"./octoshift disable-ado-repo --ado-org \"{adoOrg}\" --ado-team-project \"{adoTeamProject}\" --ado-repo \"{repo}\"";
            expected += Environment.NewLine;
            expected += $"./octoshift configure-autolink --github-org \"{githubOrg}\" --github-repo \"{adoTeamProject}-{repo}\" --ado-org \"{adoOrg}\" --ado-team-project \"{adoTeamProject}\"";
            expected += Environment.NewLine;
            expected += $"./octoshift add-team-to-repo --github-org \"{githubOrg}\" --github-repo \"{adoTeamProject}-{repo}\" --team \"{adoTeamProject}-Maintainers\" --role \"maintain\"";
            expected += Environment.NewLine;
            expected += $"./octoshift add-team-to-repo --github-org \"{githubOrg}\" --github-repo \"{adoTeamProject}-{repo}\" --team \"{adoTeamProject}-Admins\" --role \"admin\"";
            expected += Environment.NewLine;
            expected += $"./octoshift integrate-boards --ado-org \"{adoOrg}\" --ado-team-project \"{adoTeamProject}\" --github-org \"{githubOrg}\" --github-repo \"{adoTeamProject}-{repo}\"";
            expected += Environment.NewLine;
            expected += $"./octoshift rewire-pipeline --ado-org \"{adoOrg}\" --ado-team-project \"{adoTeamProject}\" --ado-pipeline \"{pipelineOne}\" --github-org \"{githubOrg}\" --github-repo \"{adoTeamProject}-{repo}\" --service-connection-id \"{appId}\"";
            expected += Environment.NewLine;
            expected += $"./octoshift rewire-pipeline --ado-org \"{adoOrg}\" --ado-team-project \"{adoTeamProject}\" --ado-pipeline \"{pipelineTwo}\" --github-org \"{githubOrg}\" --github-repo \"{adoTeamProject}-{repo}\" --service-connection-id \"{appId}\"";

            Assert.Equal(expected, script);
        }

        [Fact]
        public void SingleRepoTwoPipelinesNoServiceConnection()
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
            var script = command.GenerateScript(repos, pipelines, appIds, githubOrg, false);

            script = TrimNonExecutableLines(script);

            var expected = $"./octoshift create-team --github-org \"{githubOrg}\" --team-name \"{adoTeamProject}-Maintainers\" --idp-group \"{adoTeamProject}-Maintainers\"";
            expected += Environment.NewLine;
            expected += $"./octoshift create-team --github-org \"{githubOrg}\" --team-name \"{adoTeamProject}-Admins\" --idp-group \"{adoTeamProject}-Admins\"";
            expected += Environment.NewLine;
            expected += $"./octoshift lock-ado-repo --ado-org \"{adoOrg}\" --ado-team-project \"{adoTeamProject}\" --ado-repo \"{repo}\"";
            expected += Environment.NewLine;
            expected += $"./octoshift migrate-repo --ado-org \"{adoOrg}\" --ado-team-project \"{adoTeamProject}\" --ado-repo \"{repo}\" --github-org \"{githubOrg}\" --github-repo \"{adoTeamProject}-{repo}\"";
            expected += Environment.NewLine;
            expected += $"./octoshift disable-ado-repo --ado-org \"{adoOrg}\" --ado-team-project \"{adoTeamProject}\" --ado-repo \"{repo}\"";
            expected += Environment.NewLine;
            expected += $"./octoshift configure-autolink --github-org \"{githubOrg}\" --github-repo \"{adoTeamProject}-{repo}\" --ado-org \"{adoOrg}\" --ado-team-project \"{adoTeamProject}\"";
            expected += Environment.NewLine;
            expected += $"./octoshift add-team-to-repo --github-org \"{githubOrg}\" --github-repo \"{adoTeamProject}-{repo}\" --team \"{adoTeamProject}-Maintainers\" --role \"maintain\"";
            expected += Environment.NewLine;
            expected += $"./octoshift add-team-to-repo --github-org \"{githubOrg}\" --github-repo \"{adoTeamProject}-{repo}\" --team \"{adoTeamProject}-Admins\" --role \"admin\"";
            expected += Environment.NewLine;
            expected += $"./octoshift integrate-boards --ado-org \"{adoOrg}\" --ado-team-project \"{adoTeamProject}\" --github-org \"{githubOrg}\" --github-repo \"{adoTeamProject}-{repo}\"";

            Assert.Equal(expected, script);
        }

        [Fact]
        public void SingleRepoReposOnly()
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

            var script = command.GenerateScript(repos, null, null, githubOrg, false);

            script = TrimNonExecutableLines(script);

            var expected = $"./octoshift migrate-repo --ado-org \"{adoOrg}\" --ado-team-project \"{adoTeamProject}\" --ado-repo \"{repo}\" --github-org \"{githubOrg}\" --github-repo \"{adoTeamProject}-{repo}\"";

            Assert.Equal(expected, script);
        }

        [Fact]
        public void SingleRepoSkipIdp()
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
            var script = command.GenerateScript(repos, null, null, githubOrg, true);

            script = TrimNonExecutableLines(script);

            var expected = $"./octoshift create-team --github-org \"{githubOrg}\" --team-name \"{adoTeamProject}-Maintainers\"";
            expected += Environment.NewLine;
            expected += $"./octoshift create-team --github-org \"{githubOrg}\" --team-name \"{adoTeamProject}-Admins\"";
            expected += Environment.NewLine;
            expected += $"./octoshift lock-ado-repo --ado-org \"{adoOrg}\" --ado-team-project \"{adoTeamProject}\" --ado-repo \"{repo}\"";
            expected += Environment.NewLine;
            expected += $"./octoshift migrate-repo --ado-org \"{adoOrg}\" --ado-team-project \"{adoTeamProject}\" --ado-repo \"{repo}\" --github-org \"{githubOrg}\" --github-repo \"{adoTeamProject}-{repo}\"";
            expected += Environment.NewLine;
            expected += $"./octoshift disable-ado-repo --ado-org \"{adoOrg}\" --ado-team-project \"{adoTeamProject}\" --ado-repo \"{repo}\"";
            expected += Environment.NewLine;
            expected += $"./octoshift configure-autolink --github-org \"{githubOrg}\" --github-repo \"{adoTeamProject}-{repo}\" --ado-org \"{adoOrg}\" --ado-team-project \"{adoTeamProject}\"";
            expected += Environment.NewLine;
            expected += $"./octoshift add-team-to-repo --github-org \"{githubOrg}\" --github-repo \"{adoTeamProject}-{repo}\" --team \"{adoTeamProject}-Maintainers\" --role \"maintain\"";
            expected += Environment.NewLine;
            expected += $"./octoshift add-team-to-repo --github-org \"{githubOrg}\" --github-repo \"{adoTeamProject}-{repo}\" --team \"{adoTeamProject}-Admins\" --role \"admin\"";
            expected += Environment.NewLine;
            expected += $"./octoshift integrate-boards --ado-org \"{adoOrg}\" --ado-team-project \"{adoTeamProject}\" --github-org \"{githubOrg}\" --github-repo \"{adoTeamProject}-{repo}\"";

            Assert.Equal(expected, script);
        }

        [Fact]
        public async Task GetOrgsAllOrgs()
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
        public async Task GetOrgsOrgProvided()
        {
            var org1 = "foo-1";

            var command = new GenerateScriptCommand(new Mock<OctoLogger>().Object, null);
            var result = await command.GetOrgs(null, org1);

            Assert.Single(result);
            Assert.Contains(result, x => x == org1);
        }

        [Fact]
        public async Task GetReposTwoReposTwoTeamProjects()
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
            mockAdo.Setup(x => x.GetRepos(org, teamProject1).Result).Returns(new List<string>() { repo1 });
            mockAdo.Setup(x => x.GetRepos(org, teamProject2).Result).Returns(new List<string>() { repo2 });

            var command = new GenerateScriptCommand(new Mock<OctoLogger>().Object, null);
            var result = await command.GetRepos(mockAdo.Object, orgs);

            Assert.Single(result[org][teamProject1]);
            Assert.Single(result[org][teamProject2]);
            Assert.Contains(result[org][teamProject1], x => x == repo1);
            Assert.Contains(result[org][teamProject2], x => x == repo2);
        }

        [Fact]
        public async Task GetPipelinesOneRepoTwoPipelines()
        {
            var org = "foo-org";
            var orgs = new List<string>() { org };
            var teamProject = "foo-tp";
            var teamProjects = new List<string>() { teamProject };
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

        private string TrimNonExecutableLines(string script)
        {
            var lines = script.Split(new string[] { Environment.NewLine, "\n" }, StringSplitOptions.RemoveEmptyEntries).AsEnumerable();

            lines = lines.Where(x => !string.IsNullOrWhiteSpace(x)).Where(x => !x.Trim().StartsWith("#"));

            return string.Join(Environment.NewLine, lines);
        }
    }
}