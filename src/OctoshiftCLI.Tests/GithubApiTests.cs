using System.Net.Http;
using System.Threading.Tasks;
using FluentAssertions;
using Moq;
using OctoshiftCLI.Extensions;
using Xunit;

namespace OctoshiftCLI.Tests
{
    public class GithubApiTests
    {
        [Fact]
        public async Task AddAutoLink_Calls_The_Right_Endpoint_With_Payload()
        {
            // Arrange
            const string org = "ORG";
            const string repo = "REPO";
            const string adoOrg = "ADO_ORG";
            const string adoTeamProject = "ADO_TEAM_PROJECT";

            var url = $"https://api.github.com/repos/{org}/{repo}/autolinks";

            var payload = new
            {
                key_prefix = "AB#",
                url_template = $"https://dev.azure.com/{adoOrg}/{adoTeamProject}/_workitems/edit/<num>/"
            };

            var githubClientMock = new Mock<GithubClient>(null, null, null);

            // Act
            var githubApi = new GithubApi(githubClientMock.Object);
            await githubApi.AddAutoLink(org, repo, adoOrg, adoTeamProject);

            // Assert
            githubClientMock.Verify(m => m.PostAsync(url, It.Is<object>(x => x.ToJson() == payload.ToJson())));
        }

        [Fact]
        public async Task CreateTeam_Returns_Created_Team_Id()
        {
            // Arrange
            const string org = "ORG";
            const string teamName = "TEAM_NAME";

            var url = $"https://api.github.com/orgs/{org}/teams";
            var payload = new { name = teamName, privacy = "closed" };

            const string teamId = "TEAM_ID";
            var response = $"{{\"id\": \"{teamId}\"}}";

            var githubClientMock = new Mock<GithubClient>(null, null, null);
            githubClientMock
                .Setup(m => m.PostAsync(url, It.Is<object>(x => x.ToJson() == payload.ToJson())))
                .ReturnsAsync(response);

            // Act
            var githubApi = new GithubApi(githubClientMock.Object);
            var result = await githubApi.CreateTeam(org, teamName);

            // Assert
            result.Should().Be(teamId);
        }

        [Fact]
        public async Task GetTeamMembers_Returns_Team_Members()
        {
            // Arrange
            const string org = "ORG";
            const string teamName = "TEAM_NAME";

            var url = $"https://api.github.com/orgs/{org}/teams/{teamName}/members";
            const string teamMember1 = "TEAM_MEMBER_1";
            const string teamMember2 = "TEAM_MEMBER_2";
            var response = $@"
            [
                {{
                    ""login"": ""{teamMember1}"",
                    ""id"": 1
                }},
                {{
                    ""login"": ""{teamMember2}"", 
                    ""id"": 2
                }}
            ]";

            var githubClientMock = new Mock<GithubClient>(null, null, null);
            githubClientMock
                .Setup(m => m.GetAsync(url))
                .ReturnsAsync(response);

            // Act
            var githubApi = new GithubApi(githubClientMock.Object);
            var result = await githubApi.GetTeamMembers(org, teamName);

            // Assert
            result.Should().Equal(teamMember1, teamMember2);
        }

        [Fact]
        public async Task RemoveTeamMember_Calls_The_Right_Endpoint()
        {
            // Arrange
            const string org = "ORG";
            const string teamName = "TEAM_NAME";
            const string member = "MEMBER";

            var url = $"https://api.github.com/orgs/{org}/teams/{teamName}/memberships/{member}";

            var githubClientMock = new Mock<GithubClient>(null, null, null);
            githubClientMock.Setup(m => m.DeleteAsync(url));

            // Act
            var githubApi = new GithubApi(githubClientMock.Object);
            await githubApi.RemoveTeamMember(org, teamName, member);

            // Assert
            githubClientMock.Verify(m => m.DeleteAsync(url));
        }

