namespace POSCAM.UpdateServer.Api.Storage;

public interface IZipPackageValidator
{
    Task ValidateAsync(
        string filePath,
        int maxArchiveEntries,
        long maxExpandedBytes,
        CancellationToken cancellationToken = default);
}
