using FluentAssertions;
using Xunit;

namespace OctoshiftCLI.Tests
{
    public class RepositoryMigrationStatusTests
    {
        [Fact]
        public void IsSucceeded_Returns_True_For_Succeeded_Status()
        {
            RepositoryMigrationStatus.IsSucceeded(RepositoryMigrationStatus.Succeeded).Should().BeTrue();
        }

        [Fact]
        public void IsSucceeded_Returns_False_Otherwise()
        {
            RepositoryMigrationStatus.IsSucceeded(RepositoryMigrationStatus.InProgress).Should().BeFalse();
            RepositoryMigrationStatus.IsSucceeded(RepositoryMigrationStatus.Failed).Should().BeFalse();
            RepositoryMigrationStatus.IsSucceeded("NOT_A_REAL_STATUS").Should().BeFalse();
        }

        [Fact]
        public void IsPending_Returns_True_For_Pending_Statuses()
        {
            RepositoryMigrationStatus.IsPending(RepositoryMigrationStatus.Queued).Should().BeTrue();
            RepositoryMigrationStatus.IsPending(RepositoryMigrationStatus.PendingValidation).Should().BeTrue();
            RepositoryMigrationStatus.IsPending(RepositoryMigrationStatus.InProgress).Should().BeTrue();
        }

        [Fact]
        public void IsPending_Returns_False_Otherwise()
        {
            RepositoryMigrationStatus.IsPending(RepositoryMigrationStatus.Succeeded).Should().BeFalse();
            RepositoryMigrationStatus.IsPending(RepositoryMigrationStatus.Failed).Should().BeFalse();
            RepositoryMigrationStatus.IsPending("NOT_A_REAL_STATUS").Should().BeFalse();
        }

        [Fact]
        public void IsFailed_Returns_True_For_Failed_Or_Invalid_Statuses()
        {
            RepositoryMigrationStatus.IsFailed(RepositoryMigrationStatus.Failed).Should().BeTrue();
            RepositoryMigrationStatus.IsFailed(RepositoryMigrationStatus.FailedValidation).Should().BeTrue();
            RepositoryMigrationStatus.IsFailed("NOT_A_REAL_STATUS").Should().BeTrue();
        }

        [Fact]
        public void IsFailed_Returns_False_For_Pending_Or_Succeeded_Statuses()
        {
            RepositoryMigrationStatus.IsFailed(RepositoryMigrationStatus.Queued).Should().BeFalse();
            RepositoryMigrationStatus.IsFailed(RepositoryMigrationStatus.InProgress).Should().BeFalse();
            RepositoryMigrationStatus.IsFailed(RepositoryMigrationStatus.Succeeded).Should().BeFalse();
        }
    }
}
