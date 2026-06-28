using System.Buffers;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Options;
using POSCAM.UpdateServer.Api.Options;

namespace POSCAM.UpdateServer.Api.Storage;

/// <summary>
/// Artifact ZIP의 staging 저장, SHA-256 계산, 검증 후 packages 이동과 실패 정리를 담당한다.
/// </summary>
public sealed partial class ArtifactStorageService : IArtifactStorageService
{
    private const int CopyBufferSize = 128 * 1024;

    [GeneratedRegex("^[A-Z0-9_]+$", RegexOptions.CultureInvariant)]
    private static partial Regex ProductCodeRegex();

    [GeneratedRegex("^[0-9]+(?:\\.[0-9]+){2,3}$", RegexOptions.CultureInvariant)]
    private static partial Regex VersionRegex();

    [GeneratedRegex("^[a-z0-9_-]+$", RegexOptions.CultureInvariant)]
    private static partial Regex LowerCodeRegex();

    private readonly UpdateStorageOptions _options;
    private readonly IZipPackageValidator _zipValidator;
    private readonly ILogger<ArtifactStorageService> _logger;
    private readonly string _rootPath;
    private readonly string _packagesPath;
    private readonly string _stagingPath;
    private readonly string _quarantinePath;

    public ArtifactStorageService(
        IOptions<UpdateStorageOptions> options,
        IZipPackageValidator zipValidator,
        ILogger<ArtifactStorageService> logger)
    {
        _options = options.Value;
        _zipValidator = zipValidator;
        _logger = logger;

        _rootPath = Path.GetFullPath(_options.RootPath);
        _packagesPath = GetChildDirectory(_rootPath, "packages");
        _stagingPath = GetChildDirectory(_rootPath, ".staging");
        _quarantinePath = GetChildDirectory(_rootPath, ".quarantine");
    }

    public ArtifactStorageDestination CreateDestination(
        string productCode,
        string channel,
        string version,
        string architecture)
    {
        if (string.IsNullOrWhiteSpace(productCode)
            || !ProductCodeRegex().IsMatch(productCode)
            || string.IsNullOrWhiteSpace(channel)
            || !LowerCodeRegex().IsMatch(channel)
            || string.IsNullOrWhiteSpace(version)
            || !VersionRegex().IsMatch(version)
            || string.IsNullOrWhiteSpace(architecture)
            || !LowerCodeRegex().IsMatch(architecture))
        {
            throw StorageError("Artifact 저장 경로 구성값이 올바르지 않습니다.");
        }

        var publicId = Guid.NewGuid().ToString("N");
        var fileName = $"{productCode}_{version}_{architecture}.zip";
        var storageKey = string.Join(
            '/',
            productCode.ToLowerInvariant(),
            channel,
            version,
            publicId,
            fileName);

        _ = ResolveStorageKey(_packagesPath, storageKey);

        return new ArtifactStorageDestination
        {
            PublicId = publicId,
            FileName = fileName,
            StorageKey = storageKey
        };
    }

    public async Task<StagedArtifactFile> SaveToStagingAsync(
        Stream source,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);
        EnsureDirectories();

        var stagingFilePath = ResolveChildPath(
            _stagingPath,
            Guid.NewGuid().ToString("N") + ".upload");

        try
        {
            await using var output = new FileStream(
                stagingFilePath,
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
                    var read = await source.ReadAsync(
                        buffer.AsMemory(0, buffer.Length),
                        cancellationToken);

                    if (read == 0)
                    {
                        break;
                    }

                    if (totalBytes > _options.MaxUploadBytes - read)
                    {
                        throw new ArtifactStorageException(
                            ArtifactStorageFailureType.FileTooLarge,
                            "업로드 파일 크기 제한을 초과했습니다.");
                    }

                    await output.WriteAsync(
                        buffer.AsMemory(0, read),
                        cancellationToken);

                    hash.AppendData(buffer, 0, read);
                    totalBytes += read;
                }

                await output.FlushAsync(cancellationToken);
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(buffer);
            }

            if (totalBytes == 0)
            {
                throw new ArtifactStorageException(
                    ArtifactStorageFailureType.InvalidPackage,
                    "빈 파일은 업로드할 수 없습니다.");
            }

