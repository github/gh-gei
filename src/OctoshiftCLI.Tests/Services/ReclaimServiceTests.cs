using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using FluentAssertions;
using Moq;
using Octoshift.Models;
using Octoshift.Services;
using OctoshiftCLI.GithubEnterpriseImporter;
using OctoshiftCLI.Models;
using Xunit;

namespace OctoshiftCLI.Tests
{
    public class ReclaimServiceTests
    {
        private const string TARGET_ORG = "FOO-TARGET-ORG";
        private const string HEADER = "mannequin-user,mannequin-id,target-user";



        [Fact]
        public async Task ReclaimMannequins_AlreadyMapped_Force_Reclaim()
        {
            var orgId = Guid.NewGuid().ToString();
            var mannequinId = Guid.NewGuid().ToString();
            var mannequinLogin = "mona";
            var reclaimantId = Guid.NewGuid().ToString();
            var reclaimantLogin = "mona_gh";

            var mockGithubApi = TestHelpers.CreateMock<GithubApi>();

            var mannequinsResponse = new[]
            {
                new Mannequin
                {
                    Id = mannequinId, Login = mannequinLogin, MappedUser = new Claimant { Id = reclaimantId, Login = reclaimantLogin }
                }
            };

            var reclaimMannequinResponse = new MannequinReclaimResult()
            {
                Data = new CreateAttributionInvitationData()
                {
                    CreateAttributionInvitation = new CreateAttributionInvitation()
                    {
                        Source = new UserInfo() { Id = mannequinId, Login = mannequinLogin },
                        Target = new UserInfo() { Id = reclaimantId, Login = reclaimantLogin }
                    }
                }
            };

            mockGithubApi.Setup(x => x.GetOrganizationId(TARGET_ORG).Result).Returns(orgId);
            mockGithubApi.Setup(x => x.GetMannequins(orgId).Result).Returns(mannequinsResponse);
            mockGithubApi.Setup(x => x.GetUserId(reclaimantLogin).Result).Returns(reclaimantId);
            mockGithubApi.Setup(x => x.ReclaimMannequin(orgId, mannequinId, reclaimantId).Result).Returns(reclaimMannequinResponse);

            var csvContent = new string[] {
                HEADER,
                $"{mannequinLogin},{mannequinId},{reclaimantLogin}"
            };

            var reclaimService = new ReclaimService(mockGithubApi.Object, TestHelpers.CreateMock<OctoLogger>().Object);

            // Act
            await reclaimService.ReclaimMannequins(csvContent, TARGET_ORG, true);

            // Assert
            mockGithubApi.Verify(m => m.GetOrganizationId(TARGET_ORG), Times.Once);
            mockGithubApi.Verify(m => m.GetMannequins(orgId), Times.Once);
            mockGithubApi.Verify(x => x.ReclaimMannequin(orgId, mannequinId, reclaimantId), Times.Once);
            mockGithubApi.Verify(x => x.GetUserId(reclaimantLogin), Times.Once);
            mockGithubApi.VerifyNoOtherCalls();
        }

        [Fact]
        public async Task ReclaimMannequins_EmptyCSV_NoReclaims_IssuesWarning()
        {
            var orgId = Guid.NewGuid().ToString();

            var mockGithubApi = TestHelpers.CreateMock<GithubApi>();

            mockGithubApi.Setup(x => x.GetOrganizationId(TARGET_ORG).Result).Returns(orgId);

            var octologgerMock = TestHelpers.CreateMock<OctoLogger>();

            var reclaimService = new ReclaimService(mockGithubApi.Object, octologgerMock.Object);

            var csvContent = Array.Empty<string>();

            // Act
            await reclaimService.ReclaimMannequins(csvContent, TARGET_ORG, false);

            // Assert
            mockGithubApi.VerifyNoOtherCalls();
            octologgerMock.Verify(m => m.LogWarning("File is empty. Nothing to reclaim"), Times.Once);
        }

