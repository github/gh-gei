using FluentAssertions;
using Moq;
using OctoshiftCLI.Factories;
using OctoshiftCLI.Services;
using Xunit;

namespace OctoshiftCLI.Tests.Octoshift.Factories;

public class AwsApiFactoryTests
{
    private readonly Mock<EnvironmentVariableProvider> _mockEnvironmentVariableProvider = TestHelpers.CreateMock<EnvironmentVariableProvider>();
    private readonly Mock<OctoLogger> _mockOctoLogger = TestHelpers.CreateMock<OctoLogger>();
    private readonly AwsApiFactory _awsApiFactory;

    public AwsApiFactoryTests()
    {
        _awsApiFactory = new AwsApiFactory(_mockEnvironmentVariableProvider.Object, _mockOctoLogger.Object);
    }

    [Fact]
    public void It_Errors_If_Aws_Access_Key_Id_Is_Not_Provided_Or_Set_In_Environment_Variable()
    {
        // Act, Assert
        _awsApiFactory.Invoking(x => x.Create("us-east-2", null, "aws-secret-access-key", "aws-session-token"))
                .Should()
                .ThrowExactly<System.ArgumentNullException>();
    }

    [Fact]
    public void It_Errors_If_Aws_Secret_Access_Key_Is_Not_Set_Or_Set_In_Environment_Variable()
    {
        // Act, Assert
        _awsApiFactory.Invoking(x => x.Create("us-east-2", "aws-access-key-id", null, "aws-session-token"))
                .Should()
                .ThrowExactly<System.ArgumentNullException>();
    }
}
