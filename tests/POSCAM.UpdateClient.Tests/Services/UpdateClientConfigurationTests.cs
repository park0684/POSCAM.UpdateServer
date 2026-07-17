using POSCAM.UpdateClient.Models;
using POSCAM.UpdateClient.Services;
using Xunit;

namespace POSCAM.UpdateClient.Tests.Services
{
    public sealed class UpdateClientConfigurationTests
    {
        [Fact]
        public void TryResolveUpdateServerBaseUrl_MissingValue_UsesProductionDefault()
        {
            var success = UpdateClientConfiguration.TryResolveUpdateServerBaseUrl(
                null,
                out var baseUrl);

            Assert.True(success);
            Assert.Equal(StartupCheckOptions.DefaultBaseUrl, baseUrl);
        }

        [Fact]
        public void TryResolveUpdateServerBaseUrl_LocalHttpUrl_NormalizesTrailingSlash()
        {
            var success = UpdateClientConfiguration.TryResolveUpdateServerBaseUrl(
                " http://localhost:5257/ ",
                out var baseUrl);

            Assert.True(success);
            Assert.Equal("http://localhost:5257", baseUrl);
        }

        [Theory]
        [InlineData("localhost:5257")]
        [InlineData("file:///C:/updates")]
        [InlineData("not-a-url")]
        public void TryResolveUpdateServerBaseUrl_InvalidValue_ReturnsFalse(
            string configuredValue)
        {
            var success = UpdateClientConfiguration.TryResolveUpdateServerBaseUrl(
                configuredValue,
                out var baseUrl);

            Assert.False(success);
            Assert.Equal("", baseUrl);
        }
    }
}
