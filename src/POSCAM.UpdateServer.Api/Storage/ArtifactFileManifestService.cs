using System.Buffers;
using System.IO.Compression;
using System.Security.Cryptography;
using Microsoft.Extensions.Options;
using POSCAM.UpdateServer.Api.Options;

namespace POSCAM.UpdateServer.Api.Storage;

/// <summary>
/// 검증된 Artifact ZIP을 분석해 파일별 복구 대상 Manifest를 만들고
/// 개별 다운로드 가능한 packages 하위 파일로 저장한다.
/// </summary>
public sealed class ArtifactFileManifestService : IArtifactFileManifestService
{
    private const int CopyBufferSize = 128 * 1024;

    private static readonly HashSet<string> ExcludedDirectorySegments = new(
        StringComparer.OrdinalIgnoreCase)
    {
        "config",
        "configs",
        "log",
        "logs",
        "cache",
        "temp",
        "tmp",
        "token",
        "tokens",
        "auth",
        "localdb",
        "database",
        "data"
    };

    private static readonly HashSet<string> IncludedFileNames = new(
        StringComparer.OrdinalIgnoreCase)
    {
        "ffmpeg.exe",
        "mediamtx.exe"
    };

    private readonly ILogger<ArtifactFileManifestService> _logger;
    private readonly string _packagesPath;

    public ArtifactFileManifestService(
        IOptions<UpdateStorageOptions> options,
        ILogger<ArtifactFileManifestService> logger)
    {
        ArgumentNullException.ThrowIfNull(options);

        _logger = logger;

        var rootPath = Path.GetFullPath(options.Value.RootPath);
        _packagesPath = ResolveChildPath(rootPath, "packages");
    }

    public async Task<IReadOnlyList<ArtifactFileManifestEntry>> CreateManifestFilesAsync(
        string zipFilePath,
        ArtifactStorageDestination artifactDestination,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(zipFilePath))
        {
            throw InvalidPackage("ZIP 파일 경로가 올바르지 않습니다.");
        }

        ArgumentNullException.ThrowIfNull(artifactDestination);

        if (string.IsNullOrWhiteSpace(artifactDestination.StorageKey))
        {
            throw StorageError("Artifact Storage Key가 올바르지 않습니다.");
        }

        var artifactPrefix = GetArtifactStoragePrefix(artifactDestination.StorageKey);
        var createdFiles = new List<ArtifactFileManifestEntry>();
        var includedPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        try
        {
            Directory.CreateDirectory(_packagesPath);

            await using var zipStream = new FileStream(
                zipFilePath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                CopyBufferSize,
                FileOptions.Asynchronous | FileOptions.SequentialScan);

            using var archive = new ZipArchive(
                zipStream,
                ZipArchiveMode.Read,
                leaveOpen: false);

            foreach (var entry in archive.Entries)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (IsDirectoryEntry(entry))
                {
                    continue;
                }

                var normalizedPath = NormalizeEntryPath(entry.FullName);
                if (!IsManifestTarget(normalizedPath))
                {
                    continue;
                }

                if (!includedPaths.Add(normalizedPath))
                {
                    throw InvalidPackage("ZIP 내부 파일 경로가 중복되었습니다.");
                }

                var filePublicId = Guid.NewGuid().ToString("N");
                var storageKey = string.Join(
                    '/',
                    artifactPrefix,
                    "files",
                    filePublicId);
                var physicalPath = ResolveStorageKey(_packagesPath, storageKey);
                var parentDirectory = Path.GetDirectoryName(physicalPath)
                    ?? throw StorageError("파일별 Manifest 저장 디렉터리를 확인할 수 없습니다.");

                Directory.CreateDirectory(parentDirectory);

                var written = await WriteEntryToFileAsync(
                    entry,
                    physicalPath,
                    cancellationToken);

                var manifestEntry = new ArtifactFileManifestEntry
                {
                    PublicId = filePublicId,
                    FilePath = normalizedPath,
                    FileSize = written.FileSize,
                    Sha256 = written.Sha256,
                    StorageKey = storageKey,
                    DownloadPath = storageKey,
                    IsRequired = true,
                    PhysicalPath = physicalPath
                };

                createdFiles.Add(manifestEntry);
            }

