using System.IO;
using System.Threading.Tasks;
using Xunit;

namespace OctoshiftCLI.Tests.Integration
{
    public class GenerateArchiveTests
    {
        #region Script generation and verification helpers
        private async Task<string> GenerateArchive(string scenarioName, string additionalFlags)
        {
            var outputFilename = $"{System.IO.Path.GetTempPath()}{scenarioName}.sh";
            File.Delete(outputFilename);
            var parameterString = $"generate-archive --github-source-org GuacamoleResearch --github-url https://mygithub.com/api --github-source-repo myrepo";
            var parameters = parameterString.Trim().Split(' ');

            await OctoshiftCLI.GithubEnterpriseImporter.Program.Main(parameters);
            return "";
        }
        #endregion

        [Fact]
        public async void Should_Fail_To_Generate_With_Invalid_Parameters()
        {
            _ = await GenerateArchive("invalid-parameters", "--unsupport-parameter");
        }
    }
}