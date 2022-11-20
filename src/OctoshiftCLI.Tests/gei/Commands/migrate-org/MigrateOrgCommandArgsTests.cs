using System;
using FluentAssertions;
using OctoshiftCLI.GithubEnterpriseImporter.Commands;
using Xunit;

namespace OctoshiftCLI.Tests.GithubEnterpriseImporter.Commands;

public class MigrateOrgCommandArgsTests
{
    [Fact]
    public void GithubSourcePat_Defaults_To_GithubTargetPat()
    {
        var targetPat = Guid.NewGuid().ToString();

        var args = new MigrateOrgCommandArgs()
        {
            GithubTargetPat = targetPat
        };

        args.Validate(null);

        args.GithubSourcePat.Should().Be(targetPat);
    }
}
