namespace POSCAM.UpdateServer.Api.Models.Dtos.Admin.Audits;

public sealed class AuditListRequest
{
    public string? Action { get; set; }

    public string? TargetType { get; set; }

    public string? TargetCode { get; set; }

    public int? ActorUserCode { get; set; }

    public string? RequestId { get; set; }

    public DateTime? FromUtc { get; set; }

    public DateTime? ToUtc { get; set; }

    public int Page { get; set; } = 1;

    public int PageSize { get; set; } = 50;
}

public sealed class ReleaseAuditListRequest
{
    public string? Action { get; set; }

    public int Page { get; set; } = 1;

    public int PageSize { get; set; } = 50;
}

public sealed class AuditLogResponse
{
    public long AuditLogCode { get; init; }

    public string Action { get; init; } = string.Empty;

    public string TargetType { get; init; } = string.Empty;

    public string TargetCode { get; init; } = string.Empty;

    public int? ActorUserCode { get; init; }

    public string? ActorUserName { get; init; }

    public string? BeforeData { get; init; }

    public string? AfterData { get; init; }

    public string? IpAddress { get; init; }

    public string? UserAgent { get; init; }

    public string? RequestId { get; init; }

    public DateTime CreatedAt { get; init; }
}
