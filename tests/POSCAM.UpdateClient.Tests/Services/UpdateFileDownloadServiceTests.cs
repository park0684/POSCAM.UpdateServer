using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using POSCAM.UpdateClient.Models;
using POSCAM.UpdateClient.Services;
using POSCAM.UpdateClient.Tests.TestDoubles;
using Xunit;

namespace POSCAM.UpdateClient.Tests.Services
{
    public sealed class UpdateFileDownloadServiceTests : IDisposable
    {
        private readonly string _workDirectory;

        public UpdateFileDownloadServiceTests()
        {
            _workDirectory = Path.Combine(
                Path.GetTempPath(),
                "POSCAM.UpdateClient.DownloadTests",
                Guid.NewGuid().ToString("N"));

            Directory.CreateDirectory(_workDirectory);
        }

        [Fact]
        public async Task DownloadAndVerifyAsync_ValidFile_WritesVerifiedDestination()
        {
            var content = Encoding.UTF8.GetBytes("verified update file");
            var destinationPath = Path.Combine(
                _workDirectory,
                "downloads",
                "PcCam.exe");

            using (var service = CreateService(
                HttpStatusCode.OK,
                content))
            {
                var result = await service.DownloadAndVerifyAsync(
                    CreateRequest(destinationPath, content),
                    CancellationToken.None);

                Assert.Equal(destinationPath, result);
                Assert.Equal(content, File.ReadAllBytes(destinationPath));
                Assert.False(File.Exists(destinationPath + ".part"));
            }
        }

        [Fact]
        public async Task DownloadAndVerifyAsync_HashMismatch_DoesNotPublishFile()
        {
            var expectedContent = Encoding.UTF8.GetBytes("expected");
            var actualContent = Encoding.UTF8.GetBytes("modified");
            var destinationPath = Path.Combine(
                _workDirectory,
                "downloads",
                "provider.dll");

            using (var service = CreateService(
                HttpStatusCode.OK,
                actualContent))
            {
                await Assert.ThrowsAsync<UpdateDownloadException>(
                    () => service.DownloadAndVerifyAsync(
                        CreateRequest(destinationPath, expectedContent),
                        CancellationToken.None));
            }

            Assert.False(File.Exists(destinationPath));
            Assert.False(File.Exists(destinationPath + ".part"));
        }

        [Fact]
        public async Task DownloadAndVerifyAsync_HashMismatch_PreservesExistingDestination()
        {
            var originalContent = Encoding.UTF8.GetBytes("original");
            var expectedContent = Encoding.UTF8.GetBytes("expected");
            var actualContent = Encoding.UTF8.GetBytes("modified");
            var destinationPath = Path.Combine(
                _workDirectory,
                "downloads",
                "provider.dll");
            var destinationDirectory = Path.GetDirectoryName(destinationPath);

            if (destinationDirectory == null)
            {
                throw new InvalidOperationException(
                    "테스트 다운로드 디렉터리를 계산할 수 없습니다.");
            }

            Directory.CreateDirectory(destinationDirectory);
            File.WriteAllBytes(destinationPath, originalContent);

            using (var service = CreateService(
                HttpStatusCode.OK,
                actualContent))
            {
                await Assert.ThrowsAsync<UpdateDownloadException>(
                    () => service.DownloadAndVerifyAsync(
                        CreateRequest(destinationPath, expectedContent),
                        CancellationToken.None));
            }

            Assert.Equal(
                originalContent,
                File.ReadAllBytes(destinationPath));
            Assert.False(File.Exists(destinationPath + ".part"));
        }

        [Fact]
        public async Task DownloadAndVerifyAsync_SizeMismatch_DoesNotPublishFile()
        {
            var expectedContent = Encoding.UTF8.GetBytes("expected-long");
            var actualContent = Encoding.UTF8.GetBytes("short");
            var destinationPath = Path.Combine(
                _workDirectory,
                "downloads",
                "provider.dll");

            using (var service = CreateService(
                HttpStatusCode.OK,
                actualContent))
            {
                await Assert.ThrowsAsync<UpdateDownloadException>(
                    () => service.DownloadAndVerifyAsync(
                        CreateRequest(destinationPath, expectedContent),
                        CancellationToken.None));
            }

            Assert.False(File.Exists(destinationPath));
            Assert.False(File.Exists(destinationPath + ".part"));
        }

