using FluentAssertions;
using Xunit;

namespace OctoshiftCLI.Tests
{
    public class InsufficientPermissionsMessageGeneratorTest
    {
        [Fact]
        public void Generate_Returns_Message_With_Interpolated_Login()
        {
            InsufficientPermissionsMessageGenerator.Generate("monalisa-corp").Should().Be(". Please check that (a) you are a member of the `monalisa-corp` organization, (b) you are an organization owner or you have been granted the migrator role and (c) your personal access token has the correct scopes. For more information, see https://docs.github.com/en/migrations/using-github-enterprise-importer/preparing-to-migrate-with-github-enterprise-importer/managing-access-for-github-enterprise-importer.");
        }
    }
}
