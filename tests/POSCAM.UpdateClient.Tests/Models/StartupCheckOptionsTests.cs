using POSCAM.UpdateClient.Models;
using Xunit;

namespace POSCAM.UpdateClient.Tests.Models
{
    public sealed class StartupCheckOptionsTests
    {
        [Fact]
        public void TryParse_RequiredArguments_UsesDefaultsAndNormalizesIdentity()
        {
            var success = StartupCheckOptions.TryParse(
                new[]
                {
                    "startup-check",
                    "--app",
                    "PcCam.exe",
                    "--install-dir",
                    @"C:\POSCAM\PCCAM",
                    "--product-code",
                    "pccam",
                    "--architecture",
                    "X86"
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

        [Theory]
        [InlineData("pccam_x86", "X86", "PCCAM_X86", "x86")]
        [InlineData("pccam_x64", "X64", "PCCAM_X64", "x64")]
        public void TryParse_SplitPcCamIdentity_Normalizes(
            string productCode,
            string architecture,
            string expectedProductCode,
            string expectedArchitecture)
        {
            var success = StartupCheckOptions.TryParse(
                new[]
                {
                    "startup-check",
                    "--app",
                    "PcCam.exe",
                    "--install-dir",
                    @"C:\POSCAM\PCCAM",
                    "--product-code",
                    productCode,
                    "--architecture",
                    architecture
                },
                out var options);

            Assert.True(success);
            Assert.NotNull(options);
            Assert.Equal(expectedProductCode, options!.ProductCode);
            Assert.Equal(expectedArchitecture, options.Architecture);
        }

        [Fact]
        public void TryParse_OptionalArguments_AppliesOverrides()
        {
            var success = StartupCheckOptions.TryParse(
                new[]
                {
                    "startup-check",
                    "--app",
                    "CamViewerClient.exe",
                    "--install-dir",
                    @"C:\POSCAM\CamViewer",
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
        [InlineData(true, false)]
        [InlineData(false, true)]
        public void TryParse_MissingProductOrArchitecture_ReturnsFalse(
            bool omitProductCode,
            bool omitArchitecture)
        {
            var args = new System.Collections.Generic.List<string>
            {
                "startup-check",
                "--app",
                "PcCam.exe",
                "--install-dir",
                @"C:\POSCAM\PCCAM"
            };

            if (!omitProductCode)
            {
                args.Add("--product-code");
                args.Add("PCCAM");
            }

            if (!omitArchitecture)
            {
                args.Add("--architecture");
                args.Add("x86");
            }

            var success = StartupCheckOptions.TryParse(
                args.ToArray(),
                out var options);

            Assert.False(success);
            Assert.Null(options);
        }

        [Theory]
        [InlineData("UNKNOWN", "x86")]
        [InlineData("PCCAM", "any")]
        [InlineData("PCCAM", "arm64")]
        [InlineData("PCCAM_X86", "x64")]
        [InlineData("PCCAM_X64", "x86")]
        public void TryParse_UnsupportedIdentity_ReturnsFalse(
            string productCode,
            string architecture)
        {
            var success = StartupCheckOptions.TryParse(
                new[]
                {
                    "startup-check",
                    "--app",
                    "PcCam.exe",
                    "--install-dir",
                    @"C:\POSCAM\PCCAM",
                    "--product-code",
                    productCode,
                    "--architecture",
                    architecture
                },
                out var options);

            Assert.False(success);
            Assert.Null(options);
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
                    "--product-code",
                    "PCCAM",
                    "--architecture",
                    "x86",
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
                    @"C:\POSCAM\PCCAM",
                    "--product-code",
                    "PCCAM",
                    "--architecture",
                    "x86"
                },
                out var options);

            Assert.False(success);
            Assert.Null(options);
        }
    }
}
