using FluentAssertions;
using Octoshift;
using OctoshiftCLI.Models;
using Xunit;

namespace OctoshiftCLI.Tests
{
    public class MannequinsTests
    {

        [Fact]
        public void IsClaimed_No_ClaimedMannequins()
        {
            var mannequins = new Mannequins(
                new Mannequin[]
                {
                    new Mannequin { Id = "id1", Login = "login1" },
                    new Mannequin { Id = "id2",Login = "login2" }
                });

            // Act
            var claimed = mannequins.IsClaimed("login1", "id1");

            // Assert
            claimed.Should().Be(false);
        }

        [Fact]
        public void IsClaimed_MultipleClaims_IsClaimed()
        {
            var mannequins = new Mannequins(
                new Mannequin[]
                {
                    new Mannequin { Id = "id1", Login = "login1"},
                    new Mannequin { Id = "id1", Login = "login1",
                        MappedUser = new Claimant{ Id = "claimedid", Login = "claimedlogin" }
                    }
                });

            // Act
            var claimed = mannequins.IsClaimed("login1", "id1");

            // Assert
            claimed.Should().Be(true);
        }

        [Fact]
        public void IsClaimed_MultipleClaimsLastOneUnclaimed_IsClaimed()
        {
            var mannequins = new Mannequins(
                new Mannequin[]
                {
                    new Mannequin { Id = "id1", Login = "login1",
                        MappedUser = new Claimant{ Id = "claimedid", Login = "claimedlogin" }
                    },
                    new Mannequin { Id = "id1", Login = "login1" }
                });

            // Act
            var claimed = mannequins.IsClaimed("login1", "id1");

            // Assert
            claimed.Should().Be(true);
        }


        [Fact]
        public void IsClaimed_SameLoginDifferentID_NotClaimed()
        {
            var mannequins = new Mannequins(
                new Mannequin[]
                {
                    new Mannequin { Id = "id1", Login = "login"},
                    new Mannequin { Id = "id2", Login = "login",
                        MappedUser = new Claimant{ Id = "claimedid", Login = "claimedlogin" }
                    }
                });

            // Act
            var claimed = mannequins.IsClaimed("login", "id1");

            // Assert
            claimed.Should().Be(false);
        }

    }
}
