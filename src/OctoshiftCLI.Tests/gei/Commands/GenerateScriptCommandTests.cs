using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Moq;
using OctoshiftCLI.GithubEnterpriseImporter.Commands;
using Xunit;

namespace OctoshiftCLI.Tests.GithubEnterpriseImporter.Commands
{
    public class GenerateScriptCommandTests
    {
        [Fact]
        public void Should_Have_Options()
        {
            var command = new GenerateScriptCommand(null, null, null);

            command.Should().NotBeNull();
            command.Name.Should().Be("generate-script");
            command.Options.Count.Should().Be(7);

            TestHelpers.VerifyCommandOption(command.Options, "github-source-org", false);
            TestHelpers.VerifyCommandOption(command.Options, "ado-source-org", false);
            TestHelpers.VerifyCommandOption(command.Options, "ado-team-project", false);
            TestHelpers.VerifyCommandOption(command.Options, "github-target-org", true);
            TestHelpers.VerifyCommandOption(command.Options, "output", false);
            TestHelpers.VerifyCommandOption(command.Options, "ssh", false);
            TestHelpers.VerifyCommandOption(command.Options, "verbose", false);
        }

        [Fact]
        public void Github_No_Data()
        {
            var command = new GenerateScriptCommand(null, null, null);
            var script = command.GenerateGithubScript(null, "foo-source", "foo-target", false);

            string.IsNullOrWhiteSpace(script).Should().BeTrue();
        }

        [Fact]
        public void Github_Single_Repo()
        {
            var githubSourceOrg = "foo-source";
            var githubTargetOrg = "foo-target";
            var repo = "foo-repo";

            var repos = new List<string>() { repo };

            var command = new GenerateScriptCommand(new Mock<OctoLogger>().Object, null, null);
            var script = command.GenerateGithubScript(repos, githubSourceOrg, githubTargetOrg, false);

            script = TrimNonExecutableLines(script);

            var expected = $"Exec {{ gh gei migrate-repo --github-source-org \"{githubSourceOrg}\" --source-repo \"{repo}\" --github-target-org \"{githubTargetOrg}\" --target-repo \"{repo}\" }}";

            script.Should().Be(expected);
        }

        [Fact]
        public void Github_Multiple_Repos()
        {
            var githubSourceOrg = "foo-source";
            var githubTargetOrg = "foo-target";
            var repo1 = "foo-repo-1";
            var repo2 = "foo-repo-2";
            var repo3 = "foo-repo-3";

            var repos = new List<string>() { repo1, repo2, repo3 };

            var command = new GenerateScriptCommand(new Mock<OctoLogger>().Object, null, null);
            var script = command.GenerateGithubScript(repos, githubSourceOrg, githubTargetOrg, false);

            script = TrimNonExecutableLines(script);

            var expected = $"Exec {{ gh gei migrate-repo --github-source-org \"{githubSourceOrg}\" --source-repo \"{repo1}\" --github-target-org \"{githubTargetOrg}\" --target-repo \"{repo1}\" }}";
            expected += Environment.NewLine;
            expected += $"Exec {{ gh gei migrate-repo --github-source-org \"{githubSourceOrg}\" --source-repo \"{repo2}\" --github-target-org \"{githubTargetOrg}\" --target-repo \"{repo2}\" }}";
            expected += Environment.NewLine;
            expected += $"Exec {{ gh gei migrate-repo --github-source-org \"{githubSourceOrg}\" --source-repo \"{repo3}\" --github-target-org \"{githubTargetOrg}\" --target-repo \"{repo3}\" }}";

            script.Should().Be(expected);
        }

