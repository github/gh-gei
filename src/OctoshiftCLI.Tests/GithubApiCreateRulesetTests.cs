using System.Linq;
using System.Threading.Tasks;
using System.Collections.Generic;
using FluentAssertions;
using Moq;
using Newtonsoft.Json.Linq;
using OctoshiftCLI.Models;
using OctoshiftCLI.Services;
using Xunit;

namespace OctoshiftCLI.Tests;

public class GithubApiCreateRulesetTests
{
    [Fact]
    public async Task CreateRepoRuleset_Sends_Expected_Payload()
    {
        string capturedUrl = null;
        object capturedPayload = null;
        var client = new Mock<GithubClient>(null, null, null, null, null, "pat");
        client.Setup(c => c.PostAsync(It.IsAny<string>(), It.IsAny<object>(), It.IsAny<Dictionary<string,string>>()))
            .Callback<string, object, Dictionary<string,string>>((u, p, _) => { capturedUrl = u; capturedPayload = p; })
            .ReturnsAsync("{ \"id\": 987 } ");
        var api = new GithubApi(client.Object, "https://api.github.com", null, null);
        var def = new GithubRulesetDefinition
        {
            Name = "ado-default-branch-policies",
            TargetPatterns = new[] { "main" },
            RequiredApprovingReviewCount = 2,
            RequiredStatusChecks = new[] { "build", "test" }
        };

        var id = await api.CreateRepoRuleset("org", "repo", def);

        id.Should().Be(987);
        capturedUrl.Should().Contain("/repos/org/repo/rulesets");
        var json = JObject.FromObject(capturedPayload);
        ((string)json["name"]).Should().Be(def.Name);
        ((JArray)json["target"]["conditions"]["ref_name"]["includes"]).Select(x => (string)x).Should().Contain("main");
        ((JArray)json["rules"]).Count.Should().Be(2);
    }
}
