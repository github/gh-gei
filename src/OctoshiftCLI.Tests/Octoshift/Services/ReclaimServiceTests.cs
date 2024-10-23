using System;
using System.Threading.Tasks;
using FluentAssertions;
using Moq;
using Octoshift.Models;
using OctoshiftCLI.Models;
using OctoshiftCLI.Services;
using Xunit;

namespace OctoshiftCLI.Tests.Octoshift.Services;

public class ReclaimServiceTests
{
    private readonly Mock<GithubApi> _mockGithubApi = TestHelpers.CreateMock<GithubApi>();
    private readonly Mock<OctoLogger> _mockOctoLogger = TestHelpers.CreateMock<OctoLogger>();

    private readonly ReclaimService _service;

    private const string TARGET_ORG = "FOO-TARGET-ORG";
    private const string HEADER = "mannequin-user,mannequin-id,target-user";
    private readonly string ORG_ID = Guid.NewGuid().ToString();
    private readonly string MANNEQUIN_ID = Guid.NewGuid().ToString();
    private readonly string MANNEQUIN_LOGIN = "mona";
    private readonly string TARGET_USER_ID = Guid.NewGuid().ToString();
    private readonly string TARGET_USER_LOGIN = "mona_gh";

    public ReclaimServiceTests()
    {
        _service = new ReclaimService(_mockGithubApi.Object, _mockOctoLogger.Object);
    }