        [Fact]
        public async Task GetRepos_Two_Repos_Two_Team_Projects()
        {
            var org = "foo-org";
            var teamProject = string.Empty;
            var teamProject1 = "foo-tp1";
            var teamProject2 = "foo-tp2";
            var teamProjects = new List<string>() { teamProject1, teamProject2 };
            var repo1 = "foo-repo1";
            var repo2 = "foo-repo2";

            var mockAdo = new Mock<AdoApi>(null);

            mockAdo.Setup(x => x.GetTeamProjects(org).Result).Returns(teamProjects);
            mockAdo.Setup(x => x.GetRepos(org, teamProject1).Result).Returns(new List<string>() { repo1 });
            mockAdo.Setup(x => x.GetRepos(org, teamProject2).Result).Returns(new List<string>() { repo2 });

            var command = new GenerateScriptCommand(new Mock<OctoLogger>().Object, null, null);
            var result = await command.GetAdoRepos(mockAdo.Object, org, teamProject);

            Assert.Single(result[teamProject1]);
            Assert.Single(result[teamProject2]);
            Assert.Contains(result[teamProject1], x => x == repo1);
            Assert.Contains(result[teamProject2], x => x == repo2);
        }

        [Fact]
        public async Task GetRepos_Two_Repos_Two_Team_Projects_With_Team_Project_Supplied()
        {
            var org = "foo-org";
            var orgs = new List<string>() { org };
            var teamProject1 = "foo-tp1";
            var teamProject2 = "foo-tp2";
            var teamProjectArg = teamProject1;
            var teamProjects = new List<string>() { teamProject1, teamProject2 };
            var repo1 = "foo-repo1";
            var repo2 = "foo-repo2";

            var mockAdo = new Mock<AdoApi>(null);

            mockAdo.Setup(x => x.GetTeamProjects(org).Result).Returns(teamProjects);
            mockAdo.Setup(x => x.GetRepos(org, teamProject1).Result).Returns(new List<string>() { repo1 });
            mockAdo.Setup(x => x.GetRepos(org, teamProject2).Result).Returns(new List<string>() { repo2 });

            var command = new GenerateScriptCommand(new Mock<OctoLogger>().Object, null, null);
            var result = await command.GetAdoRepos(mockAdo.Object, org, teamProjectArg);

            Assert.Single(result[teamProjectArg]);
            Assert.False(result.ContainsKey(teamProject2));
            Assert.Contains(result[teamProjectArg], x => x == repo1);
        }

        [Fact]
        public async Task GetRepos_With_Team_Project_Supplied_Does_Not_Exist()
        {
            var org = "foo-org";
            var teamProject1 = "foo-tp1";
            var teamProject2 = "foo-tp2";
            var teamProjectArg = "foo-tp3";
            var teamProjects = new List<string>() { teamProject1, teamProject2 };
            var repo1 = "foo-repo1";
            var repo2 = "foo-repo2";

            var mockAdo = new Mock<AdoApi>(null);

            mockAdo.Setup(x => x.GetTeamProjects(org).Result).Returns(teamProjects);
            mockAdo.Setup(x => x.GetRepos(org, teamProject1).Result).Returns(new List<string>() { repo1 });
            mockAdo.Setup(x => x.GetRepos(org, teamProject2).Result).Returns(new List<string>() { repo2 });

            var command = new GenerateScriptCommand(new Mock<OctoLogger>().Object, null, null);
            var result = await command.GetAdoRepos(mockAdo.Object, org, teamProjectArg);

            Assert.Empty(result);
        }

        [Fact]
        public void Github_With_Ssh()
        {
            var githubSourceOrg = "foo-source";
            var githubTargetOrg = "foo-target";
            var repo = "foo-repo";

            var repos = new List<string>() { repo };

            var command = new GenerateScriptCommand(new Mock<OctoLogger>().Object, null, null);
            var script = command.GenerateGithubScript(repos, githubSourceOrg, githubTargetOrg, true);

            script = TrimNonExecutableLines(script);

            var expected = $"Exec {{ gh gei migrate-repo --github-source-org \"{githubSourceOrg}\" --source-repo \"{repo}\" --github-target-org \"{githubTargetOrg}\" --target-repo \"{repo}\" --ssh }}";

            script.Should().Be(expected);
        }

