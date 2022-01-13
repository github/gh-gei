using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Moq;
using Newtonsoft.Json.Linq;
using Xunit;

namespace OctoshiftCLI.Tests
{
    public class OctoLoggerTests
    {
        private string _logOutput;
        private string _verboseLogOutput;
        private string _consoleOutput;

        [Fact]
        public void Secrets_Should_Be_Masked_From_Logs_And_Console()
        {
            var secret = "purplemonkeydishwasher";

            var sut = new OctoLogger(CaptureLogOutput, CaptureVerboseLogOutput, CaptureConsoleOutput);

            sut.RegisterSecret(secret);

            sut.Verbose = false;
            sut.LogInformation($"Don't tell anybody that {secret} is my password");
            sut.LogVerbose($"Don't tell anybody that {secret} is my password");
            sut.LogWarning($"Don't tell anybody that {secret} is my password");
            sut.LogSuccess($"Don't tell anybody that {secret} is my password");
            sut.LogError($"Don't tell anybody that {secret} is my password");

            sut.Verbose = true;
            sut.LogVerbose($"Don't tell anybody that {secret} is my password");

            _consoleOutput.Should().NotContain(secret);
            _logOutput.Should().NotContain(secret);
            _verboseLogOutput.Should().NotContain(secret);
        }

        private void CaptureLogOutput(string msg) => _logOutput += msg;

        private void CaptureVerboseLogOutput(string msg) => _verboseLogOutput += msg;

        private void CaptureConsoleOutput(string msg) => _consoleOutput += msg;
    }
}