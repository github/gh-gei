using System.IO;
using Moq;
using Xunit;

namespace OctoshiftCLI.Tests
{
    public class AdoApiTests
    {
        [Fact]
        public async void GetUserIdTest()
        {
            var endpoint = "https://app.vssps.visualstudio.com/_apis/profile/profiles/me?api-version=5.0-preview.1";
            var userId = "foo";
            var userJson = "{ coreAttributes: { PublicAlias: { value: \"" + userId + "\" }}}";

            var mockClient = new Mock<AdoClient>(null, null);

            mockClient.Setup(x => x.GetAsync(endpoint).Result).Returns(userJson);

            using var sut = new AdoApi(mockClient.Object);
            var result = await sut.GetUserId();

            Assert.Equal(userId, result);
        }

        [Fact]
        public async void GetUserId_InvalidResponse()
        {
            var endpoint = "https://app.vssps.visualstudio.com/_apis/profile/profiles/me?api-version=5.0-preview.1";
            var userId = "foo";
            var userJson = "{ invalid: { PublicAlias: { value: \"" + userId + "\" }}}";

            var mockClient = new Mock<AdoClient>(null, null);

            mockClient.Setup(x => x.GetAsync(endpoint).Result).Returns(userJson);

            using var sut = new AdoApi(mockClient.Object);
            await Assert.ThrowsAsync<InvalidDataException>(async () => await sut.GetUserId());
        }
    }
}