            return createdFiles;
        }
        catch (OperationCanceledException)
        {
            await DeleteManifestFilesAsync(createdFiles, CancellationToken.None);
            throw;
        }
        catch (ArtifactStorageException)
        {
            await DeleteManifestFilesAsync(createdFiles, CancellationToken.None);
            throw;
        }
        catch (Exception exception)
            when (exception is IOException
                  or InvalidDataException
                  or NotSupportedException
                  or UnauthorizedAccessException
                  or CryptographicException)
        {
            await DeleteManifestFilesAsync(createdFiles, CancellationToken.None);

            _logger.LogWarning(
                exception,
                "ZIP 파일별 Manifest 생성 중 오류가 발생했습니다.");

            throw StorageError("ZIP 파일별 Manifest를 생성하지 못했습니다.", exception);
        }
    }

    public Task DeleteManifestFilesAsync(
        IReadOnlyList<ArtifactFileManifestEntry>? files,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (files is null || files.Count == 0)
        {
            return Task.CompletedTask;
        }

        foreach (var file in files)
        {
            try
            {
                var physicalPath = ResolveManifestPhysicalPath(file);
                if (physicalPath is null)
                {
                    continue;
                }

                EnsurePathInside(_packagesPath, physicalPath);

                if (File.Exists(physicalPath))
                {
                    File.Delete(physicalPath);
                }
            }
            catch (Exception exception)
                when (exception is IOException
                      or UnauthorizedAccessException
                      or ArtifactStorageException)
            {
                _logger.LogWarning(
                    exception,
                    "파일별 Manifest 저장 파일을 정리하지 못했습니다. StorageKeyHash: {StorageKeyHash}",
                    ComputeStorageKeyHash(file.StorageKey));
            }
        }

        return Task.CompletedTask;
    }

    private string? ResolveManifestPhysicalPath(ArtifactFileManifestEntry file)
    {
        if (!string.IsNullOrWhiteSpace(file.PhysicalPath))
        {
            return file.PhysicalPath;
        }

        if (!string.IsNullOrWhiteSpace(file.StorageKey))
        {
            return ResolveStorageKey(_packagesPath, file.StorageKey);
        }

        return null;
    }

    private static async Task<(long FileSize, string Sha256)> WriteEntryToFileAsync(
        ZipArchiveEntry entry,
        string destinationPath,
        CancellationToken cancellationToken)
    {
        await using var entryStream = entry.Open();
        await using var outputStream = new FileStream(
            destinationPath,
            FileMode.CreateNew,
            FileAccess.Write,
            FileShare.None,
            CopyBufferSize,
            FileOptions.Asynchronous | FileOptions.SequentialScan);

        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        var buffer = ArrayPool<byte>.Shared.Rent(CopyBufferSize);
        long totalBytes = 0;

        try
        {
            while (true)
            {
                var read = await entryStream.ReadAsync(
                    buffer.AsMemory(0, buffer.Length),
                    cancellationToken);

                if (read == 0)
                {
                    break;
                }

                await outputStream.WriteAsync(
                    buffer.AsMemory(0, read),
                    cancellationToken);

                hash.AppendData(buffer, 0, read);
                totalBytes += read;
            }

            await outputStream.FlushAsync(cancellationToken);
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }

        if (totalBytes != entry.Length)
        {
            throw InvalidPackage("ZIP 항목 크기 정보가 올바르지 않습니다.");
        }

        return (
            totalBytes,
            Convert.ToHexString(hash.GetHashAndReset()).ToLowerInvariant());
    }

    private static bool IsDirectoryEntry(ZipArchiveEntry entry)
    {
        var normalized = entry.FullName.Replace('\\', '/');
        return normalized.EndsWith("/", StringComparison.Ordinal);
    }

    private static string NormalizeEntryPath(string entryPath)
    {
        if (ZipPackageValidator.IsUnsafeEntryPath(entryPath))
        {
            throw InvalidPackage("ZIP 내부 경로가 안전하지 않습니다.");
        }

        var normalized = entryPath.Replace('\\', '/');
        var segments = normalized.Split(
            '/',
            StringSplitOptions.RemoveEmptyEntries);

        if (segments.Length == 0)
        {
            throw InvalidPackage("ZIP 내부 경로가 올바르지 않습니다.");
        }

        return string.Join('/', segments);
    }

    internal static bool IsManifestTarget(string normalizedPath)
    {
        if (string.IsNullOrWhiteSpace(normalizedPath))
        {
            return false;
        }

        var segments = normalizedPath.Split(
            '/',
            StringSplitOptions.RemoveEmptyEntries);

        if (segments.Length == 0
            || segments.Any(segment => ExcludedDirectorySegments.Contains(segment)))
        {
            return false;
        }

        var fileName = segments[^1];
        if (IncludedFileNames.Contains(fileName))
        {
            return true;
        }

        var extension = Path.GetExtension(fileName);
        if (extension.Equals(".exe", StringComparison.OrdinalIgnoreCase)
            || extension.Equals(".dll", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var firstSegment = segments[0];
        return firstSegment.Equals("resources", StringComparison.OrdinalIgnoreCase)
               || firstSegment.Equals("resource", StringComparison.OrdinalIgnoreCase)
               || firstSegment.Equals("templates", StringComparison.OrdinalIgnoreCase)
               || firstSegment.Equals("assets", StringComparison.OrdinalIgnoreCase);
    }

    private static string GetArtifactStoragePrefix(string artifactStorageKey)
    {
        if (string.IsNullOrWhiteSpace(artifactStorageKey)
            || artifactStorageKey.Contains('\\')
            || artifactStorageKey.StartsWith("/", StringComparison.Ordinal))
        {
            throw StorageError("Artifact Storage Key가 올바르지 않습니다.");
        }

        var lastSeparator = artifactStorageKey.LastIndexOf('/');
        if (lastSeparator <= 0)
        {
            throw StorageError("Artifact Storage Key가 올바르지 않습니다.");
        }

        var prefix = artifactStorageKey[..lastSeparator];
        _ = ResolveStorageKey("/", prefix);
        return prefix;
    }

    private static string ResolveStorageKey(
        string rootDirectory,
        string storageKey)
    {
        if (string.IsNullOrWhiteSpace(storageKey)
            || storageKey.Contains('\\')
            || storageKey.StartsWith("/", StringComparison.Ordinal))
        {
            throw StorageError("Storage Key가 올바르지 않습니다.");
        }

        var segments = storageKey.Split(
            '/',
            StringSplitOptions.RemoveEmptyEntries);

        if (segments.Length == 0 || segments.Any(IsInvalidStorageSegment))
        {
            throw StorageError("Storage Key가 올바르지 않습니다.");
        }

        var combined = rootDirectory;
        foreach (var segment in segments)
        {
            combined = Path.Combine(combined, segment);
        }

        var fullPath = Path.GetFullPath(combined);
        EnsurePathInside(rootDirectory, fullPath);
        return fullPath;
    }

    private static bool IsInvalidStorageSegment(string segment)
    {
        return segment is "." or ".."
               || segment.Contains('\0')
               || segment.Contains(':');
    }

    private static string ResolveChildPath(
        string rootDirectory,
        string childName)
    {
        var fullPath = Path.GetFullPath(
            Path.Combine(rootDirectory, childName));
        EnsurePathInside(rootDirectory, fullPath);
        return fullPath;
    }

    private static void EnsurePathInside(
        string rootDirectory,
        string candidatePath)
    {
        var normalizedRoot = Path.GetFullPath(rootDirectory)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            + Path.DirectorySeparatorChar;
        var normalizedCandidate = Path.GetFullPath(candidatePath);
        var comparison = OperatingSystem.IsWindows()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;

        if (!normalizedCandidate.StartsWith(normalizedRoot, comparison))
        {
            throw StorageError("저장소 Root 외부 경로는 사용할 수 없습니다.");
        }
    }

    private static string ComputeStorageKeyHash(string storageKey)
    {
        var hash = SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(storageKey));
        return Convert.ToHexString(hash.AsSpan(0, 8)).ToLowerInvariant();
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

    private static ArtifactStorageException StorageError(
        string message,
        Exception? innerException = null)
    {
        return new ArtifactStorageException(
            ArtifactStorageFailureType.StorageError,
            message,
            innerException);
    }
}
