using System.Buffers;
using System.IO.Compression;
using System.Text.RegularExpressions;

namespace POSCAM.UpdateServer.Api.Storage;

/// <summary>
/// ZIP을 추출하거나 실행하지 않고 구조와 모든 Entry Stream을 검사한다.
/// </summary>
public sealed partial class ZipPackageValidator : IZipPackageValidator
{
    [GeneratedRegex("^[A-Za-z]:", RegexOptions.CultureInvariant)]
    private static partial Regex WindowsDrivePathRegex();

    public async Task ValidateAsync(
        string filePath,
        int maxArchiveEntries,
        long maxExpandedBytes,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(filePath)
            || maxArchiveEntries < 1
            || maxExpandedBytes < 1)
        {
            throw InvalidPackage("ZIP 검증 조건이 올바르지 않습니다.");
        }

        try
        {
            await using var fileStream = new FileStream(
                filePath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                bufferSize: 128 * 1024,
                options: FileOptions.Asynchronous | FileOptions.SequentialScan);

            using var archive = new ZipArchive(
                fileStream,
                ZipArchiveMode.Read,
                leaveOpen: false);

            if (archive.Entries.Count == 0)
            {
                throw InvalidPackage("빈 ZIP 패키지는 등록할 수 없습니다.");
            }

            if (archive.Entries.Count > maxArchiveEntries)
            {
                throw InvalidPackage("ZIP 항목 수 제한을 초과했습니다.");
            }

            var buffer = ArrayPool<byte>.Shared.Rent(128 * 1024);
            var hasFileEntry = false;
            long totalExpandedBytes = 0;

            try
            {
                foreach (var entry in archive.Entries)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    if (IsUnsafeEntryPath(entry.FullName))
                    {
                        throw InvalidPackage("ZIP 내부 경로가 안전하지 않습니다.");
                    }

                    var normalizedEntryName = entry.FullName.Replace('\\', '/');
                    var directoryEntry = normalizedEntryName.EndsWith(
                        "/",
                        StringComparison.Ordinal);

                    // 디렉터리 Entry는 이름만 디렉터리 형태일 뿐 실제 데이터가
                    // 포함될 수 있으므로 Stream까지 읽어 0바이트인지 확인한다.
                    // 이를 건너뛰면 Expanded Bytes와 손상 ZIP 검증을 우회할 수 있다.
                    if (directoryEntry && entry.Length != 0)
                    {
                        throw InvalidPackage("ZIP 디렉터리 항목에는 데이터를 포함할 수 없습니다.");
                    }

                    if (!directoryEntry)
                    {
                        hasFileEntry = true;
                        totalExpandedBytes = AddExpandedBytes(
                            totalExpandedBytes,
                            entry.Length,
                            maxExpandedBytes);
                    }

                    await using var entryStream = entry.Open();
                    long actualEntryBytes = 0;

                    while (true)
                    {
                        var read = await entryStream.ReadAsync(
                            buffer.AsMemory(0, buffer.Length),
                            cancellationToken);

                        if (read == 0)
                        {
                            break;
                        }

                        actualEntryBytes = AddExpandedBytes(
                            actualEntryBytes,
                            read,
                            entry.Length);
                    }

                    if (actualEntryBytes != entry.Length)
                    {
                        throw InvalidPackage("ZIP 항목 크기 정보가 올바르지 않습니다.");
                    }

                    if (directoryEntry && actualEntryBytes != 0)
                    {
                        throw InvalidPackage("ZIP 디렉터리 항목에는 데이터를 포함할 수 없습니다.");
                    }
                }
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(buffer);
            }

            if (!hasFileEntry)
            {
                throw InvalidPackage("파일이 없는 ZIP 패키지는 등록할 수 없습니다.");
            }
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (ArtifactStorageException)
        {
            throw;
        }
        catch (Exception exception)
            when (exception is InvalidDataException
                  or IOException
                  or NotSupportedException
                  or UnauthorizedAccessException)
        {
            throw InvalidPackage(
                "손상되었거나 읽을 수 없는 ZIP 패키지입니다.",
                exception);
        }
    }

    internal static bool IsUnsafeEntryPath(string? entryPath)
    {
        if (string.IsNullOrWhiteSpace(entryPath)
            || entryPath.Contains('\0'))
        {
            return true;
        }

        var normalized = entryPath.Replace('\\', '/');

        if (normalized.StartsWith("/", StringComparison.Ordinal)
            || normalized.Contains(':')
            || WindowsDrivePathRegex().IsMatch(normalized)
            || Path.IsPathRooted(entryPath))
        {
            return true;
        }

        var segments = normalized.Split(
            '/',
            StringSplitOptions.RemoveEmptyEntries);

        return segments.Length == 0
               || segments.Any(segment => segment is "." or "..");
    }

    private static long AddExpandedBytes(
        long current,
        long additional,
        long maximum)
    {
        if (additional < 0 || current > maximum - additional)
        {
            throw InvalidPackage("ZIP 해제 예상 크기 제한을 초과했습니다.");
        }

        return current + additional;
    }

    private static ArtifactStorageException InvalidPackage(
        string message,
        Exception? innerException = null)
    {
        return new ArtifactStorageException(
            ArtifactStorageFailureType.InvalidPackage,
            message,
            innerException);
    }
}
