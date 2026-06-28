namespace POSCAM.UpdateServer.Api.Models.Entities;

/// <summary>
/// update_audit_logs 테이블과 대응하는 append-only 감사 Entity.
/// AuthServer가 확인한 작업자 정보는 당시 스냅샷으로 저장한다.
/// </summary>
public sealed class UpdateAuditLog
{
    public long AuditLogCode { get; set; }

    public string Action { get; set; } = string.Empty;

    public string TargetType { get; set; } = string.Empty;

    public string TargetCode { get; set; } = string.Empty;

    public int? ActorUserCode { get; set; }

    public string? ActorUserName { get; set; }

    public string? BeforeData { get; set; }

    public string? AfterData { get; set; }

    public string? IpAddress { get; set; }

    public string? UserAgent { get; set; }

    public string? RequestId { get; set; }

    public DateTime CreatedAt { get; set; }
}