    [Fact]
    public async Task ReclaimMannequins_AlreadyMapped_Force_Reclaim()
    {
        var mannequinsResponse = new[]
        {
            new Mannequin
            {
                Id = MANNEQUIN_ID, Login = MANNEQUIN_LOGIN, MappedUser = new Claimant { Id = TARGET_USER_ID, Login = TARGET_USER_LOGIN }
            }
        };

        var reclaimMannequinResponse = new CreateAttributionInvitationResult()
        {
            Data = new CreateAttributionInvitationData()
            {
                CreateAttributionInvitation = new CreateAttributionInvitation()
                {
                    Source = new UserInfo() { Id = MANNEQUIN_ID, Login = MANNEQUIN_LOGIN },
                    Target = new UserInfo() { Id = TARGET_USER_ID, Login = TARGET_USER_LOGIN }
                }
            }
        };

        _mockGithubApi.Setup(x => x.GetOrganizationId(TARGET_ORG).Result).Returns(ORG_ID);
        _mockGithubApi.Setup(x => x.GetMannequins(ORG_ID).Result).Returns(mannequinsResponse);
        _mockGithubApi.Setup(x => x.GetUserId(TARGET_USER_LOGIN).Result).Returns(TARGET_USER_ID);
        _mockGithubApi.Setup(x => x.CreateAttributionInvitation(ORG_ID, MANNEQUIN_ID, TARGET_USER_ID).Result).Returns(reclaimMannequinResponse);

        var csvContent = new string[] {
            HEADER,
            $"{MANNEQUIN_LOGIN},{MANNEQUIN_ID},{TARGET_USER_LOGIN}"
        };

        // Act
        await _service.ReclaimMannequins(csvContent, TARGET_ORG, true, false);

        // Assert
        _mockGithubApi.Verify(m => m.GetOrganizationId(TARGET_ORG), Times.Once);
        _mockGithubApi.Verify(m => m.GetMannequins(ORG_ID), Times.Once);
        _mockGithubApi.Verify(x => x.CreateAttributionInvitation(ORG_ID, MANNEQUIN_ID, TARGET_USER_ID), Times.Once);
        _mockGithubApi.Verify(x => x.GetUserId(TARGET_USER_LOGIN), Times.Once);
        _mockGithubApi.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task ReclaimMannequins_Duplicates_Same_Claimant_Throws_Error()
    {
        var mannequinsResponse = new[]
        {
            new Mannequin
            {
                Id = MANNEQUIN_ID, Login = MANNEQUIN_LOGIN, MappedUser = new Claimant { Id = TARGET_USER_ID, Login = TARGET_USER_LOGIN }
            }
        };

        var reclaimMannequinResponse = new CreateAttributionInvitationResult()
        {
            Data = new CreateAttributionInvitationData()
            {
                CreateAttributionInvitation = new CreateAttributionInvitation()
                {
                    Source = new UserInfo() { Id = MANNEQUIN_ID, Login = MANNEQUIN_LOGIN },
                    Target = new UserInfo() { Id = TARGET_USER_ID, Login = TARGET_USER_LOGIN }
                }
            }
        };

        _mockGithubApi.Setup(x => x.GetOrganizationId(TARGET_ORG).Result).Returns(ORG_ID);
        _mockGithubApi.Setup(x => x.GetMannequins(ORG_ID).Result).Returns(mannequinsResponse);
        _mockGithubApi.Setup(x => x.GetUserId(TARGET_USER_LOGIN).Result).Returns(TARGET_USER_ID);
        _mockGithubApi.Setup(x => x.CreateAttributionInvitation(ORG_ID, MANNEQUIN_ID, TARGET_USER_ID).Result).Returns(reclaimMannequinResponse);

        var csvContent = new string[] {
            HEADER,
            $"{MANNEQUIN_LOGIN},{MANNEQUIN_ID},{TARGET_USER_LOGIN}",
            $"{MANNEQUIN_LOGIN},{MANNEQUIN_ID},{TARGET_USER_LOGIN}"
        };

        // Act
        await _service.ReclaimMannequins(csvContent, TARGET_ORG, true, false);

        // Assert
        _mockGithubApi.Verify(m => m.GetOrganizationId(TARGET_ORG), Times.Once);
        _mockGithubApi.Verify(m => m.GetMannequins(ORG_ID), Times.Once);
        _mockGithubApi.Verify(x => x.CreateAttributionInvitation(ORG_ID, MANNEQUIN_ID, TARGET_USER_ID), Times.Never);
        _mockGithubApi.Verify(x => x.GetUserId(TARGET_USER_LOGIN), Times.Never);
        _mockGithubApi.VerifyNoOtherCalls();
        _mockOctoLogger.Verify(x => x.LogWarning($"Mannequin {MANNEQUIN_LOGIN} is a duplicate. Skipping."), Times.Exactly(2));
    }

    [Fact]
    public async Task ReclaimMannequins_Duplicates_Different_Claimants_Throws_Error()
    {
        var TARGET_USER_ID_2 = Guid.NewGuid().ToString();
        var TARGET_USER_LOGIN_2 = "mona_gh_2";

        var mannequinsResponse = new[]
        {
            new Mannequin
            {
                Id = MANNEQUIN_ID, Login = MANNEQUIN_LOGIN, MappedUser = new Claimant { Id = TARGET_USER_ID, Login = TARGET_USER_LOGIN }
            },
            new Mannequin
            {
                Id = MANNEQUIN_ID, Login = MANNEQUIN_LOGIN, MappedUser = new Claimant { Id = TARGET_USER_ID_2, Login = TARGET_USER_LOGIN_2 }
            }
        };

        var reclaimMannequinResponse = new CreateAttributionInvitationResult()
        {
            Data = new CreateAttributionInvitationData()
            {
                CreateAttributionInvitation = new CreateAttributionInvitation()
                {
                    Source = new UserInfo() { Id = MANNEQUIN_ID, Login = MANNEQUIN_LOGIN },
                    Target = new UserInfo() { Id = TARGET_USER_ID, Login = TARGET_USER_LOGIN }
                }
            }
        };

        _mockGithubApi.Setup(x => x.GetOrganizationId(TARGET_ORG).Result).Returns(ORG_ID);
        _mockGithubApi.Setup(x => x.GetMannequins(ORG_ID).Result).Returns(mannequinsResponse);
        _mockGithubApi.Setup(x => x.GetUserId(TARGET_USER_LOGIN).Result).Returns(TARGET_USER_ID);
        _mockGithubApi.Setup(x => x.CreateAttributionInvitation(ORG_ID, MANNEQUIN_ID, TARGET_USER_ID).Result).Returns(reclaimMannequinResponse);

        var csvContent = new string[] {
            HEADER,
            $"{MANNEQUIN_LOGIN},{MANNEQUIN_ID},{TARGET_USER_LOGIN}",
            $"{MANNEQUIN_LOGIN},{MANNEQUIN_ID},ADiffClaimant"
        };

        // Act
        await _service.ReclaimMannequins(csvContent, TARGET_ORG, true, false);

        // Assert
        _mockGithubApi.Verify(m => m.GetOrganizationId(TARGET_ORG), Times.Once);
        _mockGithubApi.Verify(m => m.GetMannequins(ORG_ID), Times.Once);
        _mockGithubApi.Verify(x => x.CreateAttributionInvitation(ORG_ID, MANNEQUIN_ID, TARGET_USER_ID), Times.Never);
        _mockGithubApi.Verify(x => x.GetUserId(TARGET_USER_LOGIN), Times.Never);
        _mockGithubApi.VerifyNoOtherCalls();
        _mockOctoLogger.Verify(x => x.LogWarning($"Mannequin {MANNEQUIN_LOGIN} is a duplicate. Skipping."), Times.Exactly(2));
    }

    [Fact]
    public async Task ReclaimMannequins_EmptyCSV_NoReclaims_IssuesWarning()
    {
        _mockGithubApi.Setup(x => x.GetOrganizationId(TARGET_ORG).Result).Returns(ORG_ID);

        var csvContent = Array.Empty<string>();

        // Act
        await _service.ReclaimMannequins(csvContent, TARGET_ORG, false, false);

        // Assert
        _mockGithubApi.VerifyNoOtherCalls();
        _mockOctoLogger.Verify(m => m.LogWarning("File is empty. Nothing to reclaim"), Times.Once);
    }

    [Fact]
    public async Task ReclaimMannequins_InvalidCSVContent_IssuesError()
    {
        _mockGithubApi.Setup(x => x.GetOrganizationId(TARGET_ORG).Result).Returns(ORG_ID);

        var csvContent = new string[] {
            HEADER,
            "login"  // invalid line
        };

        // Act
        await _service.ReclaimMannequins(csvContent, TARGET_ORG, false, false);

        // Assert
        _mockGithubApi.Verify(m => m.GetOrganizationId(TARGET_ORG), Times.Once);
        _mockGithubApi.Verify(m => m.GetMannequins(ORG_ID), Times.Once);
        _mockGithubApi.Verify(x => x.CreateAttributionInvitation(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never);
        _mockGithubApi.Verify(x => x.GetUserId(It.IsAny<string>()), Times.Never);
        _mockOctoLogger.Verify(m => m.LogWarning($"Invalid line: \"login\". Will ignore it."), Times.Once);
        _mockGithubApi.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task ReclaimMannequins_InvalidCSVHeader_OctoshiftCliException()
    {
        var csvContent = new string[] {
            "INVALID_HEADER"
        };

        // Act
        await FluentActions
            .Invoking(async () => await _service.ReclaimMannequins(csvContent, TARGET_ORG, false, false))
            .Should().ThrowAsync<OctoshiftCliException>();
    }

    [Fact]
    public async Task ReclaimMannequins_InvalidCSVContentLoginNotSpecified_IssuesError()
    {
        _mockGithubApi.Setup(x => x.GetOrganizationId(TARGET_ORG).Result).Returns(ORG_ID);

        var csvContent = new string[] {
            HEADER,
            ",,mona_gh"  // invalid line
        };

        // Act
        await _service.ReclaimMannequins(csvContent, TARGET_ORG, false, false);

        // Assert
        _mockGithubApi.Verify(m => m.GetOrganizationId(TARGET_ORG), Times.Once);
        _mockGithubApi.Verify(m => m.GetMannequins(ORG_ID), Times.Once);
        _mockGithubApi.Verify(x => x.CreateAttributionInvitation(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never);
        _mockGithubApi.Verify(x => x.GetUserId(It.IsAny<string>()), Times.Never);
        _mockOctoLogger.Verify(m => m.LogWarning($"Invalid line: \",,mona_gh\". Mannequin login is not defined. Will ignore it."), Times.Once);
        _mockGithubApi.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task ReclaimMannequins_InvalidCSVContentTargetLoginNotSpecified_IssuesError()
    {
        _mockGithubApi.Setup(x => x.GetOrganizationId(TARGET_ORG).Result).Returns(ORG_ID);

        var csvContent = new string[] {
            HEADER,
            "xx,id,"  // invalid line
        };

        // Act
        await _service.ReclaimMannequins(csvContent, TARGET_ORG, false, false);

        // Assert
        _mockGithubApi.Verify(m => m.GetOrganizationId(TARGET_ORG), Times.Once);
        _mockGithubApi.Verify(m => m.GetMannequins(ORG_ID), Times.Once);
        _mockGithubApi.Verify(x => x.CreateAttributionInvitation(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never);
        _mockGithubApi.Verify(x => x.GetUserId(It.IsAny<string>()), Times.Never);
        _mockOctoLogger.Verify(m => m.LogWarning("Invalid line: \"xx,id,\". Target User is not defined. Will ignore it."), Times.Once);
        _mockGithubApi.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task ReclaimMannequins_InvalidCSVContentClaimantLoginNotSpecified_IssuesError()
    {
        _mockGithubApi.Setup(x => x.GetOrganizationId(TARGET_ORG).Result).Returns(ORG_ID);

        var csvContent = new string[] {
            HEADER,
            "mona,,"  // invalid line
        };

        // Act
        await _service.ReclaimMannequins(csvContent, TARGET_ORG, false, false);

        // Assert
        _mockGithubApi.Verify(m => m.GetOrganizationId(TARGET_ORG), Times.Once);
        _mockGithubApi.Verify(m => m.GetMannequins(ORG_ID), Times.Once);
        _mockGithubApi.Verify(x => x.CreateAttributionInvitation(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never);
        _mockGithubApi.Verify(x => x.GetUserId(It.IsAny<string>()), Times.Never);
        _mockOctoLogger.Verify(m => m.LogWarning($"Invalid line: \"mona,,\". Mannequin Id is not defined. Will ignore it."), Times.Once);
        _mockGithubApi.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task ReclaimMannequins_OneUnreclaimedMannequin_Reclaim()
    {
        var mannequinsResponse = new[]
        {
            new Mannequin { Id = MANNEQUIN_ID, Login = MANNEQUIN_LOGIN }
        };

        var reclaimMannequinResponse = new CreateAttributionInvitationResult()
        {
            Data = new CreateAttributionInvitationData()
            {
                CreateAttributionInvitation = new CreateAttributionInvitation()
                {
                    Source = new UserInfo()
                    {
                        Id = MANNEQUIN_ID,
                        Login = MANNEQUIN_LOGIN
                    },
                    Target = new UserInfo()
                    {
                        Id = TARGET_USER_ID,
                        Login = TARGET_USER_LOGIN
                    }
                }
            }
        };

        _mockGithubApi.Setup(x => x.GetOrganizationId(TARGET_ORG).Result).Returns(ORG_ID);
        _mockGithubApi.Setup(x => x.GetMannequins(ORG_ID).Result).Returns(mannequinsResponse);
        _mockGithubApi.Setup(x => x.GetUserId(TARGET_USER_LOGIN).Result).Returns(TARGET_USER_ID);
        _mockGithubApi.Setup(x => x.CreateAttributionInvitation(ORG_ID, MANNEQUIN_ID, TARGET_USER_ID).Result).Returns(reclaimMannequinResponse);

        var csvContent = new string[] {
            HEADER,
            $"{MANNEQUIN_LOGIN},{MANNEQUIN_ID},{TARGET_USER_LOGIN}"
        };

        // Act
        await _service.ReclaimMannequins(csvContent, TARGET_ORG, false, false);

        // Assert
        _mockGithubApi.Verify(m => m.GetOrganizationId(TARGET_ORG), Times.Once);
        _mockGithubApi.Verify(m => m.GetMannequins(ORG_ID), Times.Once);
        _mockGithubApi.Verify(x => x.CreateAttributionInvitation(ORG_ID, MANNEQUIN_ID, TARGET_USER_ID), Times.Once);
        _mockGithubApi.Verify(x => x.GetUserId(TARGET_USER_LOGIN), Times.Once);
        _mockGithubApi.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task ReclaimMannequins_TwoUnreclaimedMannequin_LinesWithWhitespace_Reclaim()
    {
        var mannequinId2 = "mannequin2id";
        var mannequinLogin2 = "lisa";
        var reclaimantId2 = "reclaimant2id";
        var reclaimantLogin2 = "lisa_gh";

        var mannequinsResponse = new[]
        {
            new Mannequin { Id = MANNEQUIN_ID,Login = MANNEQUIN_LOGIN },
            new Mannequin {Id = mannequinId2, Login = mannequinLogin2 }
        };

        var reclaimMannequinResponse = new CreateAttributionInvitationResult()
        {
            Data = new CreateAttributionInvitationData()
            {
                CreateAttributionInvitation = new CreateAttributionInvitation()
                {
                    Source = new UserInfo() { Id = MANNEQUIN_ID, Login = MANNEQUIN_LOGIN },
                    Target = new UserInfo() { Id = TARGET_USER_ID, Login = TARGET_USER_LOGIN }
                }
            }
        };

        var reclaimMannequinResponse2 = new CreateAttributionInvitationResult()
        {
            Data = new CreateAttributionInvitationData()
            {
                CreateAttributionInvitation = new CreateAttributionInvitation()
                {
                    Source = new UserInfo() { Id = mannequinId2, Login = mannequinLogin2 },
                    Target = new UserInfo() { Id = reclaimantId2, Login = reclaimantLogin2 }
                }
            }
        };

        _mockGithubApi.Setup(x => x.GetOrganizationId(TARGET_ORG).Result).Returns(ORG_ID);
        _mockGithubApi.Setup(x => x.GetMannequins(ORG_ID).Result).Returns(mannequinsResponse);
        _mockGithubApi.Setup(x => x.GetUserId(TARGET_USER_LOGIN).Result).Returns(TARGET_USER_ID);
        _mockGithubApi.Setup(x => x.GetUserId(reclaimantLogin2).Result).Returns(reclaimantId2);
        _mockGithubApi.Setup(x => x.CreateAttributionInvitation(ORG_ID, MANNEQUIN_ID, TARGET_USER_ID).Result).Returns(reclaimMannequinResponse);
        _mockGithubApi.Setup(x => x.CreateAttributionInvitation(ORG_ID, mannequinId2, reclaimantId2).Result).Returns(reclaimMannequinResponse2);

        var csvContent = new string[] {
            HEADER,
            $"{MANNEQUIN_LOGIN},{MANNEQUIN_ID},{TARGET_USER_LOGIN}",
            "", // Empty Line
            " ", // whitespace line
            $"{mannequinLogin2},{mannequinId2},{reclaimantLogin2}"
        };

        // Act
        await _service.ReclaimMannequins(csvContent, TARGET_ORG, false, false);

        // Assert
        _mockGithubApi.Verify(m => m.GetOrganizationId(TARGET_ORG), Times.Once);
        _mockGithubApi.Verify(m => m.GetMannequins(ORG_ID), Times.Once);
        _mockGithubApi.Verify(x => x.CreateAttributionInvitation(ORG_ID, MANNEQUIN_ID, TARGET_USER_ID), Times.Once);
        _mockGithubApi.Verify(x => x.CreateAttributionInvitation(ORG_ID, mannequinId2, reclaimantId2), Times.Once);
        _mockGithubApi.Verify(x => x.GetUserId(TARGET_USER_LOGIN), Times.Once);
        _mockGithubApi.Verify(x => x.GetUserId(reclaimantLogin2), Times.Once);
        _mockGithubApi.Verify(x => x.GetUserId(reclaimantLogin2), Times.Once);
        _mockGithubApi.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task ReclaimMannequins_SameLoginDifferentIds_MapSameUser_Reclaim()
    {
        var mannequinId2 = "mannequin2id";
        var mannequinLogin2 = "mona";

        var mannequinsResponse = new[]
        {
            new Mannequin { Id = MANNEQUIN_ID, Login = MANNEQUIN_LOGIN },
            new Mannequin { Id = mannequinId2, Login = mannequinLogin2 }
        };

        var reclaimMannequinResponse = new CreateAttributionInvitationResult()
        {
            Data = new CreateAttributionInvitationData()
            {
                CreateAttributionInvitation = new CreateAttributionInvitation()
                {
                    Source = new UserInfo() { Id = MANNEQUIN_ID, Login = MANNEQUIN_LOGIN },
                    Target = new UserInfo() { Id = TARGET_USER_ID, Login = TARGET_USER_LOGIN }
                }
            }
        };

        var reclaimMannequinResponse2 = new CreateAttributionInvitationResult()
        {
            Data = new CreateAttributionInvitationData()
            {
                CreateAttributionInvitation = new CreateAttributionInvitation()
                {
                    Source = new UserInfo() { Id = mannequinId2, Login = mannequinLogin2 },
                    Target = new UserInfo() { Id = TARGET_USER_ID, Login = TARGET_USER_LOGIN }
                }
            }
        };

        _mockGithubApi.Setup(x => x.GetOrganizationId(TARGET_ORG).Result).Returns(ORG_ID);
        _mockGithubApi.Setup(x => x.GetMannequins(ORG_ID).Result).Returns(mannequinsResponse);
        _mockGithubApi.Setup(x => x.GetUserId(TARGET_USER_LOGIN).Result).Returns(TARGET_USER_ID);
        _mockGithubApi.Setup(x => x.CreateAttributionInvitation(ORG_ID, MANNEQUIN_ID, TARGET_USER_ID).Result).Returns(reclaimMannequinResponse);
        _mockGithubApi.Setup(x => x.CreateAttributionInvitation(ORG_ID, mannequinId2, TARGET_USER_ID).Result).Returns(reclaimMannequinResponse2);

        var csvContent = new string[] {
            HEADER,
            $"{MANNEQUIN_LOGIN},{MANNEQUIN_ID},{TARGET_USER_LOGIN}",
            $"{mannequinLogin2},{mannequinId2},{TARGET_USER_LOGIN}"
        };

        // Act
        await _service.ReclaimMannequins(csvContent, TARGET_ORG, false, false);

        // Assert
        _mockGithubApi.Verify(m => m.GetOrganizationId(TARGET_ORG), Times.Once);
        _mockGithubApi.Verify(m => m.GetMannequins(ORG_ID), Times.Once);
        _mockGithubApi.Verify(x => x.CreateAttributionInvitation(ORG_ID, MANNEQUIN_ID, TARGET_USER_ID), Times.Once);
        _mockGithubApi.Verify(x => x.CreateAttributionInvitation(ORG_ID, mannequinId2, TARGET_USER_ID), Times.Once);
        _mockGithubApi.Verify(x => x.GetUserId(TARGET_USER_LOGIN), Times.Exactly(2));
        _mockGithubApi.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task ReclaimMannequins_AlreadyMapped_No_Reclaim_ShowsError()
    {
        var mannequinsResponse = new[]
        {
            new Mannequin
            {
                Id = MANNEQUIN_ID,
                Login = MANNEQUIN_LOGIN,
                MappedUser = new Claimant
                {
                    Id = TARGET_USER_ID,
                    Login = TARGET_USER_LOGIN
                }
            }
        };

        _mockGithubApi.Setup(x => x.GetOrganizationId(TARGET_ORG).Result).Returns(ORG_ID);
        _mockGithubApi.Setup(x => x.GetMannequins(ORG_ID).Result).Returns(mannequinsResponse);

        var csvContent = new string[] {
            HEADER,
            $"{MANNEQUIN_LOGIN},{MANNEQUIN_ID},{TARGET_USER_LOGIN}"
        };

        // Act
        await _service.ReclaimMannequins(csvContent, TARGET_ORG, false, false);

        // Assert
        _mockGithubApi.Verify(m => m.GetOrganizationId(TARGET_ORG), Times.Once);
        _mockGithubApi.Verify(m => m.GetMannequins(ORG_ID), Times.Once);
        _mockGithubApi.Verify(x => x.CreateAttributionInvitation(ORG_ID, MANNEQUIN_ID, TARGET_USER_ID), Times.Never);
        _mockGithubApi.Verify(x => x.GetUserId(TARGET_USER_LOGIN), Times.Never);
        _mockOctoLogger.Verify(x => x.LogWarning($"{MANNEQUIN_LOGIN} is already claimed. Skipping (use force if you want to reclaim)"), Times.Once);
        _mockGithubApi.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task ReclaimMannequins_NoExistantMannequin_No_Reclaim_IssuesError()
    {
        var mannequinsResponse = Array.Empty<Mannequin>();

        _mockGithubApi.Setup(x => x.GetOrganizationId(TARGET_ORG).Result).Returns(ORG_ID);
        _mockGithubApi.Setup(x => x.GetMannequins(ORG_ID).Result).Returns(mannequinsResponse);

        var csvContent = new string[] {
            HEADER,
            $"{MANNEQUIN_LOGIN},{MANNEQUIN_ID},{TARGET_USER_LOGIN}"
        };

        // Act
        await _service.ReclaimMannequins(csvContent, TARGET_ORG, false, false);

        // Assert
        _mockGithubApi.Verify(m => m.GetOrganizationId(TARGET_ORG), Times.Once);
        _mockGithubApi.Verify(m => m.GetMannequins(ORG_ID), Times.Once);
        _mockGithubApi.Verify(x => x.CreateAttributionInvitation(ORG_ID, MANNEQUIN_ID, TARGET_USER_ID), Times.Never);
        _mockGithubApi.Verify(x => x.GetUserId(TARGET_USER_LOGIN), Times.Never);
        _mockOctoLogger.Verify(x => x.LogWarning($"Mannequin {MANNEQUIN_LOGIN} not found. Skipping."), Times.Once);
        _mockGithubApi.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task ReclaimMannequins_NoTarget_No_Reclaim_IssuesError()
    {
        var mannequinsResponse = new Mannequin[] {
            new Mannequin { Id = MANNEQUIN_ID, Login = MANNEQUIN_LOGIN}
        };

        _mockGithubApi.Setup(x => x.GetOrganizationId(TARGET_ORG).Result).Returns(ORG_ID);
        _mockGithubApi.Setup(x => x.GetMannequins(ORG_ID).Result).Returns(mannequinsResponse);
        _mockGithubApi.Setup(x => x.GetUserId(TARGET_USER_LOGIN)).Throws(new OctoshiftCliException($"Could not resolve to a User with the login of '{MANNEQUIN_LOGIN}'."));

        var csvContent = new string[] {
            HEADER,
            $"{MANNEQUIN_LOGIN},{MANNEQUIN_ID},{TARGET_USER_LOGIN}"
        };

        // Act
        await _service.ReclaimMannequins(csvContent, TARGET_ORG, false, false);

        // Assert
        _mockGithubApi.Verify(m => m.GetOrganizationId(TARGET_ORG), Times.Once);
        _mockGithubApi.Verify(m => m.GetMannequins(ORG_ID), Times.Once);
        _mockGithubApi.Verify(x => x.CreateAttributionInvitation(ORG_ID, MANNEQUIN_ID, TARGET_USER_ID), Times.Never);
        _mockGithubApi.Verify(x => x.GetUserId(TARGET_USER_LOGIN), Times.Once);
        _mockOctoLogger.Verify(x => x.LogWarning($"Claimant \"{TARGET_USER_LOGIN}\" not found. Will ignore it."), Times.Once);
        _mockGithubApi.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task ReclaimMannequinsSkipInvitation_Happy_Path()
    {
        var mannequinsResponse = new Mannequin[] {
            new Mannequin { Id = MANNEQUIN_ID, Login = MANNEQUIN_LOGIN}
        };

        var reclaimMannequinResponse = new ReattributeMannequinToUserResult()
        {
            Data = new ReattributeMannequinToUserData()
            {
                ReattributeMannequinToUser = new ReattributeMannequinToUser()
                {
                    Source = new UserInfo() { Id = MANNEQUIN_ID, Login = MANNEQUIN_LOGIN },
                    Target = new UserInfo() { Id = TARGET_USER_ID, Login = TARGET_USER_LOGIN }
                }
            }
        };

        _mockGithubApi.Setup(x => x.GetOrganizationId(TARGET_ORG).Result).Returns(ORG_ID);
        _mockGithubApi.Setup(x => x.GetMannequins(ORG_ID).Result).Returns(mannequinsResponse);
        _mockGithubApi.Setup(x => x.GetUserId(TARGET_USER_LOGIN).Result).Returns(TARGET_USER_ID);
        _mockGithubApi.Setup(x => x.ReclaimMannequinSkipInvitation(ORG_ID, MANNEQUIN_ID, TARGET_USER_ID).Result).Returns(reclaimMannequinResponse);

        var csvContent = new string[] {
            HEADER,
            $"{MANNEQUIN_LOGIN},{MANNEQUIN_ID},{TARGET_USER_LOGIN}"
        };

        // Act
        await _service.ReclaimMannequins(csvContent, TARGET_ORG, false, true);

        // Assert
        _mockGithubApi.Verify(m => m.GetOrganizationId(TARGET_ORG), Times.Once);
        _mockGithubApi.Verify(m => m.GetMannequins(ORG_ID), Times.Once);
        _mockGithubApi.Verify(x => x.CreateAttributionInvitation(ORG_ID, MANNEQUIN_ID, TARGET_USER_ID), Times.Never);
        _mockGithubApi.Verify(x => x.ReclaimMannequinSkipInvitation(ORG_ID, MANNEQUIN_ID, TARGET_USER_ID), Times.Once);
        _mockGithubApi.Verify(x => x.GetUserId(TARGET_USER_LOGIN), Times.Once);
        _mockGithubApi.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task ReclaimMannequinsSkipInvitation_No_EMU_Throws_Error_Fails_Fast()
    {
        var mannequinsResponse = new Mannequin[] {
            new Mannequin { Id = MANNEQUIN_ID, Login = MANNEQUIN_LOGIN}
        };

        var reclaimMannequinResponse = new ReattributeMannequinToUserResult()
        {
            Errors =
            [
                new ErrorData()
                {
                    Message = "is not an Enterprise Managed Users (EMU) organization"
                }
            ]
        };

        _mockGithubApi.Setup(x => x.GetOrganizationId(TARGET_ORG).Result).Returns(ORG_ID);
        _mockGithubApi.Setup(x => x.GetMannequins(ORG_ID).Result).Returns(mannequinsResponse);
        _mockGithubApi.Setup(x => x.GetUserId(TARGET_USER_LOGIN).Result).Returns(TARGET_USER_ID);
        _mockGithubApi.Setup(x => x.ReclaimMannequinSkipInvitation(ORG_ID, MANNEQUIN_ID, TARGET_USER_ID).Result).Returns(reclaimMannequinResponse);

        var csvContent = new string[] {
            HEADER,
            $"{MANNEQUIN_LOGIN},{MANNEQUIN_ID},{TARGET_USER_LOGIN}",
            "SecondLogin,SecondMannId,SecondTargetUserLogin",
            "ThirdLogin,ThirdMannId,ThirdTargetUserLogin"
        };

        // Act
        await _service.ReclaimMannequins(csvContent, TARGET_ORG, false, true);

        // Assert
        _mockGithubApi.Verify(m => m.GetOrganizationId(TARGET_ORG), Times.Once);
        _mockGithubApi.Verify(m => m.GetMannequins(ORG_ID), Times.Once);
        _mockGithubApi.Verify(x => x.CreateAttributionInvitation(ORG_ID, MANNEQUIN_ID, TARGET_USER_ID), Times.Never);
        _mockGithubApi.Verify(x => x.ReclaimMannequinSkipInvitation(ORG_ID, MANNEQUIN_ID, TARGET_USER_ID), Times.Once);
        _mockGithubApi.Verify(x => x.GetUserId(TARGET_USER_LOGIN), Times.Once);
        _mockGithubApi.VerifyNoOtherCalls();
        _mockOctoLogger.Verify(x => x.LogError("Failed to reclaim mannequins. The --skip-invitation flag is only available to EMU organizations."), Times.Once);
    }

    [Fact]
    public async Task ReclaimMannequin_TwoUsersSameLogin_AllReclaimed()
    {
        var mannequinUserId2 = "id2";

        var mannequinsResponse = new Mannequin[] {
            new Mannequin { Id = MANNEQUIN_ID, Login = MANNEQUIN_LOGIN},
            new Mannequin { Id = mannequinUserId2, Login = MANNEQUIN_LOGIN},
        };

        var reclaimMannequinResponse = new CreateAttributionInvitationResult()
        {
            Data = new CreateAttributionInvitationData()
            {
                CreateAttributionInvitation = new CreateAttributionInvitation()
                {
                    Source = new UserInfo() { Id = MANNEQUIN_ID, Login = MANNEQUIN_LOGIN },
                    Target = new UserInfo() { Id = TARGET_USER_ID, Login = TARGET_USER_LOGIN }
                }
            }
        };

        var reclaimMannequinResponse2 = new CreateAttributionInvitationResult()
        {
            Data = new CreateAttributionInvitationData()
            {
                CreateAttributionInvitation = new CreateAttributionInvitation()
                {
                    Source = new UserInfo() { Id = mannequinUserId2, Login = MANNEQUIN_LOGIN },
                    Target = new UserInfo() { Id = TARGET_USER_ID, Login = TARGET_USER_LOGIN }
                }
            }
        };

        _mockGithubApi.Setup(x => x.GetOrganizationId(TARGET_ORG).Result).Returns(ORG_ID);
        _mockGithubApi.Setup(x => x.GetMannequinsByLogin(ORG_ID, MANNEQUIN_LOGIN).Result).Returns(mannequinsResponse);
        _mockGithubApi.Setup(x => x.GetUserId(TARGET_USER_LOGIN).Result).Returns(TARGET_USER_ID);
        _mockGithubApi.Setup(x => x.CreateAttributionInvitation(ORG_ID, MANNEQUIN_ID, TARGET_USER_ID).Result).Returns(reclaimMannequinResponse);
        _mockGithubApi.Setup(x => x.CreateAttributionInvitation(ORG_ID, mannequinUserId2, TARGET_USER_ID).Result).Returns(reclaimMannequinResponse2);

        // Act
        await _service.ReclaimMannequin(MANNEQUIN_LOGIN, null, TARGET_USER_LOGIN, TARGET_ORG, false, false);

        // Assert
        _mockGithubApi.Verify(x => x.CreateAttributionInvitation(ORG_ID, MANNEQUIN_ID, TARGET_USER_ID), Times.Once);
        _mockGithubApi.Verify(x => x.CreateAttributionInvitation(ORG_ID, mannequinUserId2, TARGET_USER_ID), Times.Once);
        _mockGithubApi.Verify(x => x.GetUserId(TARGET_USER_LOGIN), Times.Once);
    }

    [Fact]
    public async Task ReclaimMannequin_Happy_Path()
    {
        var mannequinsResponse = new Mannequin[] {
            new Mannequin { Id = MANNEQUIN_ID, Login = MANNEQUIN_LOGIN}
        };

        var reclaimMannequinResponse = new CreateAttributionInvitationResult()
        {
            Data = new CreateAttributionInvitationData()
            {
                CreateAttributionInvitation = new CreateAttributionInvitation()
                {
                    Source = new UserInfo() { Id = MANNEQUIN_ID, Login = MANNEQUIN_LOGIN },
                    Target = new UserInfo() { Id = TARGET_USER_ID, Login = TARGET_USER_LOGIN }
                }
            }
        };

        _mockGithubApi.Setup(x => x.GetOrganizationId(TARGET_ORG).Result).Returns(ORG_ID);
        _mockGithubApi.Setup(x => x.GetMannequinsByLogin(ORG_ID, MANNEQUIN_LOGIN).Result).Returns(mannequinsResponse);
        _mockGithubApi.Setup(x => x.GetUserId(TARGET_USER_LOGIN).Result).Returns(TARGET_USER_ID);
        _mockGithubApi.Setup(x => x.CreateAttributionInvitation(ORG_ID, MANNEQUIN_ID, TARGET_USER_ID).Result).Returns(reclaimMannequinResponse);

        // Act
        await _service.ReclaimMannequin(MANNEQUIN_LOGIN, null, TARGET_USER_LOGIN, TARGET_ORG, false, false);

        _mockGithubApi.Verify(x => x.CreateAttributionInvitation(ORG_ID, MANNEQUIN_ID, TARGET_USER_ID), Times.Once);
    }

    [Fact]
    public async Task ReclaimMannequin_Duplicates_ReclaimOnlyOnce()
    {
        var mannequinsResponse = new Mannequin[] {
            new Mannequin { Id = MANNEQUIN_ID, Login = "mona"},
            new Mannequin { Id = MANNEQUIN_ID, Login = "mona"}
        };

        var reclaimMannequinResponse = new CreateAttributionInvitationResult()
        {
            Data = new CreateAttributionInvitationData()
            {
                CreateAttributionInvitation = new CreateAttributionInvitation()
                {
                    Source = new UserInfo() { Id = MANNEQUIN_ID, Login = MANNEQUIN_LOGIN },
                    Target = new UserInfo() { Id = TARGET_USER_ID, Login = TARGET_USER_LOGIN }
                }
            }
        };

        _mockGithubApi.Setup(x => x.GetOrganizationId(TARGET_ORG).Result).Returns(ORG_ID);
        _mockGithubApi.Setup(x => x.GetMannequinsByLogin(ORG_ID, MANNEQUIN_LOGIN).Result).Returns(mannequinsResponse);
        _mockGithubApi.Setup(x => x.GetUserId(TARGET_USER_LOGIN).Result).Returns(TARGET_USER_ID);
        _mockGithubApi.Setup(x => x.CreateAttributionInvitation(ORG_ID, MANNEQUIN_ID, TARGET_USER_ID).Result).Returns(reclaimMannequinResponse);

        // Act
        await _service.ReclaimMannequin(MANNEQUIN_LOGIN, null, TARGET_USER_LOGIN, TARGET_ORG, false, false);

        _mockGithubApi.Verify(x => x.CreateAttributionInvitation(ORG_ID, MANNEQUIN_ID, TARGET_USER_ID), Times.Once);
    }

    [Fact]
    public async Task ReclaimMannequin_Mannequins_ReclaimOnlySpecifiedId()
    {
        var mannequinUserId2 = Guid.NewGuid().ToString();

        var mannequinsResponse = new Mannequin[] {
            new Mannequin { Id = MANNEQUIN_ID, Login = "mona"},
            new Mannequin { Id = mannequinUserId2, Login = "mona"}
        };

        var reclaimMannequinResponse = new CreateAttributionInvitationResult()
        {
            Data = new CreateAttributionInvitationData()
            {
                CreateAttributionInvitation = new CreateAttributionInvitation()
                {
                    Source = new UserInfo() { Id = MANNEQUIN_ID, Login = MANNEQUIN_LOGIN },
                    Target = new UserInfo() { Id = TARGET_USER_ID, Login = TARGET_USER_LOGIN }
                }
            }
        };
        var reclaimMannequinResponse2 = new CreateAttributionInvitationResult()
        {
            Data = new CreateAttributionInvitationData()
            {
                CreateAttributionInvitation = new CreateAttributionInvitation()
                {
                    Source = new UserInfo() { Id = mannequinUserId2, Login = MANNEQUIN_LOGIN },
                    Target = new UserInfo() { Id = TARGET_USER_ID, Login = TARGET_USER_LOGIN }
                }
            }
        };

        _mockGithubApi.Setup(x => x.GetOrganizationId(TARGET_ORG).Result).Returns(ORG_ID);
        _mockGithubApi.Setup(x => x.GetMannequinsByLogin(ORG_ID, MANNEQUIN_LOGIN).Result).Returns(mannequinsResponse);
        _mockGithubApi.Setup(x => x.GetUserId(TARGET_USER_LOGIN).Result).Returns(TARGET_USER_ID);
        _mockGithubApi.Setup(x => x.CreateAttributionInvitation(ORG_ID, MANNEQUIN_ID, TARGET_USER_ID).Result).Returns(reclaimMannequinResponse);
        _mockGithubApi.Setup(x => x.CreateAttributionInvitation(ORG_ID, mannequinUserId2, TARGET_USER_ID).Result).Returns(reclaimMannequinResponse2);

        // Act
        await _service.ReclaimMannequin(MANNEQUIN_LOGIN, MANNEQUIN_ID, TARGET_USER_LOGIN, TARGET_ORG, false, false);

        _mockGithubApi.Verify(x => x.CreateAttributionInvitation(ORG_ID, MANNEQUIN_ID, TARGET_USER_ID), Times.Once);
        _mockGithubApi.Verify(x => x.CreateAttributionInvitation(ORG_ID, mannequinUserId2, TARGET_USER_ID), Times.Never);
    }


    [Fact]
    public async Task ReclaimMannequin_Duplicates_ForceReclaimOnlyOnce()
    {
        var mannequinsResponse = new Mannequin[] {
            new Mannequin { Id = MANNEQUIN_ID, Login = "mona"},
            new Mannequin { Id = MANNEQUIN_ID, Login = "mona"},
            new Mannequin { Id = MANNEQUIN_ID, Login = "mona", MappedUser = new Claimant() { Id = TARGET_USER_ID, Login = TARGET_USER_LOGIN } }
        };

        var reclaimMannequinResponse = new CreateAttributionInvitationResult()
        {
            Data = new CreateAttributionInvitationData()
            {
                CreateAttributionInvitation = new CreateAttributionInvitation()
                {
                    Source = new UserInfo() { Id = MANNEQUIN_ID, Login = MANNEQUIN_LOGIN },
                    Target = new UserInfo() { Id = TARGET_USER_ID, Login = TARGET_USER_LOGIN }
                }
            }
        };

        _mockGithubApi.Setup(x => x.GetOrganizationId(TARGET_ORG).Result).Returns(ORG_ID);
        _mockGithubApi.Setup(x => x.GetMannequinsByLogin(ORG_ID, MANNEQUIN_LOGIN).Result).Returns(mannequinsResponse);
        _mockGithubApi.Setup(x => x.GetUserId(TARGET_USER_LOGIN).Result).Returns(TARGET_USER_ID);
        _mockGithubApi.Setup(x => x.CreateAttributionInvitation(ORG_ID, MANNEQUIN_ID, TARGET_USER_ID).Result).Returns(reclaimMannequinResponse);

        // Act
        await _service.ReclaimMannequin(MANNEQUIN_LOGIN, MANNEQUIN_ID, TARGET_USER_LOGIN, TARGET_ORG, true, false);

        _mockGithubApi.Verify(x => x.CreateAttributionInvitation(ORG_ID, MANNEQUIN_ID, TARGET_USER_ID), Times.Once);
    }

    [Fact]
    public async Task ReclaimMannequin_Duplicates_NoReclaimOneAlreadyReclaimed_OctoshiftCliException()
    {
        var mannequinsResponse = new Mannequin[] {
            new Mannequin { Id = MANNEQUIN_ID, Login = "mona"},
            new Mannequin { Id = MANNEQUIN_ID, Login = "mona"},
            new Mannequin { Id = MANNEQUIN_ID, Login = "mona",
                MappedUser = new Claimant() { Id = TARGET_USER_ID, Login = TARGET_USER_LOGIN }
            }
        };

        var reclaimMannequinResponse = new CreateAttributionInvitationResult()
        {
            Data = new CreateAttributionInvitationData()
            {
                CreateAttributionInvitation = new CreateAttributionInvitation()
                {
                    Source = new UserInfo() { Id = MANNEQUIN_ID, Login = MANNEQUIN_LOGIN },
                    Target = new UserInfo() { Id = TARGET_USER_ID, Login = TARGET_USER_LOGIN }
                }
            }
        };

        _mockGithubApi.Setup(x => x.GetOrganizationId(TARGET_ORG).Result).Returns(ORG_ID);
        _mockGithubApi.Setup(x => x.GetMannequinsByLogin(ORG_ID, MANNEQUIN_LOGIN).Result).Returns(mannequinsResponse);
        _mockGithubApi.Setup(x => x.GetUserId(TARGET_USER_LOGIN).Result).Returns(TARGET_USER_ID);
        _mockGithubApi.Setup(x => x.CreateAttributionInvitation(ORG_ID, null, TARGET_USER_ID).Result).Returns(reclaimMannequinResponse);

        var exception = await FluentActions
            .Invoking(async () => await _service.ReclaimMannequin(MANNEQUIN_LOGIN, null, TARGET_USER_LOGIN, TARGET_ORG, false, false))
            .Should().ThrowAsync<OctoshiftCliException>();
        exception.WithMessage("User mona is already mapped to a user. Use the force option if you want to reclaim the mannequin again.");

        _mockGithubApi.Verify(x => x.CreateAttributionInvitation(ORG_ID, MANNEQUIN_ID, TARGET_USER_ID), Times.Never);
    }

    [Fact]
    public async Task ReclaimMannequin_AlreadyMapped_No_Reclaim_Throws_OctoshiftCliException()
    {
        var mannequinsResponse = new Mannequin[] {
            new Mannequin { Id = MANNEQUIN_ID, Login = "mona",
                MappedUser = new Claimant { Id = "claimantId",Login = "claimant" }
            }
        };

        _mockGithubApi.Setup(x => x.GetOrganizationId(TARGET_ORG).Result).Returns(ORG_ID);
        _mockGithubApi.Setup(x => x.GetMannequinsByLogin(ORG_ID, MANNEQUIN_LOGIN).Result).Returns(mannequinsResponse);

        var reclaimService = new ReclaimService(_mockGithubApi.Object, TestHelpers.CreateMock<OctoLogger>().Object);

        // Act
        var exception = await FluentActions
            .Invoking(async () => await reclaimService.ReclaimMannequin(MANNEQUIN_LOGIN, null, TARGET_USER_LOGIN, TARGET_ORG, false, false))
            .Should().ThrowAsync<OctoshiftCliException>();

        _mockGithubApi.Verify(x => x.GetUserId(TARGET_USER_LOGIN), Times.Never());
        _mockGithubApi.Verify(x => x.CreateAttributionInvitation(ORG_ID, MANNEQUIN_ID, TARGET_USER_ID), Times.Never());
        exception.WithMessage("User mona is already mapped to a user. Use the force option if you want to reclaim the mannequin again.");
    }

    [Fact]
    public async Task ReclaimMannequin_AlreadyMapped_Force_Reclaim()
    {
        var mannequinsResponse = new Mannequin[] {
            new Mannequin
            {
                Id = MANNEQUIN_ID,
                Login = "mona",
                MappedUser = new Claimant
                {
                    Id = "claimantId",
                    Login = "claimant"
                }
            }
        };

        var reclaimMannequinResponse = new CreateAttributionInvitationResult()
        {
            Data = new CreateAttributionInvitationData()
            {
                CreateAttributionInvitation = new CreateAttributionInvitation()
                {
                    Source = new UserInfo() { Id = MANNEQUIN_ID, Login = MANNEQUIN_LOGIN },
                    Target = new UserInfo() { Id = TARGET_USER_ID, Login = TARGET_USER_LOGIN }
                }
            }
        };

        _mockGithubApi.Setup(x => x.GetOrganizationId(TARGET_ORG).Result).Returns(ORG_ID);
        _mockGithubApi.Setup(x => x.GetMannequinsByLogin(ORG_ID, MANNEQUIN_LOGIN).Result).Returns(mannequinsResponse);
        _mockGithubApi.Setup(x => x.GetUserId(TARGET_USER_LOGIN).Result).Returns(TARGET_USER_ID);
        _mockGithubApi.Setup(x => x.CreateAttributionInvitation(ORG_ID, MANNEQUIN_ID, TARGET_USER_ID).Result).Returns(reclaimMannequinResponse);

        // Act
        await _service.ReclaimMannequin(MANNEQUIN_LOGIN, null, TARGET_USER_LOGIN, TARGET_ORG, true, false);

        _mockGithubApi.Verify(x => x.CreateAttributionInvitation(ORG_ID, MANNEQUIN_ID, TARGET_USER_ID));
    }

    [Fact]
    public async Task ReclaimMannequin_FailedToReclaim_LogsError_Throws_OctoshiftCliException()
    {
        var failureMessage = "Target must be a member of the octocat organization";
        var mannequinsResponse = new Mannequin[] {
            new Mannequin { Id = MANNEQUIN_ID, Login = "mona" }
        };

        var reclaimMannequinResponse = new CreateAttributionInvitationResult()
        {
            Data = new CreateAttributionInvitationData()
            {
                CreateAttributionInvitation = null
            },
            Errors = [new ErrorData
            {
                Type = "UNPROCESSABLE",
                Message = failureMessage,
                Path = ["createAttributionInvitation"],
                Locations = [new Location() { Line = 2, Column = 14 }]
            }
            ]
        };

        _mockGithubApi.Setup(x => x.GetOrganizationId(TARGET_ORG).Result).Returns(ORG_ID);
        _mockGithubApi.Setup(x => x.GetMannequinsByLogin(ORG_ID, MANNEQUIN_LOGIN).Result).Returns(mannequinsResponse);
        _mockGithubApi.Setup(x => x.GetUserId(TARGET_USER_LOGIN).Result).Returns(TARGET_USER_ID);
        _mockGithubApi.Setup(x => x.CreateAttributionInvitation(ORG_ID, MANNEQUIN_ID, TARGET_USER_ID).Result).Returns(reclaimMannequinResponse);

        // Act
        var exception = await FluentActions
            .Invoking(async () => await _service.ReclaimMannequin(MANNEQUIN_LOGIN, null, TARGET_USER_LOGIN, TARGET_ORG, false, false))
            .Should().ThrowAsync<OctoshiftCliException>();
        exception.WithMessage("Failed to send reclaim mannequin invitation(s).");

        _mockOctoLogger.Verify(m => m.LogError($"Failed to send reclaim invitation email to {TARGET_USER_LOGIN} for mannequin {MANNEQUIN_LOGIN} ({MANNEQUIN_ID}): {failureMessage}"), Times.Once);
    }

    [Fact]
    public async Task ReclaimMannequin_TwoMannequins_FailedToReclaimOne_LogsError_Throws_OctoshiftCliException()
    {
        var mannequinUser2 = "mona";
        var mannequinUserId2 = "modaId2";
        var failureMessage = "Target must be a member of the octocat organization";
        var mannequinsResponse = new Mannequin[] {
            new Mannequin { Id = MANNEQUIN_ID, Login = MANNEQUIN_LOGIN },
            new Mannequin { Id = mannequinUserId2, Login = mannequinUser2 }
        };

        var reclaimMannequinResponse = new CreateAttributionInvitationResult()
        {
            Data = new CreateAttributionInvitationData()
            {
                CreateAttributionInvitation = null
            },
            Errors = [new ErrorData
            {
                Type = "UNPROCESSABLE",
                Message = failureMessage,
                Path = ["createAttributionInvitation"],
                Locations = [new Location() { Line = 2, Column = 14 }]
            }
            ]
        };

        var reclaimMannequinResponse2 = new CreateAttributionInvitationResult()
        {
            Data = new CreateAttributionInvitationData()
            {
                CreateAttributionInvitation = new CreateAttributionInvitation()
                {
                    Source = new UserInfo() { Id = mannequinUserId2, Login = mannequinUser2 },
                    Target = new UserInfo() { Id = TARGET_USER_ID, Login = TARGET_USER_LOGIN }
                }
            }
        };

        _mockGithubApi.Setup(x => x.GetOrganizationId(TARGET_ORG).Result).Returns(ORG_ID);
        _mockGithubApi.Setup(x => x.GetMannequinsByLogin(ORG_ID, MANNEQUIN_LOGIN).Result).Returns(mannequinsResponse);
        _mockGithubApi.Setup(x => x.GetUserId(TARGET_USER_LOGIN).Result).Returns(TARGET_USER_ID);
        _mockGithubApi.Setup(x => x.CreateAttributionInvitation(ORG_ID, MANNEQUIN_ID, TARGET_USER_ID).Result).Returns(reclaimMannequinResponse);
        _mockGithubApi.Setup(x => x.CreateAttributionInvitation(ORG_ID, mannequinUserId2, TARGET_USER_ID).Result).Returns(reclaimMannequinResponse2);

        // Act
        var exception = await FluentActions
            .Invoking(async () => await _service.ReclaimMannequin(MANNEQUIN_LOGIN, null, TARGET_USER_LOGIN, TARGET_ORG, false, false))
            .Should().ThrowAsync<OctoshiftCliException>();
        exception.WithMessage("Failed to send reclaim mannequin invitation(s).");

        _mockGithubApi.Verify(x => x.CreateAttributionInvitation(ORG_ID, MANNEQUIN_ID, TARGET_USER_ID), Times.Once);
        _mockGithubApi.Verify(x => x.CreateAttributionInvitation(ORG_ID, mannequinUserId2, TARGET_USER_ID), Times.Once);

        _mockOctoLogger.Verify(m => m.LogError($"Failed to send reclaim invitation email to {TARGET_USER_LOGIN} for mannequin {MANNEQUIN_LOGIN} ({MANNEQUIN_ID}): {failureMessage}"), Times.Once);
    }


    [Fact]
    public async Task ReclaimMannequin_NoExistantMannequin_No_Reclaim_Throws_OctoshiftCliException()
    {
        var mannequinsResponse = Array.Empty<Mannequin>();

        _mockGithubApi.Setup(x => x.GetOrganizationId(TARGET_ORG).Result).Returns(ORG_ID);
        _mockGithubApi.Setup(x => x.GetMannequinsByLogin(ORG_ID, MANNEQUIN_LOGIN).Result).Returns(mannequinsResponse);
        _mockGithubApi.Setup(x => x.GetUserId(TARGET_USER_LOGIN).Result).Returns(TARGET_USER_ID);

        // Act
        await FluentActions
            .Invoking(async () => await _service.ReclaimMannequin(MANNEQUIN_LOGIN, null, TARGET_USER_LOGIN, TARGET_ORG, false, false))
            .Should().ThrowAsync<OctoshiftCliException>();

        _mockGithubApi.Verify(x => x.CreateAttributionInvitation(ORG_ID, MANNEQUIN_ID, TARGET_USER_ID), Times.Never());
        _mockGithubApi.Verify(x => x.GetUserId(TARGET_USER_LOGIN), Times.Never());
    }

    [Fact]
    public async Task ReclaimMannequin_NoClaimantUser_No_Reclaim_Throws_OctoshiftCliException()
    {
        var mannequinsResponse = new Mannequin[] {
            new Mannequin { Id = MANNEQUIN_ID,Login = MANNEQUIN_LOGIN }
        };

        _mockGithubApi.Setup(x => x.GetOrganizationId(TARGET_ORG).Result).Returns(ORG_ID);
        _mockGithubApi.Setup(x => x.GetMannequinsByLogin(ORG_ID, MANNEQUIN_LOGIN).Result).Returns(mannequinsResponse);
        _mockGithubApi.Setup(x => x.GetUserId(TARGET_USER_LOGIN)).Throws(new OctoshiftCliException("Could not resolve to a User with the login of 'idonotexist'."));

        // Act
        var exception = await FluentActions
            .Invoking(async () => await _service.ReclaimMannequin(MANNEQUIN_LOGIN, null, TARGET_USER_LOGIN, TARGET_ORG, false, false))
            .Should().ThrowAsync<OctoshiftCliException>();

        exception.WithMessage($"Could not resolve to a User with the login of 'idonotexist'.");

        _mockGithubApi.Verify(x => x.GetUserId(TARGET_USER_LOGIN), Times.Once());
    }

    [Fact]
    public async Task ReclaimMannequinSkipInvitation_Happy_Path()
    {
        var mannequinsResponse = new Mannequin[] {
            new Mannequin { Id = MANNEQUIN_ID, Login = MANNEQUIN_LOGIN}
        };

        var reclaimMannequinResponse = new ReattributeMannequinToUserResult()
        {
            Data = new ReattributeMannequinToUserData()
            {
                ReattributeMannequinToUser = new ReattributeMannequinToUser()
                {
                    Source = new UserInfo() { Id = MANNEQUIN_ID, Login = MANNEQUIN_LOGIN },
                    Target = new UserInfo() { Id = TARGET_USER_ID, Login = TARGET_USER_LOGIN }
                }
            }
        };

        _mockGithubApi.Setup(x => x.GetOrganizationId(TARGET_ORG).Result).Returns(ORG_ID);
        _mockGithubApi.Setup(x => x.GetMannequinsByLogin(ORG_ID, MANNEQUIN_LOGIN).Result).Returns(mannequinsResponse);
        _mockGithubApi.Setup(x => x.GetUserId(TARGET_USER_LOGIN).Result).Returns(TARGET_USER_ID);
        _mockGithubApi.Setup(x => x.ReclaimMannequinSkipInvitation(ORG_ID, MANNEQUIN_ID, TARGET_USER_ID).Result).Returns(reclaimMannequinResponse);

        // Act
        await _service.ReclaimMannequin(MANNEQUIN_LOGIN, null, TARGET_USER_LOGIN, TARGET_ORG, false, true);

        // Assert
        _mockGithubApi.Verify(m => m.GetOrganizationId(TARGET_ORG), Times.Once);
        _mockGithubApi.Verify(m => m.GetMannequinsByLogin(ORG_ID, MANNEQUIN_LOGIN), Times.Once);
        _mockGithubApi.Verify(x => x.CreateAttributionInvitation(ORG_ID, MANNEQUIN_ID, TARGET_USER_ID), Times.Never);
        _mockGithubApi.Verify(x => x.ReclaimMannequinSkipInvitation(ORG_ID, MANNEQUIN_ID, TARGET_USER_ID), Times.Once);
        _mockGithubApi.Verify(x => x.GetUserId(TARGET_USER_LOGIN), Times.Once);
        _mockGithubApi.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task ReclaimMannequinSkipInvitation_No_EMU_Throws_Error_Fails_Fast()
    {
        var mannequinsResponse = new Mannequin[] {
            new Mannequin { Id = MANNEQUIN_ID, Login = MANNEQUIN_LOGIN}
        };

        var reclaimMannequinResponse = new ReattributeMannequinToUserResult()
        {
            Errors =
            [
                new ErrorData()
                {
                    Message = "is not an Enterprise Managed Users (EMU) organization"
                }
            ]
        };

        _mockGithubApi.Setup(x => x.GetOrganizationId(TARGET_ORG).Result).Returns(ORG_ID);
        _mockGithubApi.Setup(x => x.GetMannequinsByLogin(ORG_ID, MANNEQUIN_LOGIN).Result).Returns(mannequinsResponse);
        _mockGithubApi.Setup(x => x.GetUserId(TARGET_USER_LOGIN).Result).Returns(TARGET_USER_ID);
        _mockGithubApi.Setup(x => x.ReclaimMannequinSkipInvitation(ORG_ID, MANNEQUIN_ID, TARGET_USER_ID).Result).Returns(reclaimMannequinResponse);

        // Act
        var exception = await FluentActions
            .Invoking(async () => await _service.ReclaimMannequin(MANNEQUIN_LOGIN, null, TARGET_USER_LOGIN, TARGET_ORG, false, true))
            .Should().ThrowAsync<OctoshiftCliException>();
        exception.WithMessage("Failed to reclaim mannequin.");

        // Assert
        _mockGithubApi.Verify(m => m.GetOrganizationId(TARGET_ORG), Times.Once);
        _mockGithubApi.Verify(m => m.GetMannequinsByLogin(ORG_ID, MANNEQUIN_LOGIN), Times.Once);
        _mockGithubApi.Verify(x => x.CreateAttributionInvitation(ORG_ID, MANNEQUIN_ID, TARGET_USER_ID), Times.Never);
        _mockGithubApi.Verify(x => x.ReclaimMannequinSkipInvitation(ORG_ID, MANNEQUIN_ID, TARGET_USER_ID), Times.Once);
        _mockGithubApi.Verify(x => x.GetUserId(TARGET_USER_LOGIN), Times.Once);
        _mockGithubApi.VerifyNoOtherCalls();
    }
}
