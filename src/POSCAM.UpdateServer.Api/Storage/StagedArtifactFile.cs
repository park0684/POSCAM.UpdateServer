namespace POSCAM.UpdateServer.Api.Storage;

public sealed class StagedArtifactFile
{
    internal string PhysicalPath { get; init; } = string.Empty;

    public long FileSize { get; init; }

    public string Sha256 { get; init; } = string.Empty;
}
