using System;
using System.Threading.Tasks;
using Moq;
using OctoshiftCLI.AdoToGithub;
using OctoshiftCLI.AdoToGithub.Commands;
using Xunit;

namespace OctoshiftCLI.Tests.AdoToGithub.Commands
{
    public class ShareServiceConnectionCommandTests
    {
        private readonly Mock<AdoClient> _mockAdoClient = TestHelpers.CreateMock<AdoClient>();
        private readonly Mock<AdoApiFactory> _mockAdoApiFactory = TestHelpers.CreateMock<AdoApiFactory>();
        private readonly Mock<OctoLogger> _mockOctoLogger = TestHelpers.CreateMock<OctoLogger>();

        private readonly ShareServiceConnectionCommand _command;

        public ShareServiceConnectionCommandTests()
        {
            _command = new ShareServiceConnectionCommand(_mockOctoLogger.Object, _mockAdoApiFactory.Object);
        }

        [Fact]
        public void Should_Have_Options()
        {
            Assert.NotNull(_command);
            Assert.Equal("share-service-connection", _command.Name);
            Assert.Equal(5, _command.Options.Count);

            TestHelpers.VerifyCommandOption(_command.Options, "ado-org", true);
            TestHelpers.VerifyCommandOption(_command.Options, "ado-team-project", true);
            TestHelpers.VerifyCommandOption(_command.Options, "service-connection-id", true);
            TestHelpers.VerifyCommandOption(_command.Options, "ado-pat", false);
            TestHelpers.VerifyCommandOption(_command.Options, "verbose", false);
        }

