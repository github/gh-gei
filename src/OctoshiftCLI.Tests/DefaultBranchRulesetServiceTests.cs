using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Moq;
using OctoshiftCLI.Models;
using OctoshiftCLI.Services;
using Xunit;

namespace OctoshiftCLI.Tests;

public class DefaultBranchRulesetServiceTests
{
    [Fact]
    public async Task Apply_Creates_When_None()
    {
        var api = new Mock<GithubApi>(null, null, null, null);
        api.Setup(a => a.GetRepoRulesets("org", "repo")).ReturnsAsync(new List<(int, string, IEnumerable<string>, int?, IEnumerable<string>)>());
        api.Setup(a => a.CreateRepoRuleset("org", "repo", It.IsAny<GithubRulesetDefinition>())).ReturnsAsync(42);
        var log = new Mock<OctoLogger>();
        var svc = new DefaultBranchRulesetService(api.Object, log.Object);
        var def = new GithubRulesetDefinition { Name = "rs", TargetPatterns = new[] { "main" }, RequiredApprovingReviewCount = 2, RequiredStatusChecks = new[] { "build" } };
        var id = await svc.Apply("org", "repo", def, false);
        id.Should().Be(42);
    }

    [Fact]
    public async Task Apply_Updates_When_Diff()
    {
        var api = new Mock<GithubApi>(null, null, null, null);
        api.Setup(a => a.GetRepoRulesets("org", "repo")).ReturnsAsync(new List<(int, string, IEnumerable<string>, int?, IEnumerable<string>)> { (10, "rs", new[] { "main" }.AsEnumerable(), 1, new[] { "build" }.AsEnumerable()) });
        api.Setup(a => a.UpdateRepoRuleset("org", "repo", 10, It.IsAny<GithubRulesetDefinition>())).Returns(Task.CompletedTask);
        var log = new Mock<OctoLogger>();
        var def = new GithubRulesetDefinition { Name = "rs", TargetPatterns = new[] { "main" }, RequiredApprovingReviewCount = 2, RequiredStatusChecks = new[] { "build", "test" } };
        var svc = new DefaultBranchRulesetService(api.Object, log.Object);
        var id = await svc.Apply("org", "repo", def, false);
        id.Should().Be(10);
    }

    [Fact]
    public async Task Apply_Skips_Update_When_Equivalent()
    {
        var api = new Mock<GithubApi>(null, null, null, null);
        api.Setup(a => a.GetRepoRulesets("org", "repo")).ReturnsAsync(new List<(int, string, IEnumerable<string>, int?, IEnumerable<string>)> { (10, "rs", new[] { "main" }.AsEnumerable(), 2, new[] { "build" }.AsEnumerable()) });
        var log = new Mock<OctoLogger>();
        var def = new GithubRulesetDefinition { Name = "rs", TargetPatterns = new[] { "main" }, RequiredApprovingReviewCount = 2, RequiredStatusChecks = new[] { "build" } };
        var svc = new DefaultBranchRulesetService(api.Object, log.Object);
        var id = await svc.Apply("org", "repo", def, false);
        id.Should().Be(10);
        api.Verify(a => a.UpdateRepoRuleset(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<GithubRulesetDefinition>()), Times.Never);
    }

    [Fact]
    public async Task Apply_DryRun_Create_NoMutation()
    {
        var api = new Mock<GithubApi>(null, null, null, null);
        api.Setup(a => a.GetRepoRulesets("org", "repo")).ReturnsAsync(new List<(int, string, IEnumerable<string>, int?, IEnumerable<string>)>());
        var log = new Mock<OctoLogger>();
        var svc = new DefaultBranchRulesetService(api.Object, log.Object);
        var def = new GithubRulesetDefinition { Name = "rs", TargetPatterns = new[] { "main" }, RequiredApprovingReviewCount = 1 };
        var id = await svc.Apply("org", "repo", def, true);
        id.Should().Be(0);
        api.Verify(a => a.CreateRepoRuleset(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<GithubRulesetDefinition>()), Times.Never);
    }

    [Fact]
    public async Task Apply_DryRun_Update_NoMutation()
    {
        var api = new Mock<GithubApi>(null, null, null, null);
        api.Setup(a => a.GetRepoRulesets("org", "repo")).ReturnsAsync(new List<(int, string, IEnumerable<string>, int?, IEnumerable<string>)> { (10, "rs", new[] { "main" }.AsEnumerable(), 1, System.Array.Empty<string>()) });
        var log = new Mock<OctoLogger>();
        var svc = new DefaultBranchRulesetService(api.Object, log.Object);
        var def = new GithubRulesetDefinition { Name = "rs", TargetPatterns = new[] { "main" }, RequiredApprovingReviewCount = 2 };
        var id = await svc.Apply("org", "repo", def, true);
        id.Should().Be(10);
        api.Verify(a => a.UpdateRepoRuleset(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<GithubRulesetDefinition>()), Times.Never);
    }
}























































































































 