        [Fact]
        public async Task GetIdpGroup_Returns_Id_Name_And_Description_Of_Idp_Group()
        {
            // Arrange
            const string org = "ORG";
            const string idpGroupName = "IDP_GROUP_NAME";
            const string groupId = "123";
            const string groupDescription = "GROUP_DESCRIPTION";

            var url = $"https://api.github.com/orgs/{org}/team-sync/groups";
            var response = $@"
            {{
                ""groups"": [
                    {{
                        ""group_id"": ""{groupId}"",
                        ""group_name"": ""{idpGroupName}"", 
                        ""group_description"": ""{groupDescription}""
                    }},
                    {{
                        ""group_id"": ""123"",
                        ""group_name"": ""Octocat admins"",
                        ""group_description"": ""The people who configure your octoworld.""
                    }}
                ]
            }}";

            var githubClientMock = new Mock<GithubClient>(null, null, null);
            githubClientMock
                .Setup(m => m.GetAsync(url))
                .ReturnsAsync(response);

            // Act
            var githubApi = new GithubApi(githubClientMock.Object);
            var (id, name, description) = await githubApi.GetIdpGroup(org, idpGroupName);

            // Assert
            id.Should().Be(groupId);
            name.Should().Be(idpGroupName);
            description.Should().Be(groupDescription);
        }

        [Fact]
        public async Task AddTeamSync_Calls_The_Right_Endpoint_With_Payload()
        {
            // Arrange
            const string org = "ORG";
            const string teamName = "TEAM_NAME";
            const string groupId = "GROUP_ID";
            const string groupName = "GROUP_NAME";
            const string groupDesc = "GROUP_DESC";

            var url = $"https://api.github.com/orgs/{org}/teams/{teamName}/team-sync/group-mappings";
            var payload = new
            {
                groups = new[] { new { group_id = groupId, group_name = groupName, group_description = groupDesc } }
            };

            var githubClientMock = new Mock<GithubClient>(null, null, null);

            // Act
            var githubApi = new GithubApi(githubClientMock.Object);
            await githubApi.AddTeamSync(org, teamName, groupId, groupName, groupDesc);

            // Assert
            githubClientMock.Verify(m => m.PatchAsync(url, It.Is<object>(x => x.ToJson() == payload.ToJson())));
        }

        [Fact]
        public async Task AddTeamToRepo_Calls_The_Right_Endpoint_With_Payload()
        {
            // Arrange
            const string org = "ORG";
            const string repo = "REPO";
            const string teamName = "TEAM_NAME";
            const string role = "ROLE";

            var url = $"https://api.github.com/orgs/{org}/teams/{teamName}/repos/{org}/{repo}";
            var payload = new { permission = role };

            var githubClientMock = new Mock<GithubClient>(null, null, null);;

            // Act
            var githubApi = new GithubApi(githubClientMock.Object);
            await githubApi.AddTeamToRepo(org, repo, teamName, role);

            // Assert
            githubClientMock.Verify(m => m.PutAsync(url, It.Is<object>(x => x.ToJson() == payload.ToJson())));
        }

        [Fact]
        public async Task GetOrganizationId_Returns_The_Org_Id()
        {
            // Arrange
            const string org = "ORG";
            const string orgId = "ORG_ID";

            var url = $"https://api.github.com/graphql";
            var payload =
                $"{{\"query\":\"query($login: String!) {{organization(login: $login) {{ login, id, name }} }}\",\"variables\":{{\"login\":\"{org}\"}}}}";
            var response = $@"
            {{
                ""data"": 
                    {{
                        ""organization"": 
                            {{
                                ""login"": ""{org}"",
                                ""id"": ""{orgId}"",
                                ""name"": ""github"" 
                            }} 
                    }} 
            }}";

            var githubClientMock = new Mock<GithubClient>(null, null, null);
            githubClientMock
                .Setup(m => m.PostAsync(url, It.Is<object>(x => x.ToJson() == payload)))
                .ReturnsAsync(response);

            // Act
            var githubApi = new GithubApi(githubClientMock.Object);
            var result = await githubApi.GetOrganizationId(org);

            // Assert
            result.Should().Be(orgId);
        }

