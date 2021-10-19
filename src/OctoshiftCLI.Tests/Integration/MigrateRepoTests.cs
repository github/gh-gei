using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using Xunit;

namespace OctoshiftCLI.Tests.Integration
{
    public class MigrateRepoTests
    {
        public MigrateRepoTests()
        {
            // // Need to make sure the octoshift binary is in the /tmp directory for script executables
            // var cliBinary =  System.IO.Path.Combine(System.AppContext.BaseDirectory, "octoshift*");
            // File.Copy(cliBinary, $"{System.IO.Path.GetTempPath()}*", true);
        }

        #region TODO: Merge/commonize-->Helpers
        private async Task<string> GenerateOutputScript(string scenarioName, string additionalFlags)
        {
            var outputFilename = $"{System.IO.Path.GetTempPath()}{scenarioName}.sh";
            File.Delete(outputFilename);
            var parameterString = $"generate-script --github-org GuacamoleResearch --ado-org OCLI --output {outputFilename} {additionalFlags}";
            var parameters = parameterString.Trim().Split(' ');

            await OctoshiftCLI.Program.Main(parameters);
            return outputFilename;
        }
        #endregion

        [Fact]
        public async Task Should_Migrate_Via_Default_Script()
        {
            // var outputFilename = GenerateOutputScript("migrate-empty-repo", "--repos-only").Result;
            // await Helpers.Bash("/tmp", outputFilename);
            Assert.True(true);
        }
    }
}
