using System.Buffers;
using System.Security.Cryptography;

namespace POSCAM.UpdateServer.Api.Storage;

public sealed partial class ArtifactStorageService
{
    public async Task ValidateStoredArtifactAsync(
        string storageKey,
        long expectedFileSize,
        string expectedSha256,
        CancellationToken cancellationToken = default)
    {
        if (expectedFileSize <= 0
            || string.IsNullOrWhiteSpace(expectedSha256)
            || expectedSha256.Length != 64)
        {
            throw IntegrityError("Artifact 무결성 메타데이터가 올바르지 않습니다.");
        }

        string packagePath;

        try
        {
            packagePath = ResolveStorageKey(_packagesPath, storageKey);
        }
        catch (ArtifactStorageException exception)
        {
            throw IntegrityError("Artifact 저장 경로가 올바르지 않습니다.", exception);
        }

        if (!File.Exists(packagePath))
        {
            throw IntegrityError("Artifact 파일을 찾을 수 없습니다.");
        }

        try
        {
            var fileInfo = new FileInfo(packagePath);
            if (fileInfo.Length != expectedFileSize)
            {
                throw IntegrityError("Artifact 파일 크기가 등록 정보와 일치하지 않습니다.");
            }

            await using var stream = new FileStream(
                packagePath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                CopyBufferSize,
                FileOptions.Asynchronous | FileOptions.SequentialScan);

            using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
            var buffer = ArrayPool<byte>.Shared.Rent(CopyBufferSize);

            try
            {
                while (true)
                {
                    var read = await stream.ReadAsync(
                        buffer.AsMemory(0, buffer.Length),
                        cancellationToken);

                    if (read == 0)
                    {
                        break;
                    }

                    hash.AppendData(buffer, 0, read);
                }
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(buffer);
            }

            var actualSha256 = Convert
                .ToHexString(hash.GetHashAndReset())
                .ToLowerInvariant();

            if (!string.Equals(
                    actualSha256,
                    expectedSha256,
                    StringComparison.OrdinalIgnoreCase))
            {
                throw IntegrityError("Artifact SHA-256이 등록 정보와 일치하지 않습니다.");
            }

            try
            {
                await _zipValidator.ValidateAsync(
                    packagePath,
                    _options.MaxArchiveEntries,
                    _options.MaxExpandedBytes,
                    cancellationToken);
            }
            catch (ArtifactStorageException exception)
                when (exception.FailureType == ArtifactStorageFailureType.InvalidPackage)
            {
                throw IntegrityError("Artifact ZIP 무결성 검증에 실패했습니다.", exception);
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
            when (exception is IOException
                  or UnauthorizedAccessException
                  or CryptographicException)
        {
            throw IntegrityError("Artifact 파일 무결성을 확인하지 못했습니다.", exception);
        }
    }

    public Task<QuarantinedArtifactFile> QuarantineAsync(
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
        catch (ArtifactStorageException exception)
        {
            throw StorageError("격리할 Artifact 저장 경로가 올바르지 않습니다.", exception);
        }

        if (!File.Exists(sourcePath))
        {
            return Task.FromResult(new QuarantinedArtifactFile
            {
                OriginalPhysicalPath = sourcePath,
                StorageKey = storageKey,
                FileMoved = false
            });
        }

        var quarantineFileName =
            $"{DateTime.UtcNow:yyyyMMddHHmmssfff}_{ComputeStorageKeyHash(storageKey)}_{Guid.NewGuid():N}.quarantine";
        var quarantinePath = ResolveChildPath(
            _quarantinePath,
            quarantineFileName);

        try
        {
            File.Move(sourcePath, quarantinePath, overwrite: false);

            return Task.FromResult(new QuarantinedArtifactFile
            {
                OriginalPhysicalPath = sourcePath,
                QuarantinePhysicalPath = quarantinePath,
                StorageKey = storageKey,
                FileMoved = true
            });
        }
        catch (Exception exception)
            when (exception is IOException or UnauthorizedAccessException)
        {
            throw StorageError("Artifact 파일을 긴급 격리하지 못했습니다.", exception);
        }
    }

    public Task<bool> RestoreFromQuarantineAsync(
        QuarantinedArtifactFile quarantinedFile,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(quarantinedFile);
        cancellationToken.ThrowIfCancellationRequested();

        if (!quarantinedFile.FileMoved)
        {
            return Task.FromResult(true);
        }

        try
        {
            EnsurePathInside(_packagesPath, quarantinedFile.OriginalPhysicalPath);
            EnsurePathInside(_quarantinePath, quarantinedFile.QuarantinePhysicalPath);

            if (!File.Exists(quarantinedFile.QuarantinePhysicalPath)
                || File.Exists(quarantinedFile.OriginalPhysicalPath))
            {
                return Task.FromResult(false);
            }

            var parentDirectory = Path.GetDirectoryName(
                quarantinedFile.OriginalPhysicalPath);

            if (string.IsNullOrWhiteSpace(parentDirectory))
            {
                return Task.FromResult(false);
            }

            Directory.CreateDirectory(parentDirectory);
            File.Move(
                quarantinedFile.QuarantinePhysicalPath,
                quarantinedFile.OriginalPhysicalPath,
                overwrite: false);

            return Task.FromResult(true);
        }
        catch (Exception exception)
            when (exception is IOException
                  or UnauthorizedAccessException
                  or ArtifactStorageException)
        {
            _logger.LogCritical(
                "DB Rollback 후 Artifact 격리 파일 복구에 실패했습니다. StorageKeyHash: {StorageKeyHash}",
                ComputeStorageKeyHash(quarantinedFile.StorageKey));

            return Task.FromResult(false);
        }
    }

    private static ArtifactStorageException IntegrityError(
        string message,
        Exception? innerException = null)
    {
        return new ArtifactStorageException(
            ArtifactStorageFailureType.PackageIntegrityError,
            message,
            innerException);
    }
}
