using System;
using System.IO;
using Moq;
using OctoshiftCLI.Models;
using Xunit;

namespace OctoshiftCLI.Tests
{
    public sealed class ConsoleWriterTests
    {
        private readonly Mock<GithubApi> _mockGithubApi = TestHelpers.CreateMock<GithubApi>();
        private readonly string WRITER_OUTPUT = $"Migration log available at: {TEST_URL}\n";

        private const string TARGET_ORG = "TARGET_ORG";
        private const string TARGET_REPO = "TARGET_REPO";
        private const string TEST_URL = "URL";

        public ConsoleWriterTests()
        {
        }

        [Fact]
        public void No_Output_When_Wait_Is_False()
        { 
            // Arrange
            _mockGithubApi.Setup(x => x.GetMigrationLogUrl(TARGET_ORG, TARGET_REPO)).ReturnsAsync(TEST_URL);

            using var mockConsole = new StringWriter();
            Console.SetOut(mockConsole);
            Console.SetError(mockConsole);

            // Act
            ConsoleWriter.OutputLogUrl(_mockGithubApi.Object, TARGET_ORG, TARGET_REPO, false);

            // Assert
            Assert.Empty(mockConsole.ToString());
        }


        [Fact]
        public void No_Output_When_Api_Is_Null()
        {
            // Arrange
            _mockGithubApi.Setup(x => x.GetMigrationLogUrl(TARGET_ORG, TARGET_REPO)).ReturnsAsync(TEST_URL);

            using var mockConsole = new StringWriter();
            Console.SetOut(mockConsole);
            Console.SetError(mockConsole);

            // Act
            ConsoleWriter.OutputLogUrl(null, TARGET_ORG, TARGET_REPO);

            // Assert
            Assert.Empty(mockConsole.ToString());
        }

        [Fact]
        public void No_Output_When_URL_Is_Empty()
        {
            // Arrange
            _mockGithubApi.Setup(x => x.GetMigrationLogUrl(TARGET_ORG, TARGET_REPO)).ReturnsAsync("");

            using var mockConsole = new StringWriter();
            Console.SetOut(mockConsole);
            Console.SetError(mockConsole);

            // Act
            ConsoleWriter.OutputLogUrl(_mockGithubApi.Object, TARGET_ORG, TARGET_REPO);

            // Assert
            Assert.Empty(mockConsole.ToString());
        }

        [Fact]
        public void Output_On_Successful_API_Call()
        {
            // Arrange
            _mockGithubApi.Setup(x => x.GetMigrationLogUrl(TARGET_ORG, TARGET_REPO)).ReturnsAsync(TEST_URL);

            using var mockConsole = new StringWriter();
            Console.SetOut(mockConsole);
            Console.SetError(mockConsole);

            // Act
            ConsoleWriter.OutputLogUrl(_mockGithubApi.Object, TARGET_ORG, TARGET_REPO);

            // Assert
            Assert.Equal(mockConsole.ToString(), WRITER_OUTPUT);
        }
    }
}

