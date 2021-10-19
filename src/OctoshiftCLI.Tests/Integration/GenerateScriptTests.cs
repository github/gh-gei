using System;
using System.Threading.Tasks;
using Xunit;

namespace OctoshiftCLI.Tests.Integration
{
    public class GenerateScriptTests
    {
        #region Script generation and verification
        private async Task<string> GenerateOutputScript(string scenarioName, string additionalFlags)
        {
            var outputFilename = (String.IsNullOrEmpty(scenarioName)) 
                ? $"{System.IO.Path.GetTempPath()}{Guid.NewGuid()}.sh" 
                : $"{System.IO.Path.GetTempPath()}{scenarioName}.sh";
            
            System.IO.File.Delete(outputFilename);
            var parameterString = $"generate-script --github-org GuacamoleResearch --ado-org OCLI --output {outputFilename} {additionalFlags}";
            var parameters = parameterString.Trim().Split(' ');
            await OctoshiftCLI.Program.Main(parameters);

            return outputFilename;
        }

        private async Task VerifyOutputScript(string scenarioName, string additionalFlags)
        {
            var outputFilename = await GenerateOutputScript(scenarioName, additionalFlags);
            Assert.True(System.IO.File.Exists(outputFilename), $"{outputFilename} was not generated");

            var outputContents = System.IO.File.ReadAllText(outputFilename);

            var referenceFilePath = System.IO.Path.Combine(System.AppContext.BaseDirectory, "../../../Integration/Reference", $"{scenarioName}.sh");
            var referenceFileContents = System.IO.File.ReadAllText(referenceFilePath);
            
            Assert.Equal(referenceFileContents, outputContents);
        }    
        #endregion

        [Fact]
        public async void Should_Fail_With_Invalid_Parameters()
        {
            var filename = await GenerateOutputScript("invalid-parameters", "--unsupport-parameter");
            Assert.False(System.IO.File.Exists(filename));
        }

        [Fact]
        public async void Should_Generate_With_Default_Parameters()
        {
            await VerifyOutputScript("default-parameters", "");
        }

        [Fact]
        public async void Should_Generate_With_Repos_Only()
        {
            await VerifyOutputScript("repos-only", "--repos-only");
        }

        [Fact]
        public async void Should_Generate_With_Skip_Idp()
        {
            await VerifyOutputScript("skip-idp", "--skip-idp");
        }
    }
}