        [Fact]
        public async Task Happy_Path()
        {
            var _mockAdoApi = new Mock<AdoApi>(_mockAdoClient.Object, null, null) { CallBase = true };

            var adoOrg = "FooOrg";
            var adoTeamProject = "BlahTeamProject";
            var serviceConnectionId = Guid.NewGuid().ToString();
            var teamProjectId = Guid.NewGuid().ToString();
            var serviceConnection = @$"
            {{
                ""data"": {{}},
                ""id"": ""{serviceConnectionId}"",
                ""name"": ""MyNewServiceEndpoint"",
                ""type"": ""Generic"",
                ""url"": ""https://myserver"",
                ""createdBy"": {{
                    ""displayName"": ""Chuck Reinhart"",
                    ""url"": ""https://vssps.dev.azure.com/fabrikam/_apis/Identities/e18a1f0a-b112-67fd-a9e0-e3bb081da49e"",
                    ""_links"": {{
                        ""avatar"": {{
                            ""href"": ""https://dev.azure.com/fabrikam/_apis/GraphProfile/MemberAvatars/msa.ZTE4YTFmMGEtYjExMi03N2ZkLWE5ZTAtZTNiYjA4MWRhNDll""
                        }}
                    }},
                  ""id"": ""e18a1f0a-b112-67fd-a9e0-e3bb081da49e"",
                  ""uniqueName"": ""fabfiber@outlook.com"",
                  ""imageUrl"": ""https://dev.azure.com/fabrikam/_apis/GraphProfile/MemberAvatars/msa.ZTE4YTFmMGEtYjExMi03N2ZkLWE5ZTAtZTNiYjA4MWRhNDll"",
                  ""descriptor"": """"
                }},
                ""description"": """",
                ""authorization"": {{
                    ""parameters"": {{
                        ""username"": ""myusername"",
                        ""password"": null
                    }},
                    ""scheme"": ""UsernamePassword""
                }},
                ""isShared"": false,
                ""isReady"": true,
                ""owner"": ""Library"",
                ""serviceEndpointProjectReferences"": []
              }}";

            _mockAdoClient.Setup(x => x.GetAsync(It.IsAny<string>()).Result).Returns(serviceConnection);
            _mockAdoApi.Setup(x => x.GetTeamProjectId(adoOrg, adoTeamProject).Result).Returns(teamProjectId);
            _mockAdoApiFactory.Setup(m => m.Create(null)).Returns(_mockAdoApi.Object);

            await _command.Invoke(adoOrg, adoTeamProject, serviceConnectionId);

            _mockAdoApi.Verify(x => x.ShareServiceConnection(adoOrg, adoTeamProject, teamProjectId, serviceConnectionId));
        }

        [Fact]
        public async Task It_Skips_When_Already_Shared()
        {
            var adoOrg = "FooOrg";
            var adoTeamProject = "BlahTeamProject";
            var serviceConnectionId = Guid.NewGuid().ToString();
            var teamProjectId = Guid.NewGuid().ToString();

            var _mockAdoApi = new Mock<AdoApi>(_mockAdoClient.Object, null, null) { CallBase = true };

            var serviceConnection = @$"
            {{
                ""data"": {{}},
                ""id"": ""{serviceConnectionId}"",
                ""name"": ""MyNewServiceEndpoint"",
                ""type"": ""Generic"",
                ""url"": ""https://myserver"",
                ""createdBy"": {{
                    ""displayName"": ""Chuck Reinhart"",
                    ""url"": ""https://vssps.dev.azure.com/fabrikam/_apis/Identities/e18a1f0a-b112-67fd-a9e0-e3bb081da49e"",
                    ""_links"": {{
                        ""avatar"": {{
                            ""href"": ""https://dev.azure.com/fabrikam/_apis/GraphProfile/MemberAvatars/msa.ZTE4YTFmMGEtYjExMi03N2ZkLWE5ZTAtZTNiYjA4MWRhNDll""
                        }}
                    }},
                  ""id"": ""e18a1f0a-b112-67fd-a9e0-e3bb081da49e"",
                  ""uniqueName"": ""fabfiber@outlook.com"",
                  ""imageUrl"": ""https://dev.azure.com/fabrikam/_apis/GraphProfile/MemberAvatars/msa.ZTE4YTFmMGEtYjExMi03N2ZkLWE5ZTAtZTNiYjA4MWRhNDll"",
                  ""descriptor"": """"
                }},
                ""description"": """",
                ""authorization"": {{
                    ""parameters"": {{
                        ""username"": ""myusername"",
                        ""password"": null
                    }},
                    ""scheme"": ""UsernamePassword""
                }},
                ""isShared"": false,
                ""isReady"": true,
                ""owner"": ""Library"",
                ""serviceEndpointProjectReferences"": [
                  {{
                    ""projectReference"": {{
                        ""id"": ""{teamProjectId}"",
                        ""name"": ""{adoTeamProject}""
                    }},
                    ""name"": ""MyNewServiceEndpoint""
                  }}
                ]
              }}";

            _mockAdoClient.Setup(x => x.GetAsync(It.IsAny<string>()).Result).Returns(serviceConnection);
            _mockAdoApi.Setup(x => x.GetTeamProjectId(adoOrg, adoTeamProject).Result).Returns(teamProjectId);
            //_mockAdoApi.Setup(x => x.ContainsServiceConnection(adoOrg, adoTeamProject, teamProjectId, serviceConnectionId).Result).Returns(true);
            _mockAdoApiFactory.Setup(m => m.Create(null)).Returns(_mockAdoApi.Object);

            await _command.Invoke(adoOrg, adoTeamProject, serviceConnectionId);

            _mockAdoApi.Verify(x => x.ShareServiceConnection(adoOrg, adoTeamProject, teamProjectId, serviceConnectionId), Times.Never);
        }

        [Fact]
        public async Task It_Uses_The_Ado_Pat_When_Provided()
        {
            var _mockAdoApi = TestHelpers.CreateMock<AdoApi>();

            const string adoPat = "ado-pat";

            _mockAdoApiFactory.Setup(m => m.Create(adoPat)).Returns(_mockAdoApi.Object);

            await _command.Invoke("adoOrg", "adoTeamProject", "serviceConnectionId", adoPat);

            _mockAdoApiFactory.Verify(m => m.Create(adoPat));
        }
    }
}
