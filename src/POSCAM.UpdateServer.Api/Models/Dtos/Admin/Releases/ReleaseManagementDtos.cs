namespace POSCAM.UpdateServer.Api.Models.Dtos.Admin.Releases;

public sealed class ActiveProductResponse
{
    public string ProductCode { get; init; } = string.Empty;
    public string ProductName { get; init; } = string.Empty;
    public string? ProductDescription { get; init; }
}

public sealed class ReleaseListRequest
{
    public string? ProductCode { get; set; }
    public string? Channel { get; set; }
    public int? Status { get; set; }
    public string? Keyword { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
}

public sealed class CreateReleaseRequest
{
    public string? ProductCode { get; set; }
    public string? Version { get; set; }
    public string? Channel { get; set; }
    public bool IsMandatory { get; set; }
    public string? ForceUpdateBelowVersion { get; set; }
    public string? ReleaseNotes { get; set; }
    public string? InternalMemo { get; set; }
}

public sealed class UpdateReleaseRequest
{
    public string? ProductCode { get; set; }
    public string? Version { get; set; }
    public string? Channel { get; set; }
    public bool IsMandatory { get; set; }
    public string? ForceUpdateBelowVersion { get; set; }
    public string? ReleaseNotes { get; set; }
    public string? InternalMemo { get; set; }
}

public sealed class ReleaseListItemResponse
{
    public long ReleaseCode { get; init; }
    public string ProductCode { get; init; } = string.Empty;
    public string Version { get; init; } = string.Empty;
    public string Channel { get; init; } = string.Empty;
    public bool IsMandatory { get; init; }
    public string? ForceUpdateBelowVersion { get; init; }
    public int Status { get; init; }
    public string StatusName { get; init; } = string.Empty;
    public DateTime? PublishedAt { get; init; }
    public int? CreatedByUserCode { get; init; }
    public string? CreatedByUserName { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime? UpdatedAt { get; init; }
}

public sealed class ReleaseArtifactSummaryResponse
{
    public long ArtifactCode { get; init; }
    public string PublicId { get; init; } = string.Empty;
    public string Os { get; init; } = string.Empty;
    public string Architecture { get; init; } = string.Empty;
    public string PackageType { get; init; } = string.Empty;
    public string FileName { get; init; } = string.Empty;
    public long FileSize { get; init; }
    public string Sha256 { get; init; } = string.Empty;
    public int Status { get; init; }
    public DateTime CreatedAt { get; init; }
}

public sealed class ReleaseDetailResponse
{
    public long ReleaseCode { get; init; }
    public string ProductCode { get; init; } = string.Empty;
    public string Version { get; init; } = string.Empty;
    public string Channel { get; init; } = string.Empty;
    public bool IsMandatory { get; init; }
    public string? ForceUpdateBelowVersion { get; init; }
    public string? ReleaseNotes { get; init; }
    public string? InternalMemo { get; init; }
    public int Status { get; init; }
    public string StatusName { get; init; } = string.Empty;
    public DateTime? PublishedAt { get; init; }
    public int? CreatedByUserCode { get; init; }
    public string? CreatedByUserName { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime? UpdatedAt { get; init; }
    public IReadOnlyList<ReleaseArtifactSummaryResponse> Artifacts { get; init; } = Array.Empty<ReleaseArtifactSummaryResponse>();
}

public sealed class DeleteReleaseResponse
{
    public long ReleaseCode { get; init; }
}