        [Fact]
        public async Task DownloadAndVerifyAsync_HttpFailure_DoesNotCreateDestination()
        {
            var content = Encoding.UTF8.GetBytes("not found");
            var destinationPath = Path.Combine(
                _workDirectory,
                "downloads",
                "file.bin");

            using (var service = CreateService(
                HttpStatusCode.NotFound,
                content))
            {
                await Assert.ThrowsAsync<UpdateDownloadException>(
                    () => service.DownloadAndVerifyAsync(
                        CreateRequest(destinationPath, content),
                        CancellationToken.None));
            }

            Assert.False(File.Exists(destinationPath));
            Assert.False(File.Exists(destinationPath + ".part"));
        }

        [Theory]
        [InlineData("ftp://update.poscam.co.kr/file.bin")]
        [InlineData("file:///C:/temp/file.bin")]
        [InlineData("not-a-url")]
        public async Task DownloadAndVerifyAsync_UnsafeUrl_IsRejected(
            string downloadUrl)
        {
            var content = Encoding.UTF8.GetBytes("content");
            var request = CreateRequest(
                Path.Combine(_workDirectory, "file.bin"),
                content);
            request.DownloadUrl = downloadUrl;

            using (var service = CreateService(
                HttpStatusCode.OK,
                content))
            {
                await Assert.ThrowsAsync<UpdateDownloadException>(
                    () => service.DownloadAndVerifyAsync(
                        request,
                        CancellationToken.None));
            }
        }

        [Fact]
        public async Task DownloadAndVerifyAsync_InvalidSha256_IsRejected()
        {
            var content = Encoding.UTF8.GetBytes("content");
            var request = CreateRequest(
                Path.Combine(_workDirectory, "file.bin"),
                content);
            request.ExpectedSha256 = "INVALID";

            using (var service = CreateService(
                HttpStatusCode.OK,
                content))
            {
                await Assert.ThrowsAsync<UpdateDownloadException>(
                    () => service.DownloadAndVerifyAsync(
                        request,
                        CancellationToken.None));
            }
        }

        [Fact]
        public async Task DownloadAndVerifyAsync_PreCanceled_LeavesNoPartialFile()
        {
            var content = Encoding.UTF8.GetBytes("content");
            var destinationPath = Path.Combine(
                _workDirectory,
                "downloads",
                "file.bin");
            var cancellation = new CancellationTokenSource();
            cancellation.Cancel();

            using (var service = CreateService(
                HttpStatusCode.OK,
                content))
            {
                await Assert.ThrowsAnyAsync<OperationCanceledException>(
                    () => service.DownloadAndVerifyAsync(
                        CreateRequest(destinationPath, content),
                        cancellation.Token));
            }

            Assert.False(File.Exists(destinationPath));
            Assert.False(File.Exists(destinationPath + ".part"));
        }

        private static UpdateFileDownloadService CreateService(
            HttpStatusCode statusCode,
            byte[] content)
        {
            var handler = new StubHttpMessageHandler(
                _ => new HttpResponseMessage(statusCode)
                {
                    Content = new ByteArrayContent(content)
                });

            return new UpdateFileDownloadService(
                new HttpClient(handler),
                new FileHashCalculator(),
                true);
        }

        private static UpdateFileDownloadRequest CreateRequest(
            string destinationPath,
            byte[] expectedContent)
        {
            return new UpdateFileDownloadRequest
            {
                DownloadUrl =
                    "https://update.poscam.co.kr/packages/test/file.bin",
                DestinationPath = destinationPath,
                ExpectedSize = expectedContent.LongLength,
                ExpectedSha256 = CalculateSha256(expectedContent)
            };
        }

        private static string CalculateSha256(byte[] content)
        {
            using (var sha256 = SHA256.Create())
            {
                var hash = sha256.ComputeHash(content);
                var builder = new StringBuilder(hash.Length * 2);

                foreach (var value in hash)
                {
                    builder.Append(value.ToString("X2"));
                }

                return builder.ToString();
            }
        }

        public void Dispose()
        {
            try
            {
                if (Directory.Exists(_workDirectory))
                {
                    Directory.Delete(_workDirectory, true);
                }
            }
            catch
            {
                // 테스트 정리 실패로 본 테스트 결과를 덮어쓰지 않는다.
            }
        }
    }
}
