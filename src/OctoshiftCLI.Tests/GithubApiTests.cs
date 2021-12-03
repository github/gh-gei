using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestPlatform.CommunicationUtilities.ObjectModel;
using Moq;
using Xunit;

namespace OctoshiftCLI.Tests
{
    public class GithubApiTests
    {
        [Fact]
        public async Task CreateMigrationSourceReturnsNewMigrationSourceId()
        {
            const string url = "https://api.github.com/graphql";
            const string orgId = "ORG_ID";
            const string adoToken = "ADO_TOKEN";
            const string githubPat = "GITHUB_PAT";
            const string payload = 
                "{\"query\":\"mutation createMigrationSource($name: String!, $url: String!, $ownerId: ID!, $accessToken: String!, $type: MigrationSourceType!, $githubPat: String!) " +
                "{ createMigrationSource(input: {name: $name, url: $url, ownerId: $ownerId, accessToken: $accessToken, type: $type, githubPat: $githubPat}) { migrationSource { id, name, url, type } } }\"" +
                ",\"variables\":{\"name\":\"Azure DevOps Source\",\"url\":\"https://dev.azure.com\",\"ownerId\":\"ORG_ID\",\"type\":\"AZURE_DEVOPS\",\"accessToken\":\"ADO_TOKEN\", \"githubPat\":\"GITHUB_PAT\"},\"operationName\":\"createMigrationSource\"}";
            using var body = new StringContent(payload, Encoding.UTF8, "application/json");
            const string result = "{\"data\":{\"createMigrationSource\":{\"migrationSource\": {\"id\":\"MS_kgC4NjFhOTVjOTc4ZTRhZjEwMDA5NjNhOTdm\",\"name\":\"Azure Devops Source\",\"url\":\"https://dev.azure.com\",\"type\":\"AZURE_DEVOPS\"}}}}";
            
            var githubClientMock = new Mock<GithubClient>(null, null);
            githubClientMock
                .Setup(m => m.PostAsync(url, It.Is<StringContent>(x => x.ReadAsStringAsync().Result == payload)))
                .ReturnsAsync(result);

            using var githubApi = new GithubApi(githubClientMock.Object);
            var id = await githubApi.CreateMigrationSource(orgId, adoToken, githubPat);
            
            githubClientMock.Verify(m => m.PostAsync(url, It.IsAny<StringContent>()));
            Assert.Equal("MS_kgC4NjFhOTVjOTc4ZTRhZjEwMDA5NjNhOTdm", id);
        }
    }
}