using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace POSCAM.UpdateClient.Tests.Integration
{
    internal sealed class MockUpdateServer : IDisposable
    {
        private const string CheckPath =
            "/api/v1/updates/check";
        private const string DownloadPath =
            "/files/payload.bin";

        private readonly TcpListener _listener;
        private readonly CancellationTokenSource _stopSource;
        private readonly Task _serverTask;
        private readonly string _latestVersion;
        private readonly string _manifestPath;
        private readonly byte[] _payload;
        private readonly string _payloadSha256;

        private int _checkRequestCount;
        private int _downloadRequestCount;

        public MockUpdateServer(
            string latestVersion,
            string manifestPath,
            byte[] payload)
        {
            if (string.IsNullOrWhiteSpace(latestVersion))
            {
                throw new ArgumentException(
                    "테스트 최신 버전이 비어 있습니다.",
                    nameof(latestVersion));
            }

            if (string.IsNullOrWhiteSpace(manifestPath))
            {
                throw new ArgumentException(
                    "테스트 Manifest 경로가 비어 있습니다.",
                    nameof(manifestPath));
            }

            _latestVersion = latestVersion.Trim();
            _manifestPath = manifestPath.Trim().Replace('\\', '/');
            _payload = payload == null
                ? throw new ArgumentNullException(nameof(payload))
                : (byte[])payload.Clone();
            _payloadSha256 = CalculateSha256(_payload);
            _stopSource = new CancellationTokenSource();
            _listener = new TcpListener(
                IPAddress.Loopback,
                0);
            _listener.Start();

            var endpoint = (IPEndPoint)_listener.LocalEndpoint;
            BaseUri = new Uri(
                "http://127.0.0.1:"
                + endpoint.Port
                + "/",
                UriKind.Absolute);

            _serverTask = Task.Run(
                () => RunServerAsync(_stopSource.Token));
        }

        public Uri BaseUri { get; }

        public int CheckRequestCount =>
            Volatile.Read(ref _checkRequestCount);

        public int DownloadRequestCount =>
            Volatile.Read(ref _downloadRequestCount);

        public void Dispose()
        {
            _stopSource.Cancel();

            try
            {
                _listener.Stop();
            }
            catch
            {
                // 이미 중지된 listener 정리는 무시한다.
            }

            try
            {
                _serverTask.Wait(TimeSpan.FromSeconds(5));
            }
            catch
            {
                // 테스트 본문 결과를 서버 종료 예외로 덮어쓰지 않는다.
            }

            _stopSource.Dispose();
        }

        private async Task RunServerAsync(
            CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                TcpClient client;

                try
                {
                    client = await _listener
                        .AcceptTcpClientAsync()
                        .ConfigureAwait(false);
                }
                catch (ObjectDisposedException)
                {
                    return;
                }
                catch (SocketException)
                    when (cancellationToken.IsCancellationRequested)
                {
                    return;
                }

                using (client)
                {
                    await HandleClientAsync(
                        client,
                        cancellationToken)
                        .ConfigureAwait(false);
                }
            }
        }

        private async Task HandleClientAsync(
            TcpClient client,
            CancellationToken cancellationToken)
        {
            client.ReceiveTimeout = 15000;
            client.SendTimeout = 15000;

            using (var stream = client.GetStream())
            using (var reader = new StreamReader(
                stream,
                new UTF8Encoding(false),
                false,
                4096,
                true))
            {
                var requestLine = await reader
                    .ReadLineAsync()
                    .ConfigureAwait(false);

                if (string.IsNullOrWhiteSpace(requestLine))
                {
                    return;
                }

                var requestParts = requestLine.Split(' ');

                if (requestParts.Length < 2)
                {
                    await WriteTextResponseAsync(
                        stream,
                        400,
                        "Bad Request",
                        "Invalid request",
                        cancellationToken)
                        .ConfigureAwait(false);
                    return;
                }

                var method = requestParts[0]
                    .Trim()
                    .ToUpperInvariant();
                var requestPath = NormalizeRequestPath(
                    requestParts[1]);
                var headers = await ReadHeadersAsync(reader)
                    .ConfigureAwait(false);

                if (headers.TryGetValue(
                    "Content-Length",
                    out var contentLengthText)
                    && int.TryParse(
                        contentLengthText,
                        out var contentLength)
                    && contentLength > 0)
                {
                    await ReadBodyAsync(
                        reader,
                        contentLength)
                        .ConfigureAwait(false);
                }

                if (string.Equals(
                        method,
                        "POST",
                        StringComparison.Ordinal)
                    && string.Equals(
                        requestPath,
                        CheckPath,
                        StringComparison.Ordinal))
                {
                    Interlocked.Increment(
                        ref _checkRequestCount);

                    await WriteJsonResponseAsync(
                        stream,
                        CreateCheckResponse(),
                        cancellationToken)
                        .ConfigureAwait(false);
                    return;
                }

                if (string.Equals(
                        method,
                        "GET",
                        StringComparison.Ordinal)
                    && string.Equals(
                        requestPath,
                        DownloadPath,
                        StringComparison.Ordinal))
                {
                    Interlocked.Increment(
                        ref _downloadRequestCount);

                    await WriteResponseAsync(
                        stream,
                        200,
                        "OK",
                        "application/octet-stream",
                        _payload,
                        cancellationToken)
                        .ConfigureAwait(false);
                    return;
                }

                await WriteTextResponseAsync(
                    stream,
                    404,
                    "Not Found",
                    "Not found",
                    cancellationToken)
                    .ConfigureAwait(false);
            }
        }

        private object CreateCheckResponse()
        {
            return new
            {
                success = true,
                message = "OK",
                errorCode = 0,
                data = new
                {
                    updateAvailable = false,
                    mandatory = false,
                    reasonCode = "LATEST",
                    productCode = "PCCAM",
                    currentVersion = _latestVersion,
                    latestVersion = _latestVersion,
                    forceUpdateBelowVersion = (string?)null,
                    channel = "stable",
                    os = "windows",
                    architecture = "x86",
                    packageType = (string?)null,
                    packageUrl = (string?)null,
                    fileName = (string?)null,
                    fileSize = (long?)null,
                    sha256 = (string?)null,
                    releaseNotes =
                        "UpdateClient process integration test",
                    publishedAt = (DateTime?)null,
                    files = new[]
                    {
                        new
                        {
                            path = _manifestPath,
                            size = _payload.LongLength,
                            sha256 = _payloadSha256,
                            required = true,
                            downloadUrl = new Uri(
                                BaseUri,
                                DownloadPath.TrimStart('/'))
                                .AbsoluteUri
                        }
                    }
                }
            };
        }

        private static async Task<Dictionary<string, string>>
            ReadHeadersAsync(StreamReader reader)
        {
            var headers = new Dictionary<string, string>(
                StringComparer.OrdinalIgnoreCase);

            while (true)
            {
                var line = await reader
                    .ReadLineAsync()
                    .ConfigureAwait(false);

                if (line == null || line.Length == 0)
                {
                    return headers;
                }

                var separatorIndex = line.IndexOf(':');

                if (separatorIndex <= 0)
                {
                    continue;
                }

                headers[line.Substring(0, separatorIndex).Trim()] =
                    line.Substring(separatorIndex + 1).Trim();
            }
        }

        private static async Task ReadBodyAsync(
            StreamReader reader,
            int contentLength)
        {
            var buffer = new char[Math.Min(contentLength, 4096)];
            var remaining = contentLength;

            while (remaining > 0)
            {
                var read = await reader.ReadAsync(
                    buffer,
                    0,
                    Math.Min(buffer.Length, remaining))
                    .ConfigureAwait(false);

                if (read <= 0)
                {
                    return;
                }

                remaining -= read;
            }
        }

        private static string NormalizeRequestPath(
            string requestTarget)
        {
            if (Uri.TryCreate(
                requestTarget,
                UriKind.Absolute,
                out var absoluteUri))
            {
                return absoluteUri.AbsolutePath;
            }

            var questionMarkIndex = requestTarget.IndexOf('?');

            return questionMarkIndex < 0
                ? requestTarget
                : requestTarget.Substring(0, questionMarkIndex);
        }

        private static Task WriteJsonResponseAsync(
            NetworkStream stream,
            object value,
            CancellationToken cancellationToken)
        {
            var json = JsonConvert.SerializeObject(value);

            return WriteResponseAsync(
                stream,
                200,
                "OK",
                "application/json; charset=utf-8",
                Encoding.UTF8.GetBytes(json),
                cancellationToken);
        }

        private static Task WriteTextResponseAsync(
            NetworkStream stream,
            int statusCode,
            string statusText,
            string value,
            CancellationToken cancellationToken)
        {
            return WriteResponseAsync(
                stream,
                statusCode,
                statusText,
                "text/plain; charset=utf-8",
                Encoding.UTF8.GetBytes(value),
                cancellationToken);
        }

        private static async Task WriteResponseAsync(
            NetworkStream stream,
            int statusCode,
            string statusText,
            string contentType,
            byte[] body,
            CancellationToken cancellationToken)
        {
            var headerText =
                "HTTP/1.1 "
                + statusCode
                + " "
                + statusText
                + "\r\nContent-Type: "
                + contentType
                + "\r\nContent-Length: "
                + body.Length
                + "\r\nConnection: close"
                + "\r\nCache-Control: no-store"
                + "\r\n\r\n";
            var header = Encoding.ASCII.GetBytes(headerText);

            await stream.WriteAsync(
                header,
                0,
                header.Length,
                cancellationToken)
                .ConfigureAwait(false);

            if (body.Length > 0)
            {
                await stream.WriteAsync(
                    body,
                    0,
                    body.Length,
                    cancellationToken)
                    .ConfigureAwait(false);
            }

            await stream.FlushAsync(cancellationToken)
                .ConfigureAwait(false);
        }

        private static string CalculateSha256(byte[] value)
        {
            using (var sha256 = SHA256.Create())
            {
                var hash = sha256.ComputeHash(value);
                var builder = new StringBuilder(hash.Length * 2);

                foreach (var current in hash)
                {
                    builder.Append(current.ToString("X2"));
                }

                return builder.ToString();
            }
        }
    }
}