        [Fact]
        public async Task CreateAdoMigrationSource_Returns_New_Migration_Source_Id()
        {
            // Arrange
            const string url = "https://api.github.com/graphql";
            const string orgId = "ORG_ID";
            const string adoToken = "ADO_TOKEN";
            const string githubPat = "GITHUB_PAT";
            var payload =
                "{\"query\":\"mutation createMigrationSource($name: String!, $url: String!, $ownerId: ID!, $accessToken: String!, $type: MigrationSourceType!, $githubPat: String!) " +
                "{ createMigrationSource(input: {name: $name, url: $url, ownerId: $ownerId, accessToken: $accessToken, type: $type, githubPat: $githubPat}) { migrationSource { id, name, url, type } } }\"" +
                $",\"variables\":{{\"name\":\"Azure DevOps Source\",\"url\":\"https://dev.azure.com\",\"ownerId\":\"{orgId}\",\"type\":\"AZURE_DEVOPS\",\"accessToken\":\"{adoToken}\",\"githubPat\":\"{githubPat}\"}},\"operationName\":\"createMigrationSource\"}}";
            const string actualMigrationSourceId = "MS_kgC4NjFhOTVjOTc4ZTRhZjEwMDA5NjNhOTdm";
            var response = $@"
            {{
                ""data"": {{
                    ""createMigrationSource"": {{
                        ""migrationSource"": {{
                            ""id"": ""{actualMigrationSourceId}"",
                            ""name"": ""Azure Devops Source"",
                            ""url"": ""https://dev.azure.com"",
                            ""type"": ""AZURE_DEVOPS""
                        }}
                    }}
                }}
            }}";

            var githubClientMock = new Mock<GithubClient>(null, null, null);
            githubClientMock
                .Setup(m => m.PostAsync(url, It.Is<object>(x => x.ToJson() == payload)))
                .ReturnsAsync(response);

            // Act
            var githubApi = new GithubApi(githubClientMock.Object);
            var expectedMigrationSourceId = await githubApi.CreateAdoMigrationSource(orgId, adoToken, githubPat);

            // Assert
            expectedMigrationSourceId.Should().Be(actualMigrationSourceId);
        }

        [Fact]
        public async Task CreateAdoMigrationSource_Using_Ssh()
        {
            // Arrange
            const string url = "https://api.github.com/graphql";
            const string orgId = "ORG_ID";
            const string adoToken = "ADO_TOKEN";
            const string githubPat = "GITHUB_PAT";
            var payload =
                "{\"query\":\"mutation createMigrationSource($name: String!, $url: String!, $ownerId: ID!, $accessToken: String!, $type: MigrationSourceType!, $githubPat: String!) " +
                "{ createMigrationSource(input: {name: $name, url: $url, ownerId: $ownerId, accessToken: $accessToken, type: $type, githubPat: $githubPat}) { migrationSource { id, name, url, type } } }\"" +
                $",\"variables\":{{\"name\":\"Azure DevOps Source\",\"url\":\"https://dev.azure.com\",\"ownerId\":\"{orgId}\",\"type\":\"AZURE_DEVOPS\",\"accessToken\":\"{adoToken}\",\"githubPat\":null}},\"operationName\":\"createMigrationSource\"}}";
            const string actualMigrationSourceId = "MS_kgC4NjFhOTVjOTc4ZTRhZjEwMDA5NjNhOTdm";
            var response = $@"
            {{
                ""data"": {{
                    ""createMigrationSource"": {{
                        ""migrationSource"": {{
                            ""id"": ""{actualMigrationSourceId}"",
                            ""name"": ""Azure Devops Source"",
                            ""url"": ""https://dev.azure.com"",
                            ""type"": ""AZURE_DEVOPS""
                        }}
                    }}
                }}
            }}";

            var githubClientMock = new Mock<GithubClient>(null, null, null);;
            githubClientMock
                .Setup(m => m.PostAsync(url, It.Is<object>(x => x.ToJson() == payload)))
                .ReturnsAsync(response);

            // Act
            var githubApi = new GithubApi(githubClientMock.Object);
            var expectedMigrationSourceId = await githubApi.CreateAdoMigrationSource(orgId, adoToken, githubPat, true);

            // Assert
            expectedMigrationSourceId.Should().Be(actualMigrationSourceId);
        }

