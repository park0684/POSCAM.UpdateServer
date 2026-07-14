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
                    "--restart", "PcCam.exe"
                },
                out options);

            Assert.True(success);
            Assert.NotNull(options);
            Assert.Equal(1234, options!.WaitProcessId);
            Assert.Equal("PcCam.exe", options.RestartFileName);
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
                    "--restart", "PcCam.exe",
                    "--wait-timeout-seconds", "120"
                },
                out options);

            Assert.True(success);
            Assert.NotNull(options);
            Assert.Equal(120, options!.WaitTimeoutSeconds);
        }

        [Theory]
        [InlineData("0")]
        [InlineData("-1")]
        [InlineData("not-number")]
        public void TryParse_InvalidProcessId_IsRejected(string processId)
        {
            ApplyOptions? options;

            var success = ApplyOptions.TryParse(
                new[]
                {
                    "apply",
                    "--plan", "repair-plan.json",
                    "--wait-process-id", processId,
                    "--restart", "PcCam.exe"
                },
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

            var success = ApplyOptions.TryParse(
                new[]
                {
                    "apply",
                    "--plan", "repair-plan.json",
                    "--wait-process-id", "12",
                    "--restart", "PcCam.exe",
                    "--wait-timeout-seconds", timeout
                },
                out options);

            Assert.False(success);
            Assert.Null(options);
        }

        [Fact]
        public void TryParse_UnknownOption_IsRejected()
        {
            ApplyOptions? options;

            var success = ApplyOptions.TryParse(
                new[]
                {
                    "apply",
                    "--plan", "repair-plan.json",
                    "--wait-process-id", "12",
                    "--restart", "PcCam.exe",
                    "--unknown", "value"
                },
                out options);

            Assert.False(success);
            Assert.Null(options);
        }

        [Fact]
        public void TryParse_MissingRequiredOption_IsRejected()
        {
            ApplyOptions? options;

            var success = ApplyOptions.TryParse(
                new[]
                {
                    "apply",
                    "--plan", "repair-plan.json",
                    "--wait-process-id", "12"
                },
                out options);

            Assert.False(success);
            Assert.Null(options);
        }
    }
}
