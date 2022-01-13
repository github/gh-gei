using System.IO;
using System.Threading.Tasks;
using Xunit;

namespace OctoshiftCLI.Tests.Integration
{
    public class GenerateScriptTests
    {
        #region Script generation and verification helpers
        private async Task<string> GenerateOutputScript(string scenarioName, string additionalFlags)
        {
            var outputFilename = $"{System.IO.Path.GetTempPath()}{scenarioName}.sh";
            File.Delete(outputFilename);
            var parameterString = $"generate-script --github-org GuacamoleResearch --ado-org OCLI --output {outputFilename} {additionalFlags}";
            var parameters = parameterString.Trim().Split(' ');

            await OctoshiftCLI.AdoToGithub.Program.Main(parameters);
            return outputFilename;
        }

        private async Task VerifyOutputScript(string scenarioName, string additionalFlags)
        {
            var outputFilename = await GenerateOutputScript(scenarioName, additionalFlags);
            Assert.True(System.IO.File.Exists(outputFilename), $"{outputFilename} was not generated");

            var outputContents = System.IO.File.ReadAllText(outputFilename);

            var referenceFilePath = System.IO.Path.Combine(System.AppContext.BaseDirectory, "../../../Integration/Reference", $"{scenarioName}.sh");
            var referenceFileContents = System.IO.File.ReadAllText(referenceFilePath);

            //TODO: Should probably add some whitespace stripping to improve the validation
            Assert.Equal(referenceFileContents, outputContents);
        }
        #endregion

        [Fact]
        public async void Should_Fail_To_Generate_With_Invalid_Parameters()
        {
            var filename = await GenerateOutputScript("invalid-parameters", "--unsupport-parameter");
            Assert.False(File.Exists(filename));
        }

        [Fact]
        public async void Should_Generate_Default_Script()
        {
            await VerifyOutputScript("default-parameters", "");
        }

        [Fact]
        public async void Should_Generate_Scripts_With_Repos_Only()
        {
            await VerifyOutputScript("repos-only", "--repos-only");
        }

        [Fact]
        public async void Should_Generate_Script_Without_Idp()
        {
            await VerifyOutputScript("skip-idp", "--skip-idp");
        }
    }
}