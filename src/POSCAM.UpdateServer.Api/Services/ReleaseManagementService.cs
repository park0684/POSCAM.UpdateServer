using POSCAM.UpdateServer.Api.Authorization;
using POSCAM.UpdateServer.Api.Infrastructure.Database;
using POSCAM.UpdateServer.Api.Models.Common;
using POSCAM.UpdateServer.Api.Models.Dtos.Admin.Releases;
using POSCAM.UpdateServer.Api.Repositories;

namespace POSCAM.UpdateServer.Api.Services;

/// <summary>
/// 관리자용 Active Product 조회와 Draft Release CRUD를 담당한다.
/// Published·Disabled Release는 조회만 허용하고 수정·삭제하지 않는다.
/// </summary>
public sealed partial class ReleaseManagementService : IReleaseManagementService
{
    private const int MaxPageSize = 100;
    private const int MaxKeywordLength = 100;

    private readonly IDbContext _dbContext;
    private readonly IUpdateProductRepository _productRepository;
    private readonly IUpdateReleaseRepository _releaseRepository;
    private readonly IReleaseManagementQueryRepository _managementQueryRepository;
    private readonly IUpdateArtifactRepository _artifactRepository;
    private readonly IUpdateAuditLogRepository _auditLogRepository;
    private readonly IUpdateManagementActorAccessor _actorAccessor;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public ReleaseManagementService(
        IDbContext dbContext,
        IUpdateProductRepository productRepository,
        IUpdateReleaseRepository releaseRepository,
        IReleaseManagementQueryRepository managementQueryRepository,
        IUpdateArtifactRepository artifactRepository,
        IUpdateAuditLogRepository auditLogRepository,
        IUpdateManagementActorAccessor actorAccessor,
        IHttpContextAccessor httpContextAccessor)
    {
        _dbContext = dbContext;
        _productRepository = productRepository;
        _releaseRepository = releaseRepository;
        _managementQueryRepository = managementQueryRepository;
        _artifactRepository = artifactRepository;
        _auditLogRepository = auditLogRepository;
        _actorAccessor = actorAccessor;
        _httpContextAccessor = httpContextAccessor;
    }

    public async Task<AdminServiceResult<IReadOnlyList<ActiveProductResponse>>> GetActiveProductsAsync(
        CancellationToken cancellationToken = default)
    {
        var products = await _productRepository.GetActiveAsync(
            cancellationToken: cancellationToken);

        IReadOnlyList<ActiveProductResponse> response = products
            .Select(product => new ActiveProductResponse
            {
                ProductCode = product.ProductCode,
                ProductName = product.ProductName,
                ProductDescription = product.ProductDescription
            })
            .ToArray();

        return AdminServiceResult<IReadOnlyList<ActiveProductResponse>>.Ok(
            response,
            "활성 제품 목록을 조회했습니다.");
    }

    public async Task<AdminServiceResult<PagedResponse<ReleaseListItemResponse>>> GetReleasesAsync(
        ReleaseListRequest? request,
        CancellationToken cancellationToken = default)
    {
        var validation = ValidateListRequest(request, out var criteria);
        if (validation is not null)
        {
            return validation;
        }

        var releases = await _managementQueryRepository.GetPagedAsync(
            criteria!,
            cancellationToken: cancellationToken);

        var totalCount = await _managementQueryRepository.CountAsync(
            criteria!,
            cancellationToken: cancellationToken);

        var page = request!.Page;
        var pageSize = request.PageSize;
        var totalPages = totalCount == 0
            ? 0
            : (int)Math.Ceiling(totalCount / (double)pageSize);

        var response = new PagedResponse<ReleaseListItemResponse>
        {
            Items = releases.Select(MapListItem).ToArray(),
            Page = page,
            PageSize = pageSize,
            TotalCount = totalCount,
            TotalPages = totalPages
        };

        return AdminServiceResult<PagedResponse<ReleaseListItemResponse>>.Ok(
            response,
            "릴리스 목록을 조회했습니다.");
    }

    public async Task<AdminServiceResult<ReleaseDetailResponse>> GetReleaseAsync(
        long releaseCode,
        CancellationToken cancellationToken = default)
    {
        if (releaseCode <= 0)
        {
            return ReleaseNotFound<ReleaseDetailResponse>();
        }

        var release = await _releaseRepository.GetByCodeAsync(
            releaseCode,
            cancellationToken: cancellationToken);

        if (release is null)
        {
            return ReleaseNotFound<ReleaseDetailResponse>();
        }

        var artifacts = await _artifactRepository.GetActiveByReleaseAsync(
            releaseCode,
            cancellationToken: cancellationToken);

        return AdminServiceResult<ReleaseDetailResponse>.Ok(
            MapDetail(release, artifacts),
            "릴리스 상세 정보를 조회했습니다.");
    }
}
