using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Moq;
using Newtonsoft.Json.Linq;
using OctoshiftCLI.Services;
using Xunit;

namespace OctoshiftCLI.Tests;

public class GithubApiRulesetTests
{
    [Fact]
    public async Task GetRepoRulesets_Returns_Empty_When_None()
    {
        var client = new Mock<GithubClient>(null, null, null, null, null, "pat");
        client.Setup(c => c.GetAllAsync(It.Is<string>(s => s.Contains("/rulesets")), null))
              .Returns(AsyncEnumerable.Empty<JToken>());
        var api = new GithubApi(client.Object, "https://api.github.com", null, null);

        var result = await api.GetRepoRulesets("org", "repo");
        result.Should().BeEmpty();
    }
}
