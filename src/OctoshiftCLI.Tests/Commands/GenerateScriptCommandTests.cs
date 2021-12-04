using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
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

            var repos = new Dictionary<string, Dictionary<string, IEnumerable<string>>>
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
        public void SingleRepoTwoPipelines()
        {
            var githubOrg = "foo-gh-org";
            var adoOrg = "foo-ado-org";
            var adoTeamProject = "foo-team-project";
            var repo = "foo-repo";
            var pipelineOne = "CICD";
            var pipelineTwo = "Publish";
            var appId = Guid.NewGuid().ToString();

            var repos = new Dictionary<string, Dictionary<string, IEnumerable<string>>>
            {
                { adoOrg, new Dictionary<string, IEnumerable<string>>() }
            };

            repos[adoOrg].Add(adoTeamProject, new List<string>() { repo });

            var pipelines = new Dictionary<string, Dictionary<string, Dictionary<string, IEnumerable<string>>>>
            {
                { adoOrg, new Dictionary<string, Dictionary<string, IEnumerable<string>>>() }
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
        public void SingleRepoReposOnly()
        {
            var githubOrg = "foo-gh-org";
            var adoOrg = "foo-ado-org";
            var adoTeamProject = "foo-team-project";
            var repo = "foo-repo";

            var repos = new Dictionary<string, Dictionary<string, IEnumerable<string>>>
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

            var repos = new Dictionary<string, Dictionary<string, IEnumerable<string>>>
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

        private string TrimNonExecutableLines(string script)
        {
            var lines = script.Split(new string[] { Environment.NewLine, "\n" }, StringSplitOptions.RemoveEmptyEntries).AsEnumerable();

            lines = lines.Where(x => !string.IsNullOrWhiteSpace(x)).Where(x => !x.Trim().StartsWith("#"));

            return string.Join(Environment.NewLine, lines);
        }
    }
}