        [Fact]
        public void Ado_No_Data()
        {
            var command = new GenerateScriptCommand(null, null, null);
            var script = command.GenerateAdoScript(null, "foo-source", "foo-target", false);

            string.IsNullOrWhiteSpace(script).Should().BeTrue();
        }

        [Fact]
        public void Ado_Single_Repo()
        {
            var adoSourceOrg = "foo-source";
            var adoTeamProject = "foo-team-project";
            var githubTargetOrg = "foo-target";
            var repo = "foo-repo";

            var repos = new Dictionary<string, IEnumerable<string>>() { { adoTeamProject, new List<string>() { repo } } };

            var command = new GenerateScriptCommand(new Mock<OctoLogger>().Object, null, null);
            var script = command.GenerateAdoScript(repos, adoSourceOrg, githubTargetOrg, false);

            script = TrimNonExecutableLines(script);

            var expected = $"Exec {{ gh gei migrate-repo --ado-source-org \"{adoSourceOrg}\" --ado-team-project \"{adoTeamProject}\" --source-repo \"{repo}\" --github-target-org \"{githubTargetOrg}\" --target-repo \"{adoTeamProject}-{repo}\" }}";

            script.Should().Be(expected);
        }

        [Fact]
        public void Ado_Multiple_Repos()
        {
            var adoSourceOrg = "foo-source";
            var adoTeamProject = "foo-team-project";
            var githubTargetOrg = "foo-target";
            var repo1 = "foo-repo-1";
            var repo2 = "foo-repo-2";
            var repo3 = "foo-repo-3";

            var repos = new Dictionary<string, IEnumerable<string>> { { adoTeamProject, new List<string>() { repo1, repo2, repo3 } } };

            var command = new GenerateScriptCommand(new Mock<OctoLogger>().Object, null, null);
            var script = command.GenerateAdoScript(repos, adoSourceOrg, githubTargetOrg, false);

            script = TrimNonExecutableLines(script);

            var expected = $"Exec {{ gh gei migrate-repo --ado-source-org \"{adoSourceOrg}\" --ado-team-project \"{adoTeamProject}\" --source-repo \"{repo1}\" --github-target-org \"{githubTargetOrg}\" --target-repo \"{adoTeamProject}-{repo1}\" }}";
            expected += Environment.NewLine;
            expected += $"Exec {{ gh gei migrate-repo --ado-source-org \"{adoSourceOrg}\" --ado-team-project \"{adoTeamProject}\" --source-repo \"{repo2}\" --github-target-org \"{githubTargetOrg}\" --target-repo \"{adoTeamProject}-{repo2}\" }}";
            expected += Environment.NewLine;
            expected += $"Exec {{ gh gei migrate-repo --ado-source-org \"{adoSourceOrg}\" --ado-team-project \"{adoTeamProject}\" --source-repo \"{repo3}\" --github-target-org \"{githubTargetOrg}\" --target-repo \"{adoTeamProject}-{repo3}\" }}";

            script.Should().Be(expected);
        }

        [Fact]
        public void Ado_With_Ssh()
        {
            var adoSourceOrg = "foo-source";
            var adoTeamProject = "foo-team-project";
            var githubTargetOrg = "foo-target";
            var repo = "foo-repo";

            var repos = new Dictionary<string, IEnumerable<string>>() { { adoTeamProject, new List<string>() { repo } } };

            var command = new GenerateScriptCommand(new Mock<OctoLogger>().Object, null, null);
            var script = command.GenerateAdoScript(repos, adoSourceOrg, githubTargetOrg, true);

            script = TrimNonExecutableLines(script);

            var expected = $"Exec {{ gh gei migrate-repo --ado-source-org \"{adoSourceOrg}\" --ado-team-project \"{adoTeamProject}\" --source-repo \"{repo}\" --github-target-org \"{githubTargetOrg}\" --target-repo \"{adoTeamProject}-{repo}\" --ssh }}";

            script.Should().Be(expected);
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
