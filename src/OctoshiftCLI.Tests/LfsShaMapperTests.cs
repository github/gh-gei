using System.Collections.Generic;
using System.Threading.Tasks;
using FluentAssertions;
using Xunit;
using Moq;

namespace OctoshiftCLI.Tests
{
    public class LfsShaMapperTests
    {
        private readonly Mock<OctoLogger> _mockOctoLogger = TestHelpers.CreateMock<OctoLogger>();
        private readonly Mock<ArchiveHandler> _archiveHandler = TestHelpers.CreateMock<ArchiveHandler>();

        private readonly LfsShaMapper _lfsShaMapper;

        public LfsShaMapperTests()
        {
            _lfsShaMapper = new LfsShaMapper(_mockOctoLogger.Object, _archiveHandler.Object)
            {
                WriteToFile = (_, contents) =>
                {
                    return Task.CompletedTask;
                },
                ReadFile = (_) =>
                {
                    return "Bunch of text with oldsha all over the place. oldsha here, oldsha there, oldsha everywhere.";
                },
                ReadMappingFile = (_) =>
                {
                    return new List<string> { "oldsha,newsha" };
                }
            };
        }

        [Fact]
        public async Task Does_This_Work()
        {
            var actualLogOutput = new List<string>();
            _mockOctoLogger.Setup(m => m.LogInformation(It.IsAny<string>())).Callback<string>(s => actualLogOutput.Add(s));
            _mockOctoLogger.Setup(m => m.LogWarning(It.IsAny<string>())).Callback<string>(s => actualLogOutput.Add(s));

            _archiveHandler.Setup(m => m.Unpack(It.IsAny<byte[]>())).Returns(new string[] {"archiveExtracted/pull_requests_1.json", "archiveExtracted/pull_requests_2.json"});
            _archiveHandler.Setup(m => m.Pack("./archiveExtracted")).Returns(new byte[] { 6, 7, 8, 9, 10 });

            var result = await _lfsShaMapper.MapShas(new byte[] { 6, 7, 8, 9, 10 }, "./lfsMappingFile");

            result.Should().BeEquivalentTo(new byte[] { 6, 7, 8, 9, 10 });
            _archiveHandler.Verify(m => m.Unpack(It.IsAny<byte[]>()), Times.Once);
            _archiveHandler.Verify(m => m.Pack("./archiveExtracted"), Times.Once);
            _mockOctoLogger.Verify(m => m.LogInformation(It.IsAny<string>()), Times.Exactly(2));
        }
    }

}
