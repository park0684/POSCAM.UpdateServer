using POSCAM.UpdateServer.Api.Models.Enums;

namespace POSCAM.UpdateServer.Api.Models.Queries;

/// <summary>
/// 관리자 릴리스 목록 조회에 사용하는 검증 완료 조건.
/// </summary>
public sealed class ReleaseSearchCriteria
{
    public string? ProductCode { get; init; }

    public string? Channel { get; init; }

    public ReleaseStatus? Status { get; init; }

    public string? Keyword { get; init; }

    public int Offset { get; init; }

    public int PageSize { get; init; }
}
