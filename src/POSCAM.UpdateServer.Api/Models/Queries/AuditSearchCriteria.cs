namespace POSCAM.UpdateServer.Api.Models.Queries;

public sealed class AuditSearchCriteria
{
    public string? Action { get; init; }

    public string? TargetType { get; init; }

    public string? TargetCode { get; init; }

    public int? ActorUserCode { get; init; }

    public string? RequestId { get; init; }

    public DateTime? FromUtc { get; init; }

    public DateTime? ToUtc { get; init; }

    public int Offset { get; init; }

    public int PageSize { get; init; }
}
