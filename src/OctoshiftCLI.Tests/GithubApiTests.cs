using System.Net.Http;
using System.Threading.Tasks;
using Moq;
using Xunit;

namespace OctoshiftCLI.Tests
{
    public class GithubApiTests
    {
        [Fact]
        public async Task CreateMigrationSourceReturnsNewMigrationSourceId()
        {
            // Arrange
            const string url = "https://api.github.com/graphql";
            const string orgId = "ORG_ID";
            const string adoToken = "ADO_TOKEN";
            const string githubPat = "GITHUB_PAT";
            var payload =
                "{\"query\":\"mutation createMigrationSource($name: String!, $url: String!, $ownerId: ID!, $accessToken: String!, $type: MigrationSourceType!, $githubPat: String!) " +
                "{ createMigrationSource(input: {name: $name, url: $url, ownerId: $ownerId, accessToken: $accessToken, type: $type, githubPat: $githubPat}) { migrationSource { id, name, url, type } } }\"" +
                $",\"variables\":{{\"name\":\"Azure DevOps Source\",\"url\":\"https://dev.azure.com\",\"ownerId\":\"{orgId}\",\"type\":\"AZURE_DEVOPS\",\"accessToken\":\"{adoToken}\", \"githubPat\":\"{githubPat}\"}},\"operationName\":\"createMigrationSource\"}}";
            const string migrationSourceId = "MS_kgC4NjFhOTVjOTc4ZTRhZjEwMDA5NjNhOTdm";
            const string result = $"{{\"data\":{{\"createMigrationSource\":{{\"migrationSource\": {{\"id\":\"{migrationSourceId}\",\"name\":\"Azure Devops Source\",\"url\":\"https://dev.azure.com\",\"type\":\"AZURE_DEVOPS\"}}}}}}}}";

            var githubClientMock = new Mock<GithubClient>(null, null);
            githubClientMock
                .Setup(m => m.PostAsync(url, It.Is<StringContent>(x => x.ReadAsStringAsync().Result == payload)))
                .ReturnsAsync(result);

            // Act
            using var githubApi = new GithubApi(githubClientMock.Object);
            var id = await githubApi.CreateMigrationSource(orgId, adoToken, githubPat);

            // Assert
            Assert.Equal(migrationSourceId, id);
        }
    }
}