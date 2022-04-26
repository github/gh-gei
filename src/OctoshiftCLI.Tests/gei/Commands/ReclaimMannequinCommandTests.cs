using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using FluentAssertions;
using Moq;
using Octoshift.Models;
using OctoshiftCLI.GithubEnterpriseImporter;
using OctoshiftCLI.GithubEnterpriseImporter.Commands;
using OctoshiftCLI.Models;
using Xunit;

namespace OctoshiftCLI.Tests.GithubEnterpriseImporter.Commands
{
    public class ReclaimMannequinCommandTests
    {
        [Fact]
        public void Should_Have_Options()
        {
            var command = new ReclaimMannequinCommand(null, null);
            Assert.NotNull(command);
            Assert.Equal("reclaim-mannequin", command.Name);
            Assert.Equal(5, command.Options.Count);

            TestHelpers.VerifyCommandOption(command.Options, "github-target-org", true);
            TestHelpers.VerifyCommandOption(command.Options, "mannequin-user", true);
            TestHelpers.VerifyCommandOption(command.Options, "target-user", true);
            TestHelpers.VerifyCommandOption(command.Options, "force", false);
            TestHelpers.VerifyCommandOption(command.Options, "verbose", false);
        }

        [Fact]
        public async Task Happy_Path()
        {
            var githubOrg = "FooOrg";
            var githubOrgId = Guid.NewGuid().ToString();
            var mannequinUser = "mona";
            var mannequinUserId = Guid.NewGuid().ToString();
            var targetUser = "mona_emu";
            var targetUserId = Guid.NewGuid().ToString();

            var mannequinResponse = new Mannequin
            {
                Id = mannequinUserId,
                Login = "mona"
            };

            var reclaimMannequinResponse = new MannequinReclaimResult()
            {
                Data = new CreateAttributionInvitationData()
                {
                    CreateAttributionInvitation = new CreateAttributionInvitation()
                    {
                        Source = new UserInfo()
                        {
                            Id = mannequinUserId,
                            Login = mannequinUser
                        },
                        Target = new UserInfo()
                        {
                            Id = targetUserId,
                            Login = targetUser
                        }
                    }
                }
            };

            var mockGithub = TestHelpers.CreateMock<GithubApi>();
            mockGithub.Setup(x => x.GetOrganizationId(githubOrg).Result).Returns(githubOrgId);
            mockGithub.Setup(x => x.GetMannequin(githubOrgId, mannequinUser).Result).Returns(mannequinResponse);
            mockGithub.Setup(x => x.GetUserId(targetUser).Result).Returns(targetUserId);
            mockGithub.Setup(x => x.ReclaimMannequin(githubOrgId, mannequinUserId, targetUserId).Result).Returns(reclaimMannequinResponse);

            var mockGithubApiFactory = new Mock<ITargetGithubApiFactory>();
            mockGithubApiFactory.Setup(m => m.Create(null, null)).Returns(mockGithub.Object);

            var command = new ReclaimMannequinCommand(TestHelpers.CreateMock<OctoLogger>().Object, mockGithubApiFactory.Object);
            await command.Invoke(githubOrg, mannequinUser, targetUser, false);

            mockGithub.Verify(x => x.ReclaimMannequin(githubOrgId, mannequinUserId, targetUserId));
        }

        [Fact]
        public async Task It_Uses_The_Github_Pat_When_Provided()
        {
            var githubOrg = "FooOrg";
            var githubOrgId = Guid.NewGuid().ToString();
            var mannequinUser = "mona";
            var mannequinUserId = Guid.NewGuid().ToString();
            var targetUser = "mona_emu";
            var targetUserId = Guid.NewGuid().ToString();
            var githubPat = "PAT";

            var mannequinResponse = new Mannequin
            {
                Id = mannequinUserId,
                Login = "mona"
            };

            var reclaimMannequinResponse = new MannequinReclaimResult()
            {
                Data = new CreateAttributionInvitationData()
                {
                    CreateAttributionInvitation = new CreateAttributionInvitation()
                    {
                        Source = new UserInfo()
                        {
                            Id = mannequinUserId,
                            Login = mannequinUser
                        },
                        Target = new UserInfo()
                        {
                            Id = targetUserId,
                            Login = targetUser
                        }
                    }
                }
            };

            var mockGithub = TestHelpers.CreateMock<GithubApi>();
            mockGithub.Setup(x => x.GetOrganizationId(githubOrg).Result).Returns(githubOrgId);
            mockGithub.Setup(x => x.GetMannequin(githubOrgId, mannequinUser).Result).Returns(mannequinResponse);
            mockGithub.Setup(x => x.GetUserId(targetUser).Result).Returns(targetUserId);
            mockGithub.Setup(x => x.ReclaimMannequin(githubOrgId, mannequinUserId, targetUserId).Result).Returns(reclaimMannequinResponse);

            var mockGithubApiFactory = new Mock<ITargetGithubApiFactory>();
            mockGithubApiFactory.Setup(m => m.Create(null, githubPat)).Returns(mockGithub.Object);

            var command = new ReclaimMannequinCommand(TestHelpers.CreateMock<OctoLogger>().Object, mockGithubApiFactory.Object);
            await command.Invoke(githubOrg, mannequinUser, targetUser, false, githubPat);

            mockGithubApiFactory.Verify(m => m.Create(null, githubPat));
        }

