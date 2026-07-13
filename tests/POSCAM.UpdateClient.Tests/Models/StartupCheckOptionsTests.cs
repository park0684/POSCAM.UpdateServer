using POSCAM.UpdateClient.Models;
using Xunit;

namespace POSCAM.UpdateClient.Tests.Models
{
    public sealed class StartupCheckOptionsTests
    {
        [Fact]
        public void TryParse_MinimumArguments_UsesDefaults()
        {
            var success = StartupCheckOptions.TryParse(
                new[]
                {
                    "startup-check",
                    "--app",
                    "PcCam.exe",
                    "--install-dir",
                    @"C:\POSCAM\PCCAM"
                },
                out var options);

            Assert.True(success);
            Assert.NotNull(options);
            Assert.Equal("PcCam.exe", options!.ApplicationFileName);
            Assert.Equal(@"C:\POSCAM\PCCAM", options.InstallDirectory);
            Assert.Equal(
                StartupCheckOptions.DefaultBaseUrl,
                options.BaseUrl);
            Assert.Equal("PCCAM", options.ProductCode);
            Assert.Equal("windows", options.OperatingSystem);
            Assert.Equal("x86", options.Architecture);
            Assert.Equal("stable", options.Channel);
        }

        [Fact]
        public void TryParse_OptionalArguments_AppliesOverrides()
        {
            var success = StartupCheckOptions.TryParse(
                new[]
                {
                    "startup-check",
                    "--app",
                    "PcCam.exe",
                    "--install-dir",
                    @"C:\POSCAM\PCCAM",
                    "--base-url",
                    "http://localhost:5000",
                    "--product-code",
                    "CAMVIEWER",
                    "--os",
                    "windows",
                    "--architecture",
                    "x64",
                    "--channel",
                    "beta",
                    "--current-version",
                    "1.2.3.4"
                },
                out var options);

            Assert.True(success);
            Assert.NotNull(options);
            Assert.Equal("http://localhost:5000", options!.BaseUrl);
            Assert.Equal("CAMVIEWER", options.ProductCode);
            Assert.Equal("x64", options.Architecture);
            Assert.Equal("beta", options.Channel);
            Assert.Equal("1.2.3.4", options.CurrentVersionOverride);
        }

        [Theory]
        [InlineData("--unknown")]
        [InlineData("--app")]
        [InlineData("--install-dir")]
        public void TryParse_UnknownOrIncompleteArguments_ReturnsFalse(
            string invalidKey)
        {
            string[] args;

            if (invalidKey == "--unknown")
            {
                args = new[]
                {
                    "startup-check",
                    "--app",
                    "PcCam.exe",
                    "--install-dir",
                    @"C:\POSCAM\PCCAM",
                    "--unknown",
                    "value"
                };
            }
            else
            {
                args = new[]
                {
                    "startup-check",
                    invalidKey,
                    "value"
                };
            }

            var success = StartupCheckOptions.TryParse(
                args,
                out var options);

            Assert.False(success);
            Assert.Null(options);
        }

        [Fact]
        public void TryParse_DuplicateArgument_ReturnsFalse()
        {
            var success = StartupCheckOptions.TryParse(
                new[]
                {
                    "startup-check",
                    "--app",
                    "PcCam.exe",
                    "--app",
                    "Other.exe",
                    "--install-dir",
                    @"C:\POSCAM\PCCAM"
                },
                out var options);

            Assert.False(success);
            Assert.Null(options);
        }
    }
}
