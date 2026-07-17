using POSCAM.UpdateClient.Models;
using Xunit;

namespace POSCAM.UpdateClient.Tests.Models
{
    public sealed class ApplyOptionsTests
    {
        [Fact]
        public void TryParse_RequiredArguments_ReturnsOptions()
        {
            ApplyOptions? options;

            var success = ApplyOptions.TryParse(
                new[]
                {
                    "apply",
                    "--plan", "C:\\POSCAM\\_update\\state\\repair-plan.json",
                    "--wait-process-id", "1234",
                    "--restart", "PcCam.exe",
                    "--product-code", "pccam",
                    "--architecture", "X86"
                },
                out options);

            Assert.True(success);
            Assert.NotNull(options);
            Assert.Equal(1234, options!.WaitProcessId);
            Assert.Equal("PcCam.exe", options.RestartFileName);
            Assert.Equal("PCCAM", options.ProductCode);
            Assert.Equal("x86", options.Architecture);
            Assert.Equal(
                ApplyOptions.DefaultWaitTimeoutSeconds,
                options.WaitTimeoutSeconds);
        }

        [Fact]
        public void TryParse_CustomTimeout_IsApplied()
        {
            ApplyOptions? options;

            var success = ApplyOptions.TryParse(
                new[]
                {
                    "apply",
                    "--plan", "repair-plan.json",
                    "--wait-process-id", "12",
                    "--restart", "CamViewerClient.exe",
                    "--product-code", "CAMVIEWER",
                    "--architecture", "x64",
                    "--wait-timeout-seconds", "120"
                },
                out options);

            Assert.True(success);
            Assert.NotNull(options);
            Assert.Equal(120, options!.WaitTimeoutSeconds);
            Assert.Equal("CAMVIEWER", options.ProductCode);
            Assert.Equal("x64", options.Architecture);
        }

        [Theory]
        [InlineData("0")]
        [InlineData("-1")]
        [InlineData("not-number")]
        public void TryParse_InvalidProcessId_IsRejected(string processId)
        {
            ApplyOptions? options;

            var success = ApplyOptions.TryParse(
                CreateRequiredArguments(processId),
                out options);

            Assert.False(success);
            Assert.Null(options);
        }

        [Theory]
        [InlineData("0")]
        [InlineData("601")]
        [InlineData("invalid")]
        public void TryParse_InvalidTimeout_IsRejected(string timeout)
        {
            ApplyOptions? options;
            var args = new System.Collections.Generic.List<string>(
                CreateRequiredArguments("12"))
            {
                "--wait-timeout-seconds",
                timeout
            };

            var success = ApplyOptions.TryParse(
                args.ToArray(),
                out options);

            Assert.False(success);
            Assert.Null(options);
        }

        [Theory]
        [InlineData("UNKNOWN", "x86")]
        [InlineData("PCCAM", "any")]
        [InlineData("PCCAM", "arm64")]
        public void TryParse_UnsupportedIdentity_IsRejected(
            string productCode,
            string architecture)
        {
            ApplyOptions? options;
            var args = CreateRequiredArguments("12");
            args[8] = productCode;
            args[10] = architecture;

            var success = ApplyOptions.TryParse(args, out options);

            Assert.False(success);
            Assert.Null(options);
        }

        [Fact]
        public void TryParse_UnknownOption_IsRejected()
        {
            ApplyOptions? options;
            var args = new System.Collections.Generic.List<string>(
                CreateRequiredArguments("12"))
            {
                "--unknown",
                "value"
            };

            var success = ApplyOptions.TryParse(
                args.ToArray(),
                out options);

            Assert.False(success);
            Assert.Null(options);
        }

        [Theory]
        [InlineData("--restart")]
        [InlineData("--product-code")]
        [InlineData("--architecture")]
        public void TryParse_MissingRequiredOption_IsRejected(
            string optionToRemove)
        {
            ApplyOptions? options;
            var args = new System.Collections.Generic.List<string>(
                CreateRequiredArguments("12"));
            var index = args.IndexOf(optionToRemove);
            args.RemoveAt(index + 1);
            args.RemoveAt(index);

            var success = ApplyOptions.TryParse(
                args.ToArray(),
                out options);

            Assert.False(success);
            Assert.Null(options);
        }

        private static string[] CreateRequiredArguments(string processId)
        {
            return new[]
            {
                "apply",
                "--plan", "repair-plan.json",
                "--wait-process-id", processId,
                "--restart", "PcCam.exe",
                "--product-code", "PCCAM",
                "--architecture", "x86"
            };
        }
    }
}
