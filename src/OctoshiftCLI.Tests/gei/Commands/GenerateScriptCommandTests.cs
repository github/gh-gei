using System;
using System.Collections.Generic;
using System.Linq;
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
            var command = new GenerateScriptCommand(null, null);

            command.Should().NotBeNull();
            command.Name.Should().Be("generate-script");
            command.Options.Count.Should().Be(5);

            TestHelpers.VerifyCommandOption(command.Options, "github-source-org", true);
            TestHelpers.VerifyCommandOption(command.Options, "github-target-org", true);
            TestHelpers.VerifyCommandOption(command.Options, "output", false);
            TestHelpers.VerifyCommandOption(command.Options, "ssh", false);
            TestHelpers.VerifyCommandOption(command.Options, "verbose", false);
        }

        [Fact]
        public void No_Data()
        {
            var command = new GenerateScriptCommand(null, null);
            var script = command.GenerateScript(null, "foo-source", "foo-target", false);

            string.IsNullOrWhiteSpace(script).Should().BeTrue();
        }

        [Fact]
        public void Single_Repo()
        {
            var githubSourceOrg = "foo-source";
            var githubTargetOrg = "foo-target";
            var repo = "foo-repo";

            var repos = new List<string>() { repo };

            var command = new GenerateScriptCommand(new Mock<OctoLogger>().Object, null);
            var script = command.GenerateScript(repos, githubSourceOrg, githubTargetOrg, false);

            script = TrimNonExecutableLines(script);

            var expected = $"gh gei migrate-repo --github-source-org \"{githubSourceOrg}\" --source-repo \"{repo}\" --github-target-org \"{githubTargetOrg}\" --target-repo \"{repo}\"";

            script.Should().Be(expected);
        }

        [Fact]
        public void Multiple_Repos()
        {
            var githubSourceOrg = "foo-source";
            var githubTargetOrg = "foo-target";
            var repo1 = "foo-repo-1";
            var repo2 = "foo-repo-2";
            var repo3 = "foo-repo-3";

            var repos = new List<string>() { repo1, repo2, repo3 };

            var command = new GenerateScriptCommand(new Mock<OctoLogger>().Object, null);
            var script = command.GenerateScript(repos, githubSourceOrg, githubTargetOrg, false);

            script = TrimNonExecutableLines(script);

            var expected = $"gh gei migrate-repo --github-source-org \"{githubSourceOrg}\" --source-repo \"{repo1}\" --github-target-org \"{githubTargetOrg}\" --target-repo \"{repo1}\"";
            expected += Environment.NewLine;
            expected += $"gh gei migrate-repo --github-source-org \"{githubSourceOrg}\" --source-repo \"{repo2}\" --github-target-org \"{githubTargetOrg}\" --target-repo \"{repo2}\"";
            expected += Environment.NewLine;
            expected += $"gh gei migrate-repo --github-source-org \"{githubSourceOrg}\" --source-repo \"{repo3}\" --github-target-org \"{githubTargetOrg}\" --target-repo \"{repo3}\"";

            script.Should().Be(expected);
        }

        [Fact]
        public void With_Ssh()
        {
            var githubSourceOrg = "foo-source";
            var githubTargetOrg = "foo-target";
            var repo = "foo-repo";

            var repos = new List<string>() { repo };

            var command = new GenerateScriptCommand(new Mock<OctoLogger>().Object, null);
            var script = command.GenerateScript(repos, githubSourceOrg, githubTargetOrg, true);

            script = TrimNonExecutableLines(script);

            var expected = $"gh gei migrate-repo --github-source-org \"{githubSourceOrg}\" --source-repo \"{repo}\" --github-target-org \"{githubTargetOrg}\" --target-repo \"{repo}\" --ssh";

            script.Should().Be(expected);
        }

        private string TrimNonExecutableLines(string script)
        {
            var lines = script.Split(new string[] { Environment.NewLine, "\n" }, StringSplitOptions.RemoveEmptyEntries).AsEnumerable();

            lines = lines.Where(x => !string.IsNullOrWhiteSpace(x)).Where(x => !x.Trim().StartsWith("#"));

            return string.Join(Environment.NewLine, lines);
        }
    }
}
