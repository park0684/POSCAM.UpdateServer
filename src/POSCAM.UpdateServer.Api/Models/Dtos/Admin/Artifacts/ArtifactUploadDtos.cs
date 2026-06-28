using Microsoft.AspNetCore.Mvc;

namespace POSCAM.UpdateServer.Api.Models.Dtos.Admin.Artifacts;

public sealed class ArtifactUploadRequest
{
    [FromForm(Name = "os")]
    public string? Os { get; set; }

    [FromForm(Name = "architecture")]
    public string? Architecture { get; set; }

    [FromForm(Name = "packageType")]
    public string? PackageType { get; set; }

    [FromForm(Name = "file")]
    public IFormFile? File { get; set; }
}

public sealed class ArtifactUploadResponse
{
    public long ArtifactCode { get; init; }
    public long ReleaseCode { get; init; }
    public string PublicId { get; init; } = string.Empty;
    public string Os { get; init; } = string.Empty;
    public string Architecture { get; init; } = string.Empty;
    public string PackageType { get; init; } = string.Empty;
    public string FileName { get; init; } = string.Empty;
    public long FileSize { get; init; }
    public string Sha256 { get; init; } = string.Empty;
    public bool Replaced { get; init; }
}
