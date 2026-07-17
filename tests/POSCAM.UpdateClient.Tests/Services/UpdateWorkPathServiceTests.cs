using System;
using System.IO;
using POSCAM.UpdateClient.Services;
using Xunit;

namespace POSCAM.UpdateClient.Tests.Services
{
    public sealed class UpdateWorkPathServiceTests
    {
        private readonly UpdateWorkPathService _service
            = new UpdateWorkPathService();

        [Fact]
        public void ResolveJobFilePath_SafeRelativePath_ReturnsContainedPath()
        {
            var jobDirectory = Path.Combine(
                Path.GetTempPath(),
                "POSCAM.UpdateClient.PathTests",
                Guid.NewGuid().ToString("N"));

            var result = _service.ResolveJobFilePath(
                jobDirectory,
                "files/providers/provider.dll");

            Assert.Equal(
                Path.Combine(
                    jobDirectory,
                    "files",
                    "providers",
                    "provider.dll"),
                result);
        }

        [Theory]
        [InlineData("../outside.dll")]
        [InlineData("files/../../outside.dll")]
        [InlineData("files//provider.dll")]
        [InlineData(@"C:\Windows\system32\file.dll")]
        public void ResolveJobFilePath_UnsafePath_Throws(
            string relativePath)
        {
            var jobDirectory = Path.Combine(
                Path.GetTempPath(),
                "POSCAM.UpdateClient.PathTests",
                Guid.NewGuid().ToString("N"));

            Assert.Throws<InvalidDataException>(
                () => _service.ResolveJobFilePath(
                    jobDirectory,
                    relativePath));
        }

        [Theory]
        [InlineData("../package.zip")]
        [InlineData("folder/package.zip")]
        [InlineData(@"folder\package.zip")]
        [InlineData("package.zip.")]
        public void ValidateFileName_UnsafeFileName_Throws(
            string fileName)
        {
            Assert.Throws<InvalidDataException>(
                () => _service.ValidateFileName(fileName));
        }
    }
}
