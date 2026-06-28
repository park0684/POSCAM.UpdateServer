namespace POSCAM.UpdateServer.Api.Models.Dtos.Admin.Lifecycle;

public sealed class ReleaseLifecycleResponse
{
    public long ReleaseCode { get; init; }

    public int Status { get; init; }

    public string StatusName { get; init; } = string.Empty;

    public DateTime? PublishedAt { get; init; }
}

public sealed class QuarantineArtifactResponse
{
    public long ArtifactCode { get; init; }

    public long ReleaseCode { get; init; }

    public int ArtifactStatus { get; init; }

    public int ReleaseStatus { get; init; }

    public bool FileMoved { get; init; }
}