            return new StagedArtifactFile
            {
                PhysicalPath = stagingFilePath,
                FileSize = totalBytes,
                Sha256 = Convert.ToHexString(hash.GetHashAndReset()).ToLowerInvariant()
            };
        }
        catch (OperationCanceledException)
        {
            TryDeletePhysicalFile(stagingFilePath);
            throw;
        }
        catch (ArtifactStorageException)
        {
            TryDeletePhysicalFile(stagingFilePath);
            throw;
        }
        catch (Exception exception)
            when (exception is IOException
                  or UnauthorizedAccessException
                  or CryptographicException)
        {
            TryDeletePhysicalFile(stagingFilePath);
            throw StorageError("업로드 파일을 임시 저장하지 못했습니다.", exception);
        }
    }

    public Task ValidatePackageAsync(
        StagedArtifactFile stagedFile,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(stagedFile);
        EnsurePathInside(_stagingPath, stagedFile.PhysicalPath);

        return _zipValidator.ValidateAsync(
            stagedFile.PhysicalPath,
            _options.MaxArchiveEntries,
            _options.MaxExpandedBytes,
            cancellationToken);
    }

    public Task MoveToPackagesAsync(
        StagedArtifactFile stagedFile,
        ArtifactStorageDestination destination,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(stagedFile);
        ArgumentNullException.ThrowIfNull(destination);
        cancellationToken.ThrowIfCancellationRequested();

        EnsureDirectories();
        EnsurePathInside(_stagingPath, stagedFile.PhysicalPath);

        var finalPath = ResolveStorageKey(
            _packagesPath,
            destination.StorageKey);

        try
        {
            var parentDirectory = Path.GetDirectoryName(finalPath)
                ?? throw StorageError("Artifact 최종 디렉터리를 확인할 수 없습니다.");

            Directory.CreateDirectory(parentDirectory);
            File.Move(stagedFile.PhysicalPath, finalPath, overwrite: false);
            cancellationToken.ThrowIfCancellationRequested();
            return Task.CompletedTask;
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
            when (exception is IOException
                  or UnauthorizedAccessException)
        {
            throw StorageError("Artifact 파일을 최종 저장소로 이동하지 못했습니다.", exception);
        }
    }

    public Task DeleteStagingAsync(
        StagedArtifactFile? stagedFile,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (stagedFile is null)
        {
            return Task.CompletedTask;
        }

        try
        {
            EnsurePathInside(_stagingPath, stagedFile.PhysicalPath);
            TryDeletePhysicalFile(stagedFile.PhysicalPath);
        }
        catch (ArtifactStorageException)
        {
            // 잘못된 외부 경로는 삭제하지 않는다.
        }

        return Task.CompletedTask;
    }

    public Task<bool> RemoveOrQuarantineAsync(
        string storageKey,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        EnsureDirectories();

        string sourcePath;
        try
        {
            sourcePath = ResolveStorageKey(_packagesPath, storageKey);
        }
        catch (ArtifactStorageException)
        {
            return Task.FromResult(false);
        }

        if (!File.Exists(sourcePath))
        {
            return Task.FromResult(true);
        }

        try
        {
            File.Delete(sourcePath);
            return Task.FromResult(true);
        }
        catch (Exception deleteException)
            when (deleteException is IOException or UnauthorizedAccessException)
        {
            try
            {
                var quarantineFileName =
                    $"{DateTime.UtcNow:yyyyMMddHHmmssfff}_{Guid.NewGuid():N}.quarantine";
                var quarantinePath = ResolveChildPath(
                    _quarantinePath,
                    quarantineFileName);

                File.Move(sourcePath, quarantinePath, overwrite: false);
                return Task.FromResult(true);
            }
            catch (Exception quarantineException)
                when (quarantineException is IOException or UnauthorizedAccessException)
            {
                _logger.LogError(
                    "Artifact 파일 정리와 격리에 모두 실패했습니다. StorageKeyHash: {StorageKeyHash}",
                    ComputeStorageKeyHash(storageKey));

                return Task.FromResult(false);
            }
        }
    }

    private void EnsureDirectories()
    {
        try
        {
            Directory.CreateDirectory(_rootPath);
            Directory.CreateDirectory(_packagesPath);
            Directory.CreateDirectory(_stagingPath);
            Directory.CreateDirectory(_quarantinePath);
        }
        catch (Exception exception)
            when (exception is IOException or UnauthorizedAccessException)
        {
            throw StorageError("Artifact 저장소 디렉터리를 준비하지 못했습니다.", exception);
        }
    }

    private static string GetChildDirectory(
        string rootPath,
        string childName)
    {
        return ResolveChildPath(rootPath, childName);
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

        if (segments.Length == 0
            || segments.Any(segment => segment is "." or ".." || segment.Contains('\0')))
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

    private static void TryDeletePhysicalFile(string filePath)
    {
        try
        {
            if (File.Exists(filePath))
            {
                File.Delete(filePath);
            }
        }
        catch
        {
            // 원래 업로드 오류를 보존하고 stale staging 정리는 운영 정리 작업에 맡긴다.
        }
    }

    private static string ComputeStorageKeyHash(string storageKey)
    {
        var hash = SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(storageKey));
        return Convert.ToHexString(hash.AsSpan(0, 8)).ToLowerInvariant();
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