        [Fact]
        public async Task AlreadyMapped_No_Reclaim_Throws_OctoshiftCliException()
        {
            var githubOrg = "FooOrg";
            var githubOrgId = Guid.NewGuid().ToString();
            var mannequinUser = "mona";
            var mannequinUserId = Guid.NewGuid().ToString();
            var targetUser = "mona_emu";
            var targetUserId = Guid.NewGuid().ToString();

            var mannequinResponse = new Mannequin
            {
                Id = mannequinUserId,
                Login = "mona",
                MappedUser = new Claimant
                {
                    Id = "claimantId",
                    Login = "claimant"
                }
            };

            var mockGithub = TestHelpers.CreateMock<GithubApi>();
            mockGithub.Setup(x => x.GetOrganizationId(githubOrg).Result).Returns(githubOrgId);
            mockGithub.Setup(x => x.GetMannequin(githubOrgId, mannequinUser).Result).Returns(mannequinResponse);

            var mockGithubApiFactory = new Mock<ITargetGithubApiFactory>();
            mockGithubApiFactory.Setup(m => m.Create(null, null)).Returns(mockGithub.Object);

            var command = new ReclaimMannequinCommand(TestHelpers.CreateMock<OctoLogger>().Object, mockGithubApiFactory.Object);


            await FluentActions
                .Invoking(async () => await command.Invoke(githubOrg, mannequinUser, targetUser, false))
                .Should().ThrowAsync<OctoshiftCliException>();

            mockGithub.Verify(x => x.GetUserId(targetUser), Times.Never());
            mockGithub.Verify(x => x.ReclaimMannequin(githubOrgId, mannequinUserId, targetUserId), Times.Never());
        }

        [Fact]
        public async Task AlreadyMapped_Force_Reclaim()
        {
            var githubOrg = "FooOrg";
            var githubOrgId = Guid.NewGuid().ToString();
            var mannequinUser = "mona";
            var mannequinUserId = Guid.NewGuid().ToString();
            var targetUser = "mona_emu";
            var targetUserId = Guid.NewGuid().ToString();

            var mannequinResponse = new Mannequin
            {
                Id = mannequinUserId,
                Login = mannequinUser,
                MappedUser = new Claimant
                {
                    Id = "claimantId",
                    Login = "claimant"
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
                            Id = mannequinUserId,
                            Login = mannequinUser
                        },
                        Target = new UserInfo()
                        {
                            Id = targetUserId,
                            Login = targetUser
                        }
                    }
                }
            };

            var mockGithub = TestHelpers.CreateMock<GithubApi>();
            mockGithub.Setup(x => x.GetOrganizationId(githubOrg).Result).Returns(githubOrgId);
            mockGithub.Setup(x => x.GetMannequin(githubOrgId, mannequinUser).Result).Returns(mannequinResponse);
            mockGithub.Setup(x => x.GetUserId(targetUser).Result).Returns(targetUserId);
            mockGithub.Setup(x => x.ReclaimMannequin(githubOrgId, mannequinUserId, targetUserId).Result).Returns(reclaimMannequinResponse);

            var mockGithubApiFactory = new Mock<ITargetGithubApiFactory>();
            mockGithubApiFactory.Setup(m => m.Create(null, null)).Returns(mockGithub.Object);

            var command = new ReclaimMannequinCommand(TestHelpers.CreateMock<OctoLogger>().Object, mockGithubApiFactory.Object);
            await command.Invoke(githubOrg, mannequinUser, targetUser, true);

