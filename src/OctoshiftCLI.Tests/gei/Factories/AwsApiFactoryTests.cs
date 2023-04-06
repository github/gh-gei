using Moq;
using OctoshiftCLI.GithubEnterpriseImporter.Factories;
using OctoshiftCLI.Services;
using Xunit;

namespace OctoshiftCLI.Tests.GithubEnterpriseImporter.Factories;

public class AwsApiFactoryTests
{
    private readonly Mock<EnvironmentVariableProvider> _mockEnvironmentVariableProvider = TestHelpers.CreateMock<EnvironmentVariableProvider>();
    private readonly AwsApiFactory _awsApiFactory;

    public AwsApiFactoryTests()
    {
        _awsApiFactory = new AwsApiFactory(_mockEnvironmentVariableProvider.Object);
    }

    [Fact]
    public void It_Falls_Back_To_Aws_Access_Key_Environment_Variable_If_Aws_Access_Key_Id_Is_Not_Set()
    {
        // Arrange
        const string awsAccessKey = "AWS_ACCESS_KEY";
        const string awsRegion = "us-east-2";
#pragma warning disable CS0618
        _mockEnvironmentVariableProvider.Setup(m => m.AwsAccessKey(false)).Returns(awsAccessKey);
#pragma warning restore CS0618

        // Act
        _awsApiFactory.Create(awsRegion, null, "aws-secret-access-key", "aws-session-token");

        // Assert
#pragma warning disable CS0618
        _mockEnvironmentVariableProvider.Verify(m => m.AwsAccessKey(false), Times.Once);
#pragma warning restore CS0618
    }

    [Fact]
    public void It_Falls_Back_To_Aws_Secret_Key_Environment_Variable_If_Aws_Secret_Access_Key_Is_Not_Set()
    {
        // Arrange
        const string awsSecretKey = "AWS_SECRET_KEY";
        const string awsRegion = "us-east-2";
#pragma warning disable CS0618
        _mockEnvironmentVariableProvider.Setup(m => m.AwsSecretKey(false)).Returns(awsSecretKey);
#pragma warning restore CS0618

        // Act
        _awsApiFactory.Create(awsRegion, "aws-access-key-id", null, "aws-session-token");

        // Assert
#pragma warning disable CS0618
        _mockEnvironmentVariableProvider.Verify(m => m.AwsSecretKey(false), Times.Once);
#pragma warning restore CS0618
    }
}
