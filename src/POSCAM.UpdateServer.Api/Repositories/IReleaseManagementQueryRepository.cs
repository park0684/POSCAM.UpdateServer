using System.Data;
using POSCAM.UpdateServer.Api.Models.Entities;
using POSCAM.UpdateServer.Api.Models.Queries;

namespace POSCAM.UpdateServer.Api.Repositories;

/// <summary>
/// 관리자 릴리스 목록·건수·잠금 조회를 담당한다.
/// 공개 Update Check Repository와 쓰기 Repository의 책임을 분리한다.
/// </summary>
public interface IReleaseManagementQueryRepository
{
    Task<IReadOnlyList<UpdateRelease>> GetPagedAsync(
        ReleaseSearchCriteria criteria,
        IDbTransaction? transaction = null,
        CancellationToken cancellationToken = default);

    Task<long> CountAsync(
        ReleaseSearchCriteria criteria,
        IDbTransaction? transaction = null,
        CancellationToken cancellationToken = default);

    Task<UpdateRelease?> GetByCodeForUpdateAsync(
        long releaseCode,
        IDbTransaction transaction,
        CancellationToken cancellationToken = default);
}