        [Fact]
        public async Task CreateGhecMigrationSource_Returns_New_Migration_Source_Id()
        {
            // Arrange
            const string url = "https://api.github.com/graphql";
            const string orgId = "ORG_ID";
            const string sourceGithubPat = "SOURCE_GITHUB_PAT";
            const string targetGithubPat = "TARGET_GITHUB_PAT";
            var payload =
                "{\"query\":\"mutation createMigrationSource($name: String!, $url: String!, $ownerId: ID!, $accessToken: String!, $type: MigrationSourceType!, $githubPat: String!) " +
                "{ createMigrationSource(input: {name: $name, url: $url, ownerId: $ownerId, accessToken: $accessToken, type: $type, githubPat: $githubPat}) { migrationSource { id, name, url, type } } }\"" +
                $",\"variables\":{{\"name\":\"GHEC Source\",\"url\":\"https://github.com\",\"ownerId\":\"{orgId}\",\"type\":\"GITHUB_ARCHIVE\",\"accessToken\":\"{sourceGithubPat}\",\"githubPat\":\"{targetGithubPat}\"}},\"operationName\":\"createMigrationSource\"}}";
            const string actualMigrationSourceId = "MS_kgC4NjFhOTVjOTc4ZTRhZjEwMDA5NjNhOTdm";
            var response = $@"
            {{
                ""data"": {{
                    ""createMigrationSource"": {{
                        ""migrationSource"": {{
                            ""id"": ""{actualMigrationSourceId}"",
                            ""name"": ""GHEC Source"",
                            ""url"": ""https://github.com"",
                            ""type"": ""GITHUB_ARCHIVE""
                        }}
                    }}
                }}
            }}";

            var githubClientMock = new Mock<GithubClient>(null, null, null);;
            githubClientMock
                .Setup(m => m.PostAsync(url, It.Is<object>(x => x.ToJson() == payload)))
                .ReturnsAsync(response);

            // Act
            var githubApi = new GithubApi(githubClientMock.Object);
            var expectedMigrationSourceId = await githubApi.CreateGhecMigrationSource(orgId, sourceGithubPat, targetGithubPat);

            // Assert
            expectedMigrationSourceId.Should().Be(actualMigrationSourceId);
        }

        [Fact]
        public async Task CreateGhecMigrationSource_Using_Ssh()
        {
            // Arrange
            const string url = "https://api.github.com/graphql";
            const string orgId = "ORG_ID";
            const string sourceGithubPat = "SOURCE_GITHUB_PAT";
            const string targetGithubPat = "target_GITHUB_PAT";
            var payload =
                "{\"query\":\"mutation createMigrationSource($name: String!, $url: String!, $ownerId: ID!, $accessToken: String!, $type: MigrationSourceType!, $githubPat: String!) " +
                "{ createMigrationSource(input: {name: $name, url: $url, ownerId: $ownerId, accessToken: $accessToken, type: $type, githubPat: $githubPat}) { migrationSource { id, name, url, type } } }\"" +
                $",\"variables\":{{\"name\":\"GHEC Source\",\"url\":\"https://github.com\",\"ownerId\":\"{orgId}\",\"type\":\"GITHUB_ARCHIVE\",\"accessToken\":\"{sourceGithubPat}\",\"githubPat\":null}},\"operationName\":\"createMigrationSource\"}}";
            const string actualMigrationSourceId = "MS_kgC4NjFhOTVjOTc4ZTRhZjEwMDA5NjNhOTdm";
            var response = $@"
            {{
                ""data"": {{
                    ""createMigrationSource"": {{
                        ""migrationSource"": {{
                            ""id"": ""{actualMigrationSourceId}"",
                            ""name"": ""GHEC Source"",
                            ""url"": ""https://github.com"",
                            ""type"": ""GITHUB_ARCHIVE""
                        }}
                    }}
                }}
            }}";

            var githubClientMock = new Mock<GithubClient>(null, null, null);;
            githubClientMock
                .Setup(m => m.PostAsync(url, It.Is<object>(x => x.ToJson() == payload)))
                .ReturnsAsync(response);

            // Act
            var githubApi = new GithubApi(githubClientMock.Object);
            var expectedMigrationSourceId = await githubApi.CreateGhecMigrationSource(orgId, sourceGithubPat, targetGithubPat, true);

            // Assert
            expectedMigrationSourceId.Should().Be(actualMigrationSourceId);
        }

