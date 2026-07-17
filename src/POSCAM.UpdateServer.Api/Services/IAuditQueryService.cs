using POSCAM.UpdateServer.Api.Models.Common;
using POSCAM.UpdateServer.Api.Models.Dtos.Admin.Audits;

namespace POSCAM.UpdateServer.Api.Services;

public interface IAuditQueryService
{
    Task<AdminServiceResult<PagedResponse<AuditLogResponse>>> GetAuditLogsAsync(
        AuditListRequest? request,
        CancellationToken cancellationToken = default);

    Task<AdminServiceResult<PagedResponse<AuditLogResponse>>> GetReleaseHistoryAsync(
        long releaseCode,
        ReleaseAuditListRequest? request,
        CancellationToken cancellationToken = default);
}
