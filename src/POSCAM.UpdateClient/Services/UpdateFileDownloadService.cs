using System;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using POSCAM.UpdateClient.Models;

namespace POSCAM.UpdateClient.Services
{
    /// <summary>
    /// 업데이트 파일을 임시 작업 경로로 다운로드한 뒤 크기와 SHA-256을 검증한다.
    /// 검증이 완료되기 전에는 최종 다운로드 경로를 노출하지 않는다.
    /// </summary>
    internal sealed class UpdateFileDownloadService : IDisposable
    {
        private const int BufferSize = 81920;

        private readonly HttpClient _httpClient;
        private readonly FileHashCalculator _fileHashCalculator;
        private readonly bool _ownsHttpClient;

        public UpdateFileDownloadService()
            : this(
                new HttpClient
                {
                    Timeout = TimeSpan.FromMinutes(5)
                },
                new FileHashCalculator(),
                true)
        {
        }

        internal UpdateFileDownloadService(
            HttpClient httpClient,
            FileHashCalculator fileHashCalculator,
            bool ownsHttpClient = false)
        {
            _httpClient = httpClient
                ?? throw new ArgumentNullException(nameof(httpClient));
            _fileHashCalculator = fileHashCalculator
                ?? throw new ArgumentNullException(nameof(fileHashCalculator));
            _ownsHttpClient = ownsHttpClient;
        }

        public async Task<string> DownloadAndVerifyAsync(
            UpdateFileDownloadRequest request,
            CancellationToken cancellationToken)
        {
            if (request == null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            var downloadUri = ValidateRequest(request);
            var expectedSha256 = request.ExpectedSha256.Trim();
            var destinationPath = Path.GetFullPath(
                request.DestinationPath.Trim());
            var destinationDirectory = Path.GetDirectoryName(destinationPath);

            if (string.IsNullOrWhiteSpace(destinationDirectory))
            {
                throw new UpdateDownloadException(
                    "다운로드 대상 디렉터리를 확인할 수 없습니다.");
            }

            Directory.CreateDirectory(destinationDirectory);

            var partialPath = destinationPath + ".part";
            DeleteIfExists(partialPath);

            try
            {
                using (var response = await _httpClient
                    .GetAsync(
                        downloadUri,
                        HttpCompletionOption.ResponseHeadersRead,
                        cancellationToken)
                    .ConfigureAwait(false))
                {
                    if (!response.IsSuccessStatusCode)
                    {
                        throw new UpdateDownloadException(
                            "업데이트 파일 다운로드 요청이 실패했습니다. HTTP "
                            + (int)response.StatusCode);
                    }

                    var contentLength = response.Content.Headers.ContentLength;

                    if (contentLength.HasValue
                        && contentLength.Value != request.ExpectedSize)
                    {
                        throw new UpdateDownloadException(
                            "다운로드 응답의 파일 크기가 예상 값과 다릅니다.");
                    }

                    using (var source = await response.Content
                        .ReadAsStreamAsync()
                        .ConfigureAwait(false))
                    using (var destination = new FileStream(
                        partialPath,
                        FileMode.CreateNew,
                        FileAccess.Write,
                        FileShare.None,
                        BufferSize,
                        FileOptions.SequentialScan))
                    {
                        await CopyToAsync(
                            source,
                            destination,
                            cancellationToken)
                            .ConfigureAwait(false);
                    }
                }

                var downloadedFile = new FileInfo(partialPath);

                if (downloadedFile.Length != request.ExpectedSize)
                {
                    throw new UpdateDownloadException(
                        "다운로드된 파일 크기가 예상 값과 다릅니다.");
                }

                var actualSha256 = _fileHashCalculator
                    .CalculateSha256(partialPath);

                if (!string.Equals(
                    actualSha256,
                    expectedSha256,
                    StringComparison.OrdinalIgnoreCase))
                {
                    throw new UpdateDownloadException(
                        "다운로드된 파일의 SHA-256이 예상 값과 다릅니다.");
                }

                DeleteIfExists(destinationPath);
                File.Move(partialPath, destinationPath);

                return destinationPath;
            }
            catch (OperationCanceledException)
            {
                DeleteIfExists(partialPath);
                throw;
            }
            catch (UpdateDownloadException)
            {
                DeleteIfExists(partialPath);
                throw;
            }
            catch (Exception exception)
                when (exception is HttpRequestException
                    || exception is IOException
                    || exception is UnauthorizedAccessException
                    || exception is NotSupportedException)
            {
                DeleteIfExists(partialPath);

                throw new UpdateDownloadException(
                    "업데이트 파일 다운로드 또는 저장 중 오류가 발생했습니다.",
                    exception);
            }
        }

        public void Dispose()
        {
            if (_ownsHttpClient)
            {
                _httpClient.Dispose();
            }
        }

        private static Uri ValidateRequest(UpdateFileDownloadRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.DownloadUrl))
            {
                throw new UpdateDownloadException(
                    "다운로드 URL이 비어 있습니다.");
            }

            Uri? downloadUri;

            if (!Uri.TryCreate(
                    request.DownloadUrl.Trim(),
                    UriKind.Absolute,
                    out downloadUri)
                || downloadUri == null)
            {
                throw new UpdateDownloadException(
                    "다운로드 URL이 올바르지 않습니다.");
            }

            if (!string.Equals(
                    downloadUri.Scheme,
                    Uri.UriSchemeHttp,
                    StringComparison.OrdinalIgnoreCase)
                && !string.Equals(
                    downloadUri.Scheme,
                    Uri.UriSchemeHttps,
                    StringComparison.OrdinalIgnoreCase))
            {
                throw new UpdateDownloadException(
                    "다운로드 URL은 HTTP 또는 HTTPS만 사용할 수 있습니다.");
            }

            if (string.IsNullOrWhiteSpace(request.DestinationPath))
            {
                throw new UpdateDownloadException(
                    "다운로드 대상 경로가 비어 있습니다.");
            }

            if (request.ExpectedSize < 0)
            {
                throw new UpdateDownloadException(
                    "예상 파일 크기는 음수일 수 없습니다.");
            }

            if (!IsValidSha256(request.ExpectedSha256))
            {
                throw new UpdateDownloadException(
                    "예상 SHA-256 값이 올바르지 않습니다.");
            }

            return downloadUri;
        }

        private static bool IsValidSha256(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return false;
            }

            var normalized = value.Trim();

            if (normalized.Length != 64)
            {
                return false;
            }

            foreach (var character in normalized)
            {
                var isHex =
                    character >= '0' && character <= '9'
                    || character >= 'a' && character <= 'f'
                    || character >= 'A' && character <= 'F';

                if (!isHex)
                {
                    return false;
                }
            }

            return true;
        }

        private static async Task CopyToAsync(
            Stream source,
            Stream destination,
            CancellationToken cancellationToken)
        {
            var buffer = new byte[BufferSize];

            while (true)
            {
                var read = await source.ReadAsync(
                    buffer,
                    0,
                    buffer.Length,
                    cancellationToken)
                    .ConfigureAwait(false);

                if (read == 0)
                {
                    break;
                }

                await destination.WriteAsync(
                    buffer,
                    0,
                    read,
                    cancellationToken)
                    .ConfigureAwait(false);
            }
        }

        private static void DeleteIfExists(string path)
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }
}