        [Fact]
        public async Task StartMigration_Returns_New_Repository_Migration_Id()
        {
            // Arrange
            const string migrationSourceId = "MIGRATION_SOURCE_ID";
            const string adoRepoUrl = "ADO_REPO_URL";
            const string orgId = "ORG_ID";
            const string repo = "REPO";
            const string url = "https://api.github.com/graphql";

            var payload =
                "{\"query\":\"mutation startRepositoryMigration($sourceId: ID!, $ownerId: ID!, $sourceRepositoryUrl: URI!, $repositoryName: String!, $continueOnError: Boolean!) " +
                "{ startRepositoryMigration(input: { sourceId: $sourceId, ownerId: $ownerId, sourceRepositoryUrl: $sourceRepositoryUrl, repositoryName: $repositoryName, continueOnError: $continueOnError }) " +
                "{ repositoryMigration { id, migrationSource { id, name, type }, sourceUrl, state, failureReason } } }\"" +
                $",\"variables\":{{\"sourceId\":\"{migrationSourceId}\",\"ownerId\":\"{orgId}\",\"sourceRepositoryUrl\":\"{adoRepoUrl}\",\"repositoryName\":\"{repo}\",\"continueOnError\":true}}," +
                "\"operationName\":\"startRepositoryMigration\"}";
            const string actualRepositoryMigrationId = "RM_kgC4NjFhNmE2NGU2ZWE1YTQwMDA5ODliZjhi";
            var response = $@"
            {{
                ""data"": {{
                    ""startRepositoryMigration"": {{
                        ""repositoryMigration"": {{
                            ""id"": ""{actualRepositoryMigrationId}"",
                            ""migrationSource"": {{
                                ""id"": ""MS_kgC4NjFhNmE2NDViNWZmOTEwMDA5MTZiMGQw"",
                                ""name"": ""Azure Devops Source"",
                                ""type"": ""AZURE_DEVOPS""
                            }},
                        ""sourceUrl"": ""https://dev.azure.com/github-inside-msft/Team-Demos/_git/Tiny"",
                        ""state"": ""QUEUED"",
                        ""failureReason"": """"
                        }}
                    }}
                }}
            }}";

            var githubClientMock = new Mock<GithubClient>(null, null, null);;
            githubClientMock
                .Setup(m => m.PostAsync(url, It.Is<object>(x => x.ToJson() == payload)))
                .ReturnsAsync(response);

            // Act
            var githubApi = new GithubApi(githubClientMock.Object);
            var expectedRepositoryMigrationId =
                await githubApi.StartMigration(migrationSourceId, adoRepoUrl, orgId, repo);

            // Assert
            expectedRepositoryMigrationId.Should().Be(actualRepositoryMigrationId);
        }

        [Fact]
        public async Task GetMigrationState_Returns_The_Migration_State()
        {
            // Arrange
            const string migrationId = "MIGRATION_ID";
            const string url = "https://api.github.com/graphql";

            var payload =
                "{\"query\":\"query($id: ID!) { node(id: $id) { ... on Migration { id, sourceUrl, migrationSource { name }, state, failureReason } } }\"" +
                $",\"variables\":{{\"id\":\"{migrationId}\"}}}}";
            const string actualMigrationState = "SUCCEEDED";
            var response = $@"
            {{
                ""data"": {{
                    ""node"": {{
                        ""id"": ""RM_kgC4NjFhNmE2ZWY1NmE4MjAwMDA4NjA5NTZi"",
                        ""sourceUrl"": ""https://github.com/import-testing/archive-export-testing"",
                        ""migrationSource"": {{
                            ""name"": ""GHEC Archive Source""
                        }},
                        ""state"": ""{actualMigrationState}"",
                        ""failureReason"": """"
                    }}
                }}
            }}";

            var githubClientMock = new Mock<GithubClient>(null, null, null);;
            githubClientMock
                .Setup(m => m.PostAsync(url, It.Is<object>(x => x.ToJson() == payload)))
                .ReturnsAsync(response);

            // Act
            var githubApi = new GithubApi(githubClientMock.Object);
            var expectedMigrationState = await githubApi.GetMigrationState(migrationId);

            // Assert
            expectedMigrationState.Should().Be(actualMigrationState);
        }

        [Fact]
        public async Task GetMigrationFailureReason_Returns_The_Migration_Failure_Reason()
        {
            // Arrange
            const string migrationId = "MIGRATION_ID";
            const string url = "https://api.github.com/graphql";

            var payload =
                "{\"query\":\"query($id: ID!) { node(id: $id) { ... on Migration { id, sourceUrl, migrationSource { name }, state, failureReason } } }\"" +
                $",\"variables\":{{\"id\":\"{migrationId}\"}}}}";
            const string actualFailureReason = "FAILURE_REASON";
            var response = $@"
            {{
                ""data"": {{
                    ""node"": {{
                        ""id"": ""RM_kgC4NjFhNmE2ZWY1NmE4MjAwMDA4NjA5NTZi"",
                        ""sourceUrl"": ""https://github.com/import-testing/archive-export-testing"",
                        ""migrationSource"": {{
                            ""name"": ""GHEC Archive Source""
                        }},
                        ""state"": ""FAILED"",
                        ""failureReason"": ""{actualFailureReason}""
                    }}
                }}
            }}";

            var githubClientMock = new Mock<GithubClient>(null, null, null);;
            githubClientMock
                .Setup(m => m.PostAsync(url, It.Is<object>(x => x.ToJson() == payload)))
                .ReturnsAsync(response);

            // Act
            var githubApi = new GithubApi(githubClientMock.Object);
            var expectedFailureReason = await githubApi.GetMigrationFailureReason(migrationId);

            // Assert
            expectedFailureReason.Should().Be(actualFailureReason);
        }

        [Fact]
        public async Task GetIdpGroupId_Returns_The_Idp_Group_Id()
        {
            // Arrange
            const string org = "ORG";
            const string groupName = "GROUP_NAME";

            var url = $"https://api.github.com/orgs/{org}/external-groups";
            const int expectedGroupId = 123;
            var response = $@"
            {{
                ""groups"": [
                    {{
                       ""group_id"": ""{expectedGroupId}"",
                       ""group_name"": ""{groupName}"",
                       ""updated_at"": ""2021-01-24T11:31:04-06:00""
                    }},
                    {{
                       ""group_id"": ""456"",
                       ""group_name"": ""Octocat admins"",
                       ""updated_at"": ""2021-03-24T11:31:04-06:00""
                    }},
                ]
            }}";

            var githubClientMock = new Mock<GithubClient>(null, null, null);;
            githubClientMock
                .Setup(m => m.GetAsync(url))
                .ReturnsAsync(response);

            // Act
            var githubApi = new GithubApi(githubClientMock.Object);
            var actualGroupId = await githubApi.GetIdpGroupId(org, groupName);

            // Assert
            actualGroupId.Should().Be(expectedGroupId);
        }

        [Fact]
        public async Task GetTeamSlug_Returns_The_Team_Slug()
        {
            // Arrange
            const string org = "ORG";
            const string teamName = "TEAM_NAME";

            var url = $"https://api.github.com/orgs/{org}/teams";
            const string expectedTeamSlug = "justice-league";
            var response = $@"
            [
              {{
                ""id"": 1,
                ""node_id"": ""MDQ6VGVhbTE="",
                ""url"": ""https://api.github.com/teams/1"",
                ""html_url"": ""https://github.com/orgs/github/teams/justice-league"",
                ""name"": ""{teamName}"",
                ""slug"": ""{expectedTeamSlug}"",
                ""description"": ""A great team."",
                ""privacy"": ""closed"",
                ""permission"": ""admin"",
                ""members_url"": ""https://api.github.com/teams/1/members/membber"",
                ""repositories_url"": ""https://api.github.com/teams/1/repos"",
                ""parent"": null
              }}
            ]";

            var githubClientMock = new Mock<GithubClient>(null, null, null);;
            githubClientMock
                .Setup(m => m.GetAsync(url))
                .ReturnsAsync(response);

            // Act
            var githubApi = new GithubApi(githubClientMock.Object);
            var actualTeamSlug = await githubApi.GetTeamSlug(org, teamName);

            // Assert
            actualTeamSlug.Should().Be(expectedTeamSlug);
        }

        [Fact]
        public async Task AddEmuGroupToTeam_Calls_The_Right_Endpoint_With_Payload()
        {
            // Arrange
            const string org = "ORG";
            const string teamSlug = "TEAM_SLUG";
            const int groupId = 1;

            var url = $"https://api.github.com/orgs/{org}/teams/{teamSlug}/external-groups";
            var payload = new { group_id = groupId };

            var githubClientMock = new Mock<GithubClient>(null, null, null);;

            // Act
            var githubApi = new GithubApi(githubClientMock.Object);
            await githubApi.AddEmuGroupToTeam(org, teamSlug, groupId);

            // Assert
            githubClientMock.Verify(m => m.PatchAsync(url, It.Is<object>(x => x.ToJson() == payload.ToJson())));
        }

        [Fact]
        public async Task GrantMigratorRole_Returns_True_On_Success()
        {
            // Arrange
            const string org = "ORG";
            const string actor = "ACTOR";
            const string actorType = "ACTOR_TYPE";
            const string url = "https://api.github.com/graphql";

            var payload =
                "{\"query\":\"mutation grantMigratorRole ( $organizationId: ID!, $actor: String!, $actor_type: ActorType! ) " +
                "{ grantMigratorRole( input: {organizationId: $organizationId, actor: $actor, actorType: $actor_type }) { success } }\"" +
                $",\"variables\":{{\"organizationId\":\"{org}\",\"actor\":\"{actor}\",\"actor_type\":\"{actorType}\"}}," +
                "\"operationName\":\"grantMigratorRole\"}";
            const bool expectedSuccessState = true;
            var response = $@"
            {{
                ""data"": {{
                    ""grantMigratorRole"": {{
                        ""success"": {expectedSuccessState.ToString().ToLower()}
                    }}
                }}
            }}";

            var githubClientMock = new Mock<GithubClient>(null, null, null);;
            githubClientMock
                .Setup(m => m.PostAsync(url, It.Is<object>(x => x.ToJson() == payload)))
                .ReturnsAsync(response);

            // Act
            var githubApi = new GithubApi(githubClientMock.Object);
            var actualSuccessState = await githubApi.GrantMigratorRole(org, actor, actorType);

            // Assert
            actualSuccessState.Should().BeTrue();
        }

        [Fact]
        public async Task GrantMigratorRole_Returns_False_On_HttpRequestException()
        {
            // Arrange
            const string org = "ORG";
            const string actor = "ACTOR";
            const string actorType = "ACTOR_TYPE";
            const string url = "https://api.github.com/graphql";

            var payload =
                "{\"query\":\"mutation grantMigratorRole ( $organizationId: ID!, $actor: String!, $actor_type: ActorType! ) " +
                "{ grantMigratorRole( input: {organizationId: $organizationId, actor: $actor, actorType: $actor_type }) { success } }\"" +
                $",\"variables\":{{\"organizationId\":\"{org}\",\"actor\":\"{actor}\",\"actor_type\":\"{actorType}\"}}," +
                "\"operationName\":\"grantMigratorRole\"}";

            var githubClientMock = new Mock<GithubClient>(null, null, null);;
            githubClientMock
                .Setup(m => m.PostAsync(url, It.Is<object>(x => x.ToJson() == payload)))
                .Throws<HttpRequestException>();

            // Act
            var githubApi = new GithubApi(githubClientMock.Object);
            var actualSuccessState = await githubApi.GrantMigratorRole(org, actor, actorType);

            // Assert
            actualSuccessState.Should().BeFalse();
        }

        [Fact]
        public async Task RevokeMigratorRole_Returns_True_On_Success()
        {
            // Arrange
            const string org = "ORG";
            const string actor = "ACTOR";
            const string actorType = "ACTOR_TYPE";
            const string url = "https://api.github.com/graphql";

            var payload =
                "{\"query\":\"mutation revokeMigratorRole ( $organizationId: ID!, $actor: String!, $actor_type: ActorType! ) " +
                "{ revokeMigratorRole( input: {organizationId: $organizationId, actor: $actor, actorType: $actor_type }) { success } }\"" +
                $",\"variables\":{{\"organizationId\":\"{org}\",\"actor\":\"{actor}\",\"actor_type\":\"{actorType}\"}}," +
                "\"operationName\":\"revokeMigratorRole\"}";
            const bool expectedSuccessState = true;
            var response = $@"
            {{
                ""data"": {{
                    ""revokeMigratorRole"": {{
                        ""success"": {expectedSuccessState.ToString().ToLower()}
                    }}
                }}
            }}";

            var githubClientMock = new Mock<GithubClient>(null, null, null);;
            githubClientMock
                .Setup(m => m.PostAsync(url, It.Is<object>(x => x.ToJson() == payload)))
                .ReturnsAsync(response);

            // Act
            var githubApi = new GithubApi(githubClientMock.Object);
            var actualSuccessState = await githubApi.RevokeMigratorRole(org, actor, actorType);

            // Assert
            actualSuccessState.Should().BeTrue();
        }

        [Fact]
        public async Task RevokeMigratorRole_Returns_False_On_HttpRequestException()
        {
            // Arrange
            const string org = "ORG";
            const string actor = "ACTOR";
            const string actorType = "ACTOR_TYPE";
            const string url = "https://api.github.com/graphql";

            var payload =
                "{\"query\":\"mutation revokeMigratorRole ( $organizationId: ID!, $actor: String!, $actor_type: ActorType! ) " +
                "{ revokeMigratorRole( input: {organizationId: $organizationId, actor: $actor, actorType: $actor_type }) { success } }\"" +
                $",\"variables\":{{\"organizationId\":\"{org}\",\"actor\":\"{actor}\",\"actor_type\":\"{actorType}\"}}," +
                "\"operationName\":\"revokeMigratorRole\"}";

            var githubClientMock = new Mock<GithubClient>(null, null, null);;
            githubClientMock
                .Setup(m => m.PostAsync(url, It.Is<object>(x => x.ToJson() == payload)))
                .Throws<HttpRequestException>();

            // Act
            var githubApi = new GithubApi(githubClientMock.Object);
            var actualSuccessState = await githubApi.RevokeMigratorRole(org, actor, actorType);

            // Assert
            actualSuccessState.Should().BeFalse();
        }
    }
}