        [Fact]
        public async Task ReclaimMannequins_InvalidCSVContent_IssuesError()
        {
            var orgId = Guid.NewGuid().ToString();

            var mockGithubApi = TestHelpers.CreateMock<GithubApi>();
            mockGithubApi.Setup(x => x.GetOrganizationId(TARGET_ORG).Result).Returns(orgId);

            var octologgerMock = TestHelpers.CreateMock<OctoLogger>();

            var reclaimService = new ReclaimService(mockGithubApi.Object, octologgerMock.Object);

            var csvContent = new string[] {
                HEADER,
                "login"  // invalid line
            };

            // Act
            await reclaimService.ReclaimMannequins(csvContent, TARGET_ORG, false);

            // Assert
            mockGithubApi.Verify(m => m.GetOrganizationId(TARGET_ORG), Times.Once);
            mockGithubApi.Verify(m => m.GetMannequins(orgId), Times.Once);
            mockGithubApi.Verify(x => x.ReclaimMannequin(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never);
            mockGithubApi.Verify(x => x.GetUserId(It.IsAny<string>()), Times.Never);
            octologgerMock.Verify(m => m.LogError($"Invalid line: \"login\". Will ignore it."), Times.Once);
            mockGithubApi.VerifyNoOtherCalls();
        }

        [Fact]
        public async Task ReclaimMannequins_InvalidCSVHeader_OctoshiftCliException()
        {
            var reclaimService = new ReclaimService(null, TestHelpers.CreateMock<OctoLogger>().Object);

            var csvContent = new string[] {
                "INVALID_HEADER"
            };

            // Act
            await FluentActions
                .Invoking(async () => await reclaimService.ReclaimMannequins(csvContent, TARGET_ORG, false))
                .Should().ThrowAsync<OctoshiftCliException>();
        }

        [Fact]
        public async Task ReclaimMannequins_InvalidCSVContentLoginNotSpecified_IssuesError()
        {
            var orgId = Guid.NewGuid().ToString();

            var mockGithubApi = TestHelpers.CreateMock<GithubApi>();
            mockGithubApi.Setup(x => x.GetOrganizationId(TARGET_ORG).Result).Returns(orgId);

            var octologgerMock = TestHelpers.CreateMock<OctoLogger>();

            var reclaimService = new ReclaimService(mockGithubApi.Object, octologgerMock.Object);

            var csvContent = new string[] {
                HEADER,
                ",,mona_gh"  // invalid line
            };

            // Act
            await reclaimService.ReclaimMannequins(csvContent, TARGET_ORG, false);

            // Assert
            mockGithubApi.Verify(m => m.GetOrganizationId(TARGET_ORG), Times.Once);
            mockGithubApi.Verify(m => m.GetMannequins(orgId), Times.Once);
            mockGithubApi.Verify(x => x.ReclaimMannequin(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never);
            mockGithubApi.Verify(x => x.GetUserId(It.IsAny<string>()), Times.Never);
            octologgerMock.Verify(m => m.LogError($"Invalid line: \",,mona_gh\". Mannequin login is not defined. Will ignore it."), Times.Once);
            mockGithubApi.VerifyNoOtherCalls();
        }

        [Fact]
        public async Task ReclaimMannequins_InvalidCSVContentTargetLoginNotSpecified_IssuesError()
        {
            var orgId = Guid.NewGuid().ToString();

            var mockGithubApi = TestHelpers.CreateMock<GithubApi>();
            mockGithubApi.Setup(x => x.GetOrganizationId(TARGET_ORG).Result).Returns(orgId);

            var octologgerMock = TestHelpers.CreateMock<OctoLogger>();

            var reclaimService = new ReclaimService(mockGithubApi.Object, octologgerMock.Object);

            var csvContent = new string[] {
                HEADER,
                "xx,id,"  // invalid line
            };

            // Act
            await reclaimService.ReclaimMannequins(csvContent, TARGET_ORG, false);

            // Assert
            mockGithubApi.Verify(m => m.GetOrganizationId(TARGET_ORG), Times.Once);
            mockGithubApi.Verify(m => m.GetMannequins(orgId), Times.Once);
            mockGithubApi.Verify(x => x.ReclaimMannequin(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never);
            mockGithubApi.Verify(x => x.GetUserId(It.IsAny<string>()), Times.Never);
            octologgerMock.Verify(m => m.LogError("Invalid line: \"xx,id,\". Target User is not defined. Will ignore it."), Times.Once);
            mockGithubApi.VerifyNoOtherCalls();
        }


        [Fact]
        public async Task ReclaimMannequins_InvalidCSVContentClaimantLoginNotSpecified_IssuesError()
        {
            var orgId = Guid.NewGuid().ToString();

            var mockGithubApi = TestHelpers.CreateMock<GithubApi>();
            mockGithubApi.Setup(x => x.GetOrganizationId(TARGET_ORG).Result).Returns(orgId);

            var octologgerMock = TestHelpers.CreateMock<OctoLogger>();

            var reclaimService = new ReclaimService(mockGithubApi.Object, octologgerMock.Object);

            var csvContent = new string[] {
                HEADER,
                "mona,,"  // invalid line
            };

            // Act
            await reclaimService.ReclaimMannequins(csvContent, TARGET_ORG, false);

            // Assert
            mockGithubApi.Verify(m => m.GetOrganizationId(TARGET_ORG), Times.Once);
            mockGithubApi.Verify(m => m.GetMannequins(orgId), Times.Once);
            mockGithubApi.Verify(x => x.ReclaimMannequin(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never);
            mockGithubApi.Verify(x => x.GetUserId(It.IsAny<string>()), Times.Never);
            octologgerMock.Verify(m => m.LogError($"Invalid line: \"mona,,\". Mannequin Id is not defined. Will ignore it."), Times.Once);
            mockGithubApi.VerifyNoOtherCalls();
        }

        [Fact]
        public async Task ReclaimMannequins_OneUnreclaimedMannequin_Reclaim()
        {
            var orgId = Guid.NewGuid().ToString();
            var mannequinId = Guid.NewGuid().ToString();
            var mannequinLogin = "mona";
            var reclaimantId = Guid.NewGuid().ToString();
            var reclaimantLogin = "mona_gh";

            var mannequinsResponse = new[]
            {
                new Mannequin
                {
                    Id = mannequinId,
                    Login = mannequinLogin
                }
            };

            var reclaimMannequinResponse = new MannequinReclaimResult()
            {
                Data = new CreateAttributionInvitationData()
                {
                    CreateAttributionInvitation = new CreateAttributionInvitation()
                    {
                        Source = new UserInfo()
                        {
                            Id = mannequinId,
                            Login = mannequinLogin
                        },
                        Target = new UserInfo()
                        {
                            Id = reclaimantId,
                            Login = reclaimantLogin
                        }
                    }
                }
            };

            var mockGithubApi = TestHelpers.CreateMock<GithubApi>();
            mockGithubApi.Setup(x => x.GetOrganizationId(TARGET_ORG).Result).Returns(orgId);
            mockGithubApi.Setup(x => x.GetMannequins(orgId).Result).Returns(mannequinsResponse);
            mockGithubApi.Setup(x => x.GetUserId(reclaimantLogin).Result).Returns(reclaimantId);
            mockGithubApi.Setup(x => x.ReclaimMannequin(orgId, mannequinId, reclaimantId).Result).Returns(reclaimMannequinResponse);

            var reclaimService = new ReclaimService(mockGithubApi.Object, TestHelpers.CreateMock<OctoLogger>().Object);

            var csvContent = new string[] {
                HEADER,
                $"{mannequinLogin},{mannequinId},{reclaimantLogin}"
            };

            // Act
            await reclaimService.ReclaimMannequins(csvContent, TARGET_ORG, false);

            // Assert
            mockGithubApi.Verify(m => m.GetOrganizationId(TARGET_ORG), Times.Once);
            mockGithubApi.Verify(m => m.GetMannequins(orgId), Times.Once);
            mockGithubApi.Verify(x => x.ReclaimMannequin(orgId, mannequinId, reclaimantId), Times.Once);
            mockGithubApi.Verify(x => x.GetUserId(reclaimantLogin), Times.Once);
            mockGithubApi.VerifyNoOtherCalls();
        }

        [Fact]
        public async Task ReclaimMannequins_TwoUnreclaimedMannequin_LinesWithWhitespace_Reclaim()
        {
            var orgId = Guid.NewGuid().ToString();
            var mannequinId = "mannequin1id";
            var mannequinLogin = "mona";
            var reclaimantId = "reclaimant1id";
            var reclaimantLogin = "mona_gh";

            var mannequinId2 = "mannequin2id";
            var mannequinLogin2 = "lisa";
            var reclaimantId2 = "reclaimant2id";
            var reclaimantLogin2 = "lisa_gh";

            var mannequinsResponse = new[]
            {
                new Mannequin
                {
                    Id = mannequinId,
                    Login = mannequinLogin
                },
                new Mannequin
                {
                    Id = mannequinId2,
                    Login = mannequinLogin2
                }
            };

            var reclaimMannequinResponse = new MannequinReclaimResult()
            {
                Data = new CreateAttributionInvitationData()
                {
                    CreateAttributionInvitation = new CreateAttributionInvitation()
                    {
                        Source = new UserInfo()
                        {
                            Id = mannequinId,
                            Login = mannequinLogin
                        },
                        Target = new UserInfo()
                        {
                            Id = reclaimantId,
                            Login = reclaimantLogin
                        }
                    }
                }
            };

            var reclaimMannequinResponse2 = new MannequinReclaimResult()
            {
                Data = new CreateAttributionInvitationData()
                {
                    CreateAttributionInvitation = new CreateAttributionInvitation()
                    {
                        Source = new UserInfo()
                        {
                            Id = mannequinId2,
                            Login = mannequinLogin2
                        },
                        Target = new UserInfo()
                        {
                            Id = reclaimantId2,
                            Login = reclaimantLogin2
                        }
                    }
                }
            };

            var mockGithubApi = TestHelpers.CreateMock<GithubApi>();
            mockGithubApi.Setup(x => x.GetOrganizationId(TARGET_ORG).Result).Returns(orgId);
            mockGithubApi.Setup(x => x.GetMannequins(orgId).Result).Returns(mannequinsResponse);
            mockGithubApi.Setup(x => x.GetUserId(reclaimantLogin).Result).Returns(reclaimantId);
            mockGithubApi.Setup(x => x.GetUserId(reclaimantLogin2).Result).Returns(reclaimantId2);
            mockGithubApi.Setup(x => x.ReclaimMannequin(orgId, mannequinId, reclaimantId).Result).Returns(reclaimMannequinResponse);
            mockGithubApi.Setup(x => x.ReclaimMannequin(orgId, mannequinId2, reclaimantId2).Result).Returns(reclaimMannequinResponse2);

            var reclaimService = new ReclaimService(mockGithubApi.Object, TestHelpers.CreateMock<OctoLogger>().Object);

            var csvContent = new string[] {
                HEADER,
                $"{mannequinLogin},{mannequinId},{reclaimantLogin}",
                "", // Empty Line
                " ", // whitespace line
                $"{mannequinLogin2},{mannequinId2},{reclaimantLogin2}"
            };

            // Act
            await reclaimService.ReclaimMannequins(csvContent, TARGET_ORG, false);

            // Assert
            mockGithubApi.Verify(m => m.GetOrganizationId(TARGET_ORG), Times.Once);
            mockGithubApi.Verify(m => m.GetMannequins(orgId), Times.Once);
            mockGithubApi.Verify(x => x.ReclaimMannequin(orgId, mannequinId, reclaimantId), Times.Once);
            mockGithubApi.Verify(x => x.ReclaimMannequin(orgId, mannequinId2, reclaimantId2), Times.Once);
            mockGithubApi.Verify(x => x.GetUserId(reclaimantLogin), Times.Once);
            mockGithubApi.Verify(x => x.GetUserId(reclaimantLogin2), Times.Once);
            mockGithubApi.Verify(x => x.GetUserId(reclaimantLogin2), Times.Once);
            mockGithubApi.VerifyNoOtherCalls();
        }

        [Fact]
        public async Task ReclaimMannequins_SameLoginDifferentIds_MapSameUser_Reclaim()
        {
            var orgId = Guid.NewGuid().ToString();
            var mannequinId = "mannequin1id";
            var mannequinLogin = "mona";
            var reclaimantId = "reclaimant1id";
            var reclaimantLogin = "mona_gh";

            var mannequinId2 = "mannequin2id";
            var mannequinLogin2 = "mona";

            var mannequinsResponse = new[]
            {
                new Mannequin
                {
                    Id = mannequinId,
                    Login = mannequinLogin
                },
                new Mannequin
                {
                    Id = mannequinId2,
                    Login = mannequinLogin2
                }
            };

            var reclaimMannequinResponse = new MannequinReclaimResult()
            {
                Data = new CreateAttributionInvitationData()
                {
                    CreateAttributionInvitation = new CreateAttributionInvitation()
                    {
                        Source = new UserInfo()
                        {
                            Id = mannequinId,
                            Login = mannequinLogin
                        },
                        Target = new UserInfo()
                        {
                            Id = reclaimantId,
                            Login = reclaimantLogin
                        }
                    }
                }
            };

            var reclaimMannequinResponse2 = new MannequinReclaimResult()
            {
                Data = new CreateAttributionInvitationData()
                {
                    CreateAttributionInvitation = new CreateAttributionInvitation()
                    {
                        Source = new UserInfo()
                        {
                            Id = mannequinId2,
                            Login = mannequinLogin2
                        },
                        Target = new UserInfo()
                        {
                            Id = reclaimantId,
                            Login = reclaimantLogin
                        }
                    }
                }
            };

            var mockGithubApi = TestHelpers.CreateMock<GithubApi>();
            mockGithubApi.Setup(x => x.GetOrganizationId(TARGET_ORG).Result).Returns(orgId);
            mockGithubApi.Setup(x => x.GetMannequins(orgId).Result).Returns(mannequinsResponse);
            mockGithubApi.Setup(x => x.GetUserId(reclaimantLogin).Result).Returns(reclaimantId);
            mockGithubApi.Setup(x => x.ReclaimMannequin(orgId, mannequinId, reclaimantId).Result).Returns(reclaimMannequinResponse);
            mockGithubApi.Setup(x => x.ReclaimMannequin(orgId, mannequinId2, reclaimantId).Result).Returns(reclaimMannequinResponse2);

            var reclaimService = new ReclaimService(mockGithubApi.Object, TestHelpers.CreateMock<OctoLogger>().Object);

            var csvContent = new string[] {
                HEADER,
                $"{mannequinLogin},{mannequinId},{reclaimantLogin}",
                $"{mannequinLogin2},{mannequinId2},{reclaimantLogin}"
            };

            // Act
            await reclaimService.ReclaimMannequins(csvContent, TARGET_ORG, false);

            // Assert
            mockGithubApi.Verify(m => m.GetOrganizationId(TARGET_ORG), Times.Once);
            mockGithubApi.Verify(m => m.GetMannequins(orgId), Times.Once);
            mockGithubApi.Verify(x => x.ReclaimMannequin(orgId, mannequinId, reclaimantId), Times.Once);
            mockGithubApi.Verify(x => x.ReclaimMannequin(orgId, mannequinId2, reclaimantId), Times.Once);
            mockGithubApi.Verify(x => x.GetUserId(reclaimantLogin), Times.Exactly(2));
            mockGithubApi.VerifyNoOtherCalls();
        }

        [Fact]
        public async Task ReclaimMannequins_AlreadyMapped_No_Reclaim_ShowsError()
        {
            var orgId = Guid.NewGuid().ToString();
            var mannequinId = Guid.NewGuid().ToString();
            var mannequinLogin = "mona";
            var reclaimantId = Guid.NewGuid().ToString();
            var reclaimantLogin = "mona_gh";

            var mannequinsResponse = new[]
            {
                new Mannequin
                {
                    Id = mannequinId,
                    Login = mannequinLogin,
                    MappedUser = new Claimant
                    {
                        Id = reclaimantId,
                        Login = reclaimantLogin
                    }
                }
            };

            var mockGithubApi = TestHelpers.CreateMock<GithubApi>();
            mockGithubApi.Setup(x => x.GetOrganizationId(TARGET_ORG).Result).Returns(orgId);
            mockGithubApi.Setup(x => x.GetMannequins(orgId).Result).Returns(mannequinsResponse);

            var octologgerMock = TestHelpers.CreateMock<OctoLogger>();

            var reclaimService = new ReclaimService(mockGithubApi.Object, octologgerMock.Object);

            var csvContent = new string[] {
                HEADER,
                $"{mannequinLogin},{mannequinId},{reclaimantLogin}"
            };

            // Act
            await reclaimService.ReclaimMannequins(csvContent, TARGET_ORG, false);

            // Assert
            mockGithubApi.Verify(m => m.GetOrganizationId(TARGET_ORG), Times.Once);
            mockGithubApi.Verify(m => m.GetMannequins(orgId), Times.Once);
            mockGithubApi.Verify(x => x.ReclaimMannequin(orgId, mannequinId, reclaimantId), Times.Never);
            mockGithubApi.Verify(x => x.GetUserId(reclaimantLogin), Times.Never);
            octologgerMock.Verify(x => x.LogError($"{mannequinLogin} is already claimed. Skipping (use force if you want to reclaim)"), Times.Once);
            mockGithubApi.VerifyNoOtherCalls();
        }

        [Fact]
        public async Task ReclaimMannequins_NoExistantMannequin_No_Reclaim_IssuesError()
        {
            var orgId = Guid.NewGuid().ToString();
            var mannequinId = Guid.NewGuid().ToString();
            var mannequinLogin = "mona";
            var reclaimantId = Guid.NewGuid().ToString();
            var reclaimantLogin = "mona_gh";

            var mannequinsResponse = Array.Empty<Mannequin>();

            var mockGithubApi = TestHelpers.CreateMock<GithubApi>();
            mockGithubApi.Setup(x => x.GetOrganizationId(TARGET_ORG).Result).Returns(orgId);
            mockGithubApi.Setup(x => x.GetMannequins(orgId).Result).Returns(mannequinsResponse);

            var octologgerMock = TestHelpers.CreateMock<OctoLogger>();

            var reclaimService = new ReclaimService(mockGithubApi.Object, octologgerMock.Object);

            var csvContent = new string[] {
                HEADER,
                $"{mannequinLogin},{mannequinId},{reclaimantLogin}"
            };

            // Act
            await reclaimService.ReclaimMannequins(csvContent, TARGET_ORG, false);

            // Assert
            mockGithubApi.Verify(m => m.GetOrganizationId(TARGET_ORG), Times.Once);
            mockGithubApi.Verify(m => m.GetMannequins(orgId), Times.Once);
            mockGithubApi.Verify(x => x.ReclaimMannequin(orgId, mannequinId, reclaimantId), Times.Never);
            mockGithubApi.Verify(x => x.GetUserId(reclaimantLogin), Times.Never);
            octologgerMock.Verify(x => x.LogError($"Mannequin {mannequinLogin} not found. Skipping."), Times.Once);
            mockGithubApi.VerifyNoOtherCalls();
        }

        [Fact]
        public async Task ReclaimMannequin_TwoUsersSameLogin_AllReclaimed()
        {
            var githubOrg = "FooOrg";
            var githubOrgId = Guid.NewGuid().ToString();
            var mannequinUser = "mona";
            var mannequinUserId = "id1";
            var mannequinUserId2 = "id2";
            var targetUser = "mona_emu";
            var targetUserId = Guid.NewGuid().ToString();

            var mannequinsResponse = new Mannequin[] {
                new Mannequin { Id = mannequinUserId, Login = mannequinUser},
                new Mannequin { Id = mannequinUserId2, Login = mannequinUser},
            };

            var reclaimMannequinResponse = new MannequinReclaimResult()
            {
                Data = new CreateAttributionInvitationData()
                {
                    CreateAttributionInvitation = new CreateAttributionInvitation()
                    {
                        Source = new UserInfo() { Id = mannequinUserId, Login = mannequinUser },
                        Target = new UserInfo() { Id = targetUserId, Login = targetUser }
                    }
                }
            };

            var reclaimMannequinResponse2 = new MannequinReclaimResult()
            {
                Data = new CreateAttributionInvitationData()
                {
                    CreateAttributionInvitation = new CreateAttributionInvitation()
                    {
                        Source = new UserInfo() { Id = mannequinUserId2, Login = mannequinUser },
                        Target = new UserInfo() { Id = targetUserId, Login = targetUser }
                    }
                }
            };

            var mockGithub = TestHelpers.CreateMock<GithubApi>();
            mockGithub.Setup(x => x.GetOrganizationId(githubOrg).Result).Returns(githubOrgId);
            mockGithub.Setup(x => x.GetMannequins(githubOrgId).Result).Returns(mannequinsResponse);
            mockGithub.Setup(x => x.GetUserId(targetUser).Result).Returns(targetUserId);
            mockGithub.Setup(x => x.ReclaimMannequin(githubOrgId, mannequinUserId, targetUserId).Result).Returns(reclaimMannequinResponse);
            mockGithub.Setup(x => x.ReclaimMannequin(githubOrgId, mannequinUserId2, targetUserId).Result).Returns(reclaimMannequinResponse2);

            var reclaimService = new ReclaimService(mockGithub.Object, TestHelpers.CreateMock<OctoLogger>().Object);

            // Act
            await reclaimService.ReclaimMannequin(mannequinUser, null, targetUser, githubOrg, false);

            // Assert
            mockGithub.Verify(x => x.ReclaimMannequin(githubOrgId, mannequinUserId, targetUserId), Times.Once);
            mockGithub.Verify(x => x.ReclaimMannequin(githubOrgId, mannequinUserId2, targetUserId), Times.Once);
            mockGithub.Verify(x => x.GetUserId(targetUser), Times.Once);
        }

        [Fact]
        public async Task ReclaimMannequin_Happy_Path()
        {
            var githubOrg = "FooOrg";
            var githubOrgId = Guid.NewGuid().ToString();
            var mannequinUser = "mona";
            var mannequinUserId = Guid.NewGuid().ToString();
            var targetUser = "mona_emu";
            var targetUserId = Guid.NewGuid().ToString();

            var mannequinsResponse = new Mannequin[] {
                new Mannequin { Id = mannequinUserId, Login = mannequinUser}
            };

            var reclaimMannequinResponse = new MannequinReclaimResult()
            {
                Data = new CreateAttributionInvitationData()
                {
                    CreateAttributionInvitation = new CreateAttributionInvitation()
                    {
                        Source = new UserInfo() { Id = mannequinUserId, Login = mannequinUser },
                        Target = new UserInfo() { Id = targetUserId, Login = targetUser }
                    }
                }
            };

            var mockGithub = TestHelpers.CreateMock<GithubApi>();
            mockGithub.Setup(x => x.GetOrganizationId(githubOrg).Result).Returns(githubOrgId);
            mockGithub.Setup(x => x.GetMannequins(githubOrgId).Result).Returns(mannequinsResponse);
            mockGithub.Setup(x => x.GetUserId(targetUser).Result).Returns(targetUserId);
            mockGithub.Setup(x => x.ReclaimMannequin(githubOrgId, mannequinUserId, targetUserId).Result).Returns(reclaimMannequinResponse);

            var reclaimService = new ReclaimService(mockGithub.Object, TestHelpers.CreateMock<OctoLogger>().Object);

            // Act
            await reclaimService.ReclaimMannequin(mannequinUser, null, targetUser, githubOrg, false);

            mockGithub.Verify(x => x.ReclaimMannequin(githubOrgId, mannequinUserId, targetUserId), Times.Once);
        }

        [Fact]
        public async Task ReclaimMannequin_Duplicates_ReclaimOnlyOnce()
        {
            var githubOrg = "FooOrg";
            var githubOrgId = Guid.NewGuid().ToString();
            var mannequinUser = "mona";
            var mannequinUserId = Guid.NewGuid().ToString();
            var targetUser = "mona_emu";
            var targetUserId = Guid.NewGuid().ToString();

            var mannequinsResponse = new Mannequin[] {
                new Mannequin { Id = mannequinUserId, Login = "mona"},
                new Mannequin { Id = mannequinUserId, Login = "mona"}
            };

            var reclaimMannequinResponse = new MannequinReclaimResult()
            {
                Data = new CreateAttributionInvitationData()
                {
                    CreateAttributionInvitation = new CreateAttributionInvitation()
                    {
                        Source = new UserInfo() { Id = mannequinUserId, Login = mannequinUser },
                        Target = new UserInfo() { Id = targetUserId, Login = targetUser }
                    }
                }
            };

            var mockGithub = TestHelpers.CreateMock<GithubApi>();
            mockGithub.Setup(x => x.GetOrganizationId(githubOrg).Result).Returns(githubOrgId);
            mockGithub.Setup(x => x.GetMannequins(githubOrgId).Result).Returns(mannequinsResponse);
            mockGithub.Setup(x => x.GetUserId(targetUser).Result).Returns(targetUserId);
            mockGithub.Setup(x => x.ReclaimMannequin(githubOrgId, mannequinUserId, targetUserId).Result).Returns(reclaimMannequinResponse);

            var reclaimService = new ReclaimService(mockGithub.Object, TestHelpers.CreateMock<OctoLogger>().Object);

            // Act
            await reclaimService.ReclaimMannequin(mannequinUser, null, targetUser, githubOrg, false);

            mockGithub.Verify(x => x.ReclaimMannequin(githubOrgId, mannequinUserId, targetUserId), Times.Once);
        }

        [Fact]
        public async Task ReclaimMannequin_Mannequins_ReclaimOnlySpecifiedId()
        {
            var githubOrg = "FooOrg";
            var githubOrgId = Guid.NewGuid().ToString();
            var mannequinUser = "mona";
            var mannequinUserId = Guid.NewGuid().ToString();
            var mannequinUserId2 = Guid.NewGuid().ToString();
            var targetUser = "mona_emu";
            var targetUserId = Guid.NewGuid().ToString();

            var mannequinsResponse = new Mannequin[] {
                new Mannequin { Id = mannequinUserId, Login = "mona"},
                new Mannequin { Id = mannequinUserId2, Login = "mona"}
            };

            var reclaimMannequinResponse = new MannequinReclaimResult()
            {
                Data = new CreateAttributionInvitationData()
                {
                    CreateAttributionInvitation = new CreateAttributionInvitation()
                    {
                        Source = new UserInfo() { Id = mannequinUserId, Login = mannequinUser },
                        Target = new UserInfo() { Id = targetUserId, Login = targetUser }
                    }
                }
            };
            var reclaimMannequinResponse2 = new MannequinReclaimResult()
            {
                Data = new CreateAttributionInvitationData()
                {
                    CreateAttributionInvitation = new CreateAttributionInvitation()
                    {
                        Source = new UserInfo() { Id = mannequinUserId2, Login = mannequinUser },
                        Target = new UserInfo() { Id = targetUserId, Login = targetUser }
                    }
                }
            };

            var mockGithub = TestHelpers.CreateMock<GithubApi>();
            mockGithub.Setup(x => x.GetOrganizationId(githubOrg).Result).Returns(githubOrgId);
            mockGithub.Setup(x => x.GetMannequins(githubOrgId).Result).Returns(mannequinsResponse);
            mockGithub.Setup(x => x.GetUserId(targetUser).Result).Returns(targetUserId);
            mockGithub.Setup(x => x.ReclaimMannequin(githubOrgId, mannequinUserId, targetUserId).Result).Returns(reclaimMannequinResponse);
            mockGithub.Setup(x => x.ReclaimMannequin(githubOrgId, mannequinUserId2, targetUserId).Result).Returns(reclaimMannequinResponse2);

            var reclaimService = new ReclaimService(mockGithub.Object, TestHelpers.CreateMock<OctoLogger>().Object);

            // Act
            await reclaimService.ReclaimMannequin(mannequinUser, mannequinUserId, targetUser, githubOrg, false);

            mockGithub.Verify(x => x.ReclaimMannequin(githubOrgId, mannequinUserId, targetUserId), Times.Once);
            mockGithub.Verify(x => x.ReclaimMannequin(githubOrgId, mannequinUserId2, targetUserId), Times.Never);
        }


        [Fact]
        public async Task ReclaimMannequin_Duplicates_ForceReclaimOnlyOnce()
        {
            var githubOrg = "FooOrg";
            var githubOrgId = Guid.NewGuid().ToString();
            var mannequinUser = "mona";
            var mannequinUserId = Guid.NewGuid().ToString();
            var targetUser = "mona_emu";
            var targetUserId = Guid.NewGuid().ToString();

            var mannequinsResponse = new Mannequin[] {
                new Mannequin { Id = mannequinUserId, Login = "mona"},
                new Mannequin { Id = mannequinUserId, Login = "mona"},
                new Mannequin { Id = mannequinUserId, Login = "mona", MappedUser = new Claimant() { Id = targetUserId, Login = targetUser } }
            };

            var reclaimMannequinResponse = new MannequinReclaimResult()
            {
                Data = new CreateAttributionInvitationData()
                {
                    CreateAttributionInvitation = new CreateAttributionInvitation()
                    {
                        Source = new UserInfo() { Id = mannequinUserId, Login = mannequinUser },
                        Target = new UserInfo() { Id = targetUserId, Login = targetUser }
                    }
                }
            };

            var mockGithub = TestHelpers.CreateMock<GithubApi>();
            mockGithub.Setup(x => x.GetOrganizationId(githubOrg).Result).Returns(githubOrgId);
            mockGithub.Setup(x => x.GetMannequins(githubOrgId).Result).Returns(mannequinsResponse);
            mockGithub.Setup(x => x.GetUserId(targetUser).Result).Returns(targetUserId);
            mockGithub.Setup(x => x.ReclaimMannequin(githubOrgId, mannequinUserId, targetUserId).Result).Returns(reclaimMannequinResponse);

            var reclaimService = new ReclaimService(mockGithub.Object, TestHelpers.CreateMock<OctoLogger>().Object);

            // Act
            await reclaimService.ReclaimMannequin(mannequinUser, mannequinUserId, targetUser, githubOrg, true);

            mockGithub.Verify(x => x.ReclaimMannequin(githubOrgId, mannequinUserId, targetUserId), Times.Once);
        }

        [Fact]
        public async Task ReclaimMannequin_Duplicates_NoReclaimOneAlreadyReclaimed_OctoshiftCliException()
        {
            var githubOrg = "FooOrg";
            var githubOrgId = Guid.NewGuid().ToString();
            var mannequinUser = "mona";
            var mannequinUserId = Guid.NewGuid().ToString();
            var targetUser = "mona_emu";
            var targetUserId = Guid.NewGuid().ToString();

            var mannequinsResponse = new Mannequin[] {
                new Mannequin { Id = mannequinUserId, Login = "mona"},
                new Mannequin { Id = mannequinUserId, Login = "mona"},
                new Mannequin { Id = mannequinUserId, Login = "mona",
                    MappedUser = new Claimant() { Id = targetUserId, Login = targetUser }
                }
            };

            var reclaimMannequinResponse = new MannequinReclaimResult()
            {
                Data = new CreateAttributionInvitationData()
                {
                    CreateAttributionInvitation = new CreateAttributionInvitation()
                    {
                        Source = new UserInfo() { Id = mannequinUserId, Login = mannequinUser },
                        Target = new UserInfo() { Id = targetUserId, Login = targetUser }
                    }
                }
            };

            var mockGithub = TestHelpers.CreateMock<GithubApi>();
            mockGithub.Setup(x => x.GetOrganizationId(githubOrg).Result).Returns(githubOrgId);
            mockGithub.Setup(x => x.GetMannequins(githubOrgId).Result).Returns(mannequinsResponse);
            mockGithub.Setup(x => x.GetUserId(targetUser).Result).Returns(targetUserId);
            mockGithub.Setup(x => x.ReclaimMannequin(githubOrgId, null, targetUserId).Result).Returns(reclaimMannequinResponse);

            var reclaimService = new ReclaimService(mockGithub.Object, TestHelpers.CreateMock<OctoLogger>().Object);

            var exception = await FluentActions
                .Invoking(async () => await reclaimService.ReclaimMannequin(mannequinUser, null, targetUser, githubOrg, false))
                .Should().ThrowAsync<OctoshiftCliException>();
            exception.WithMessage("User mona is already mapped to a user. Use the force option if you want to reclaim the mannequin again.");

            mockGithub.Verify(x => x.ReclaimMannequin(githubOrgId, mannequinUserId, targetUserId), Times.Never);
        }

        [Fact]
        public async Task ReclaimMannequin_AlreadyMapped_No_Reclaim_Throws_OctoshiftCliException()
        {
            var githubOrg = "FooOrg";
            var githubOrgId = Guid.NewGuid().ToString();
            var mannequinUser = "mona";
            var mannequinUserId = Guid.NewGuid().ToString();
            var targetUser = "mona_emu";
            var targetUserId = Guid.NewGuid().ToString();

            var mannequinsResponse = new Mannequin[] {
                new Mannequin { Id = mannequinUserId, Login = "mona",
                    MappedUser = new Claimant { Id = "claimantId",Login = "claimant" }
                }
            };

            var mockGithub = TestHelpers.CreateMock<GithubApi>();
            mockGithub.Setup(x => x.GetOrganizationId(githubOrg).Result).Returns(githubOrgId);
            mockGithub.Setup(x => x.GetMannequins(githubOrgId).Result).Returns(mannequinsResponse);

            var mockGithubApiFactory = new Mock<ITargetGithubApiFactory>();
            mockGithubApiFactory.Setup(m => m.Create(null, null)).Returns(mockGithub.Object);

            var reclaimService = new ReclaimService(mockGithub.Object, TestHelpers.CreateMock<OctoLogger>().Object);

            // Act
            var exception = await FluentActions
                .Invoking(async () => await reclaimService.ReclaimMannequin(mannequinUser, null, targetUser, githubOrg, false))
                .Should().ThrowAsync<OctoshiftCliException>();

            mockGithub.Verify(x => x.GetUserId(targetUser), Times.Never());
            mockGithub.Verify(x => x.ReclaimMannequin(githubOrgId, mannequinUserId, targetUserId), Times.Never());
            exception.WithMessage("User mona is already mapped to a user. Use the force option if you want to reclaim the mannequin again.");
        }

        [Fact]
        public async Task ReclaimMannequin_AlreadyMapped_Force_Reclaim()
        {
            var githubOrg = "FooOrg";
            var githubOrgId = Guid.NewGuid().ToString();
            var mannequinUser = "mona";
            var mannequinUserId = Guid.NewGuid().ToString();
            var targetUser = "mona_emu";
            var targetUserId = Guid.NewGuid().ToString();

            var mannequinsResponse = new Mannequin[] {
                new Mannequin
                {
                    Id = mannequinUserId,
                    Login = "mona",
                    MappedUser = new Claimant
                    {
                        Id = "claimantId",
                        Login = "claimant"
                    }
                }
            };

            var reclaimMannequinResponse = new MannequinReclaimResult()
            {
                Data = new CreateAttributionInvitationData()
                {
                    CreateAttributionInvitation = new CreateAttributionInvitation()
                    {
                        Source = new UserInfo() { Id = mannequinUserId, Login = mannequinUser },
                        Target = new UserInfo() { Id = targetUserId, Login = targetUser }
                    }
                }
            };

            var mockGithub = TestHelpers.CreateMock<GithubApi>();
            mockGithub.Setup(x => x.GetOrganizationId(githubOrg).Result).Returns(githubOrgId);
            mockGithub.Setup(x => x.GetMannequins(githubOrgId).Result).Returns(mannequinsResponse);
            mockGithub.Setup(x => x.GetUserId(targetUser).Result).Returns(targetUserId);
            mockGithub.Setup(x => x.ReclaimMannequin(githubOrgId, mannequinUserId, targetUserId).Result).Returns(reclaimMannequinResponse);

            var reclaimService = new ReclaimService(mockGithub.Object, TestHelpers.CreateMock<OctoLogger>().Object);

            // Act
            await reclaimService.ReclaimMannequin(mannequinUser, null, targetUser, githubOrg, true);

            mockGithub.Verify(x => x.ReclaimMannequin(githubOrgId, mannequinUserId, targetUserId));
        }

        [Fact]
        public async Task ReclaimMannequin_FailtToReclaim_LogsError_Throws_OctoshiftCliException()
        {
            var githubOrg = "FooOrg";
            var githubOrgId = Guid.NewGuid().ToString();
            var mannequinUser = "mona";
            var mannequinUserId = Guid.NewGuid().ToString();
            var targetUser = "mona_emu";
            var targetUserId = Guid.NewGuid().ToString();
            var failureMessage = "Target must be a member of the octocat organization";
            var mannequinsResponse = new Mannequin[] {
                new Mannequin { Id = mannequinUserId, Login = "mona" }
            };

            var reclaimMannequinResponse = new MannequinReclaimResult()
            {
                Data = new CreateAttributionInvitationData()
                {
                    CreateAttributionInvitation = null
                },
                Errors = new Collection<ErrorData>{new ErrorData
                {
                    Type = "UNPROCESSABLE",
                    Message = failureMessage,
                    Path = new Collection<string> { "createAttributionInvitation" },
                    Locations = new Collection<Location> { new Location() { Line = 2, Column = 14 } }
                    }
                }
            };

            var mockGithub = TestHelpers.CreateMock<GithubApi>();
            mockGithub.Setup(x => x.GetOrganizationId(githubOrg).Result).Returns(githubOrgId);
            mockGithub.Setup(x => x.GetMannequins(githubOrgId).Result).Returns(mannequinsResponse);
            mockGithub.Setup(x => x.GetUserId(targetUser).Result).Returns(targetUserId);
            mockGithub.Setup(x => x.ReclaimMannequin(githubOrgId, mannequinUserId, targetUserId).Result).Returns(reclaimMannequinResponse);

            var mockGithubApiFactory = new Mock<ITargetGithubApiFactory>();
            mockGithubApiFactory.Setup(m => m.Create(null, null)).Returns(mockGithub.Object);

            var octologgerMock = TestHelpers.CreateMock<OctoLogger>();

            var reclaimService = new ReclaimService(mockGithub.Object, octologgerMock.Object);

            // Act
            var exception = await FluentActions
                .Invoking(async () => await reclaimService.ReclaimMannequin(mannequinUser, null, targetUser, githubOrg, false))
                .Should().ThrowAsync<OctoshiftCliException>();
            exception.WithMessage("Failed to reclaim mannequin(s).");

            octologgerMock.Verify(m => m.LogError($"Failed to reclaim {mannequinUser} ({mannequinUserId}) to {targetUser} ({targetUserId}) Reason: {failureMessage}"), Times.Once);
        }

        [Fact]
        public async Task ReclaimMannequin_TwoMannequins_FailtToReclaimOne_LogsError_Throws_OctoshiftCliException()
        {
            var githubOrg = "FooOrg";
            var githubOrgId = Guid.NewGuid().ToString();
            var mannequinUser = "mona";
            var mannequinUserId = "monaId";
            var mannequinUser2 = "mona";
            var mannequinUserId2 = "modaId2";
            var targetUser = "mona_emu";
            var targetUserId = "mona_emuId";
            var failureMessage = "Target must be a member of the octocat organization";
            var mannequinsResponse = new Mannequin[] {
                new Mannequin { Id = mannequinUserId, Login = mannequinUser },
                new Mannequin { Id = mannequinUserId2, Login = mannequinUser2 }
            };

            var reclaimMannequinResponse = new MannequinReclaimResult()
            {
                Data = new CreateAttributionInvitationData()
                {
                    CreateAttributionInvitation = null
                },
                Errors = new Collection<ErrorData>{new ErrorData
                {
                    Type = "UNPROCESSABLE",
                    Message = failureMessage,
                    Path = new Collection<string> { "createAttributionInvitation" },
                    Locations = new Collection<Location> { new Location() { Line = 2, Column = 14 } }
                    }
                }
            };

            var reclaimMannequinResponse2 = new MannequinReclaimResult()
            {
                Data = new CreateAttributionInvitationData()
                {
                    CreateAttributionInvitation = new CreateAttributionInvitation()
                    {
                        Source = new UserInfo() { Id = mannequinUserId2, Login = mannequinUser2 },
                        Target = new UserInfo() { Id = targetUserId, Login = targetUser }
                    }
                }
            };

            var mockGithub = TestHelpers.CreateMock<GithubApi>();
            mockGithub.Setup(x => x.GetOrganizationId(githubOrg).Result).Returns(githubOrgId);
            mockGithub.Setup(x => x.GetMannequins(githubOrgId).Result).Returns(mannequinsResponse);
            mockGithub.Setup(x => x.GetUserId(targetUser).Result).Returns(targetUserId);
            mockGithub.Setup(x => x.ReclaimMannequin(githubOrgId, mannequinUserId, targetUserId).Result).Returns(reclaimMannequinResponse);
            mockGithub.Setup(x => x.ReclaimMannequin(githubOrgId, mannequinUserId2, targetUserId).Result).Returns(reclaimMannequinResponse2);

            var mockGithubApiFactory = new Mock<ITargetGithubApiFactory>();
            mockGithubApiFactory.Setup(m => m.Create(null, null)).Returns(mockGithub.Object);

            var octologgerMock = TestHelpers.CreateMock<OctoLogger>();

            var reclaimService = new ReclaimService(mockGithub.Object, octologgerMock.Object);

            // Act
            var exception = await FluentActions
                .Invoking(async () => await reclaimService.ReclaimMannequin(mannequinUser, null, targetUser, githubOrg, false))
                .Should().ThrowAsync<OctoshiftCliException>();
            exception.WithMessage("Failed to reclaim mannequin(s).");

            mockGithub.Verify(x => x.ReclaimMannequin(githubOrgId, mannequinUserId, targetUserId), Times.Once);
            mockGithub.Verify(x => x.ReclaimMannequin(githubOrgId, mannequinUserId2, targetUserId), Times.Once);

            octologgerMock.Verify(m => m.LogError($"Failed to reclaim {mannequinUser} ({mannequinUserId}) to {targetUser} ({targetUserId}) Reason: {failureMessage}"), Times.Once);
        }


        [Fact]
        public async Task ReclaimMannequin_NoExistantMannequin_No_Reclaim_Throws_OctoshiftCliException()
        {
            var githubOrg = "FooOrg";
            var githubOrgId = Guid.NewGuid().ToString();
            var mannequinUser = "monadoesnotexist";
            var mannequinUserId = Guid.NewGuid().ToString();
            var targetUser = "mona_emu";
            var targetUserId = Guid.NewGuid().ToString();
            var mannequinsResponse = Array.Empty<Mannequin>();

            var mockGithub = TestHelpers.CreateMock<GithubApi>();
            mockGithub.Setup(x => x.GetOrganizationId(githubOrg).Result).Returns(githubOrgId);
            mockGithub.Setup(x => x.GetMannequins(githubOrgId).Result).Returns(mannequinsResponse);
            mockGithub.Setup(x => x.GetUserId(targetUser).Result).Returns(targetUserId);

            var reclaimService = new ReclaimService(mockGithub.Object, TestHelpers.CreateMock<OctoLogger>().Object);

            // Act
            await FluentActions
                .Invoking(async () => await reclaimService.ReclaimMannequin(mannequinUser, null, targetUser, githubOrg, false))
                .Should().ThrowAsync<OctoshiftCliException>();

            mockGithub.Verify(x => x.ReclaimMannequin(githubOrgId, mannequinUserId, targetUserId), Times.Never());
            mockGithub.Verify(x => x.GetUserId(targetUser), Times.Never());
        }

        [Fact]
        public async Task ReclaimMannequin_NoClaimantUser_No_Reclaim_Throws_OctoshiftCliException()
        {
            var githubOrg = "FooOrg";
            var githubOrgId = Guid.NewGuid().ToString();
            var mannequinUser = "mona";
            var mannequinUserId = Guid.NewGuid().ToString();
            var targetUser = "mona_emu";
            var targetUserId = Guid.NewGuid().ToString();
            var mannequinsResponse = new Mannequin[] {
                new Mannequin { Id = mannequinUserId,Login = mannequinUser,
                    MappedUser = new Claimant { Id = "claimantId", Login = "claimant" }
                }
            };

            var mockGithub = TestHelpers.CreateMock<GithubApi>();
            mockGithub.Setup(x => x.GetOrganizationId(githubOrg).Result).Returns(githubOrgId);
            mockGithub.Setup(x => x.GetMannequins(githubOrgId).Result).Returns(mannequinsResponse);

            var reclaimService = new ReclaimService(mockGithub.Object, TestHelpers.CreateMock<OctoLogger>().Object);

            // Act
            await FluentActions
                .Invoking(async () => await reclaimService.ReclaimMannequin(mannequinUser, null, targetUser, githubOrg, false))
                .Should().ThrowAsync<OctoshiftCliException>();

            mockGithub.Verify(x => x.ReclaimMannequin(githubOrgId, mannequinUserId, targetUserId), Times.Never());
            mockGithub.Verify(x => x.GetUserId(targetUser), Times.Never());
        }
    }
}