            mockGithub.Verify(x => x.ReclaimMannequin(githubOrgId, mannequinUserId, targetUserId));
        }

        [Fact]
        public async Task Cant_ReclaimUser_Throws_OctoshiftCliException()
        {
            var githubOrg = "FooOrg";
            var githubOrgId = Guid.NewGuid().ToString();
            var mannequinUser = "mona";
            var mannequinUserId = Guid.NewGuid().ToString();
            var targetUser = "mona_emu";
            var targetUserId = Guid.NewGuid().ToString();

            var mannequinResponse = new Mannequin
            {
                Id = mannequinUserId,
                Login = "mona",
                MappedUser = new Claimant
                {
                    Id = "claimantId",
                    Login = "claimant"
                }
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
                    Message = "Target must be a member of the octocat organization",
                    Path = new Collection<string> { "createAttributionInvitation" },
                    Locations = new Collection<Location> {
                                new Location()
                                {
                                    Line = 2,
                                    Column = 14
                                }
                            }
                    }
                }
            };

            var mockGithub = TestHelpers.CreateMock<GithubApi>();
            mockGithub.Setup(x => x.GetOrganizationId(githubOrg).Result).Returns(githubOrgId);
            mockGithub.Setup(x => x.GetMannequin(githubOrgId, mannequinUser).Result).Returns(mannequinResponse);
            mockGithub.Setup(x => x.ReclaimMannequin(githubOrgId, mannequinUserId, targetUserId).Result).Returns(reclaimMannequinResponse);

            var mockGithubApiFactory = new Mock<ITargetGithubApiFactory>();
            mockGithubApiFactory.Setup(m => m.Create(null, null)).Returns(mockGithub.Object);

            var command = new ReclaimMannequinCommand(TestHelpers.CreateMock<OctoLogger>().Object, mockGithubApiFactory.Object);

            await FluentActions
                .Invoking(async () => await command.Invoke(githubOrg, mannequinUser, targetUser, false))
                .Should().ThrowAsync<OctoshiftCliException>();
        }

        [Fact]
        public async Task NoExistantMannequin_No_Reclaim_Throws_OctoshiftCliException()
        {
            var githubOrg = "FooOrg";
            var githubOrgId = Guid.NewGuid().ToString();
            var mannequinUser = "monadoesnotexist";
            var mannequinUserId = Guid.NewGuid().ToString();
            var targetUser = "mona_emu";
            var targetUserId = Guid.NewGuid().ToString();
            var mannequinResponse = new Mannequin();

            var mockGithub = TestHelpers.CreateMock<GithubApi>();
            mockGithub.Setup(x => x.GetOrganizationId(githubOrg).Result).Returns(githubOrgId);
            mockGithub.Setup(x => x.GetMannequin(githubOrgId, mannequinUser).Result).Returns(mannequinResponse);
            mockGithub.Setup(x => x.GetUserId(targetUser).Result).Returns(targetUserId);

            var mockGithubApiFactory = new Mock<ITargetGithubApiFactory>();
            mockGithubApiFactory.Setup(m => m.Create(null, null)).Returns(mockGithub.Object);

            var command = new ReclaimMannequinCommand(TestHelpers.CreateMock<OctoLogger>().Object, mockGithubApiFactory.Object);

            await FluentActions
                .Invoking(async () => await command.Invoke(githubOrg, mannequinUser, targetUser, false))
                .Should().ThrowAsync<OctoshiftCliException>();

            mockGithub.Verify(x => x.ReclaimMannequin(githubOrgId, mannequinUserId, targetUserId), Times.Never());
            mockGithub.Verify(x => x.GetUserId(targetUser), Times.Never());
        }

        [Fact]
        public async Task NoTargetUser_No_Reclaim_Throws_OctoshiftCliException()
        {
            var githubOrg = "FooOrg";
            var githubOrgId = Guid.NewGuid().ToString();
            var mannequinUser = "mona";
            var mannequinUserId = Guid.NewGuid().ToString();
            var targetUser = "mona_emu";
            var targetUserId = Guid.NewGuid().ToString();
            var mannequinResponse = new Mannequin
            {
                Id = mannequinUserId,
                Login = mannequinUser,
                MappedUser = new Claimant
                {
                    Id = "claimantId",
                    Login = "claimant"
                }
            };

            var mockGithub = TestHelpers.CreateMock<GithubApi>();
            mockGithub.Setup(x => x.GetOrganizationId(githubOrg).Result).Returns(githubOrgId);
            mockGithub.Setup(x => x.GetMannequin(githubOrgId, mannequinUser).Result).Returns(mannequinResponse);

            var mockGithubApiFactory = new Mock<ITargetGithubApiFactory>();
            mockGithubApiFactory.Setup(m => m.Create(null, null)).Returns(mockGithub.Object);

            var command = new ReclaimMannequinCommand(TestHelpers.CreateMock<OctoLogger>().Object, mockGithubApiFactory.Object);

            await FluentActions
                .Invoking(async () => await command.Invoke(githubOrg, mannequinUser, targetUser, false))
                .Should().ThrowAsync<OctoshiftCliException>();

            mockGithub.Verify(x => x.ReclaimMannequin(githubOrgId, mannequinUserId, targetUserId), Times.Never());
            mockGithub.Verify(x => x.GetUserId(targetUser), Times.Never());
        }
    }
}
