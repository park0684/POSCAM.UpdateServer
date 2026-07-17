using System.Data;
using POSCAM.UpdateServer.Api.Infrastructure.Database;
using POSCAM.UpdateServer.Api.Models.Domain;
using POSCAM.UpdateServer.Api.Models.Dtos.Admin.Releases;
using POSCAM.UpdateServer.Api.Models.Entities;
using POSCAM.UpdateServer.Api.Models.Enums;

namespace POSCAM.UpdateServer.Api.Services;

public sealed partial class ReleaseManagementService
{
    public async Task<AdminServiceResult<ReleaseDetailResponse>> CreateDraftAsync(
        CreateReleaseRequest? request,
        CancellationToken cancellationToken = default)
    {
        var actorFailure = RequireActor<ReleaseDetailResponse>(out var actor);
        if (actorFailure is not null)
        {
            return actorFailure;
        }

        var validation = ValidateMutation(
            request?.ProductCode,
            request?.Version,
            request?.Channel,
            request?.IsMandatory ?? false,
            request?.ForceUpdateBelowVersion,
            out var mutation);

        if (validation is not null)
        {
            return validation;
        }

        await using var connection = await _dbContext.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(
            IsolationLevel.ReadCommitted,
            cancellationToken);

        try
        {
            var productFailure = await ValidateActiveProductAsync<ReleaseDetailResponse>(
                mutation!.ProductCode,
                transaction,
                cancellationToken);

            if (productFailure is not null)
            {
                await transaction.RollbackAsync(cancellationToken);
                return productFailure;
            }

            var duplicate = await _releaseRepository.ExistsVersionAsync(
                mutation.ProductCode,
                mutation.Channel,
                mutation.Version,
                transaction: transaction,
                cancellationToken: cancellationToken);

            if (duplicate)
            {
                await transaction.RollbackAsync(cancellationToken);
                return DuplicateRelease<ReleaseDetailResponse>();
            }

            var release = CreateEntity(
                mutation,
                request!.ReleaseNotes,
                request.InternalMemo,
                actor!);

            release.ReleaseCode = await _releaseRepository.CreateDraftAsync(
                release,
                transaction,
                cancellationToken);

            var created = await _releaseRepository.GetByCodeAsync(
                release.ReleaseCode,
                transaction,
                cancellationToken);

            if (created is null)
            {
                throw new InvalidOperationException("생성된 릴리스를 다시 조회할 수 없습니다.");
            }

            await CreateAuditAsync(
                AuditActions.Create,
                created.ReleaseCode,
                before: null,
                after: created,
                actor!,
                transaction,
                cancellationToken);

            await transaction.CommitAsync(cancellationToken);

            return AdminServiceResult<ReleaseDetailResponse>.Ok(
                MapDetail(created, Array.Empty<UpdateArtifact>()),
                "Draft 릴리스를 생성했습니다.",
                StatusCodes.Status201Created);
        }
        catch (UpdateDatabaseException exception)
            when (exception.FailureType == DatabaseFailureType.Duplicate)
        {
            await SafeRollbackAsync(transaction);
            return DuplicateRelease<ReleaseDetailResponse>();
        }
        catch
        {
            await SafeRollbackAsync(transaction);
            throw;
        }
    }

    public async Task<AdminServiceResult<ReleaseDetailResponse>> UpdateDraftAsync(
        long releaseCode,
        UpdateReleaseRequest? request,
        CancellationToken cancellationToken = default)
    {
        if (releaseCode <= 0)
        {
            return ReleaseNotFound<ReleaseDetailResponse>();
        }

        var actorFailure = RequireActor<ReleaseDetailResponse>(out var actor);
        if (actorFailure is not null)
        {
            return actorFailure;
        }

        var validation = ValidateMutation(
            request?.ProductCode,
            request?.Version,
            request?.Channel,
            request?.IsMandatory ?? false,
            request?.ForceUpdateBelowVersion,
            out var mutation);

        if (validation is not null)
        {
            return validation;
        }

        await using var connection = await _dbContext.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(
            IsolationLevel.ReadCommitted,
            cancellationToken);

        try
        {
            var current = await _managementQueryRepository.GetByCodeForUpdateAsync(
                releaseCode,
                transaction,
                cancellationToken);

            if (current is null)
            {
                await transaction.RollbackAsync(cancellationToken);
                return ReleaseNotFound<ReleaseDetailResponse>();
            }

            if (current.ReleaseStatus != ReleaseStatus.Draft)
            {
                await transaction.RollbackAsync(cancellationToken);
                return InvalidReleaseState<ReleaseDetailResponse>(
                    "Draft 상태의 릴리스만 수정할 수 있습니다.");
            }

            var productFailure = await ValidateActiveProductAsync<ReleaseDetailResponse>(
                mutation!.ProductCode,
                transaction,
                cancellationToken);

            if (productFailure is not null)
            {
                await transaction.RollbackAsync(cancellationToken);
                return productFailure;
            }

            var duplicate = await _releaseRepository.ExistsVersionAsync(
                mutation.ProductCode,
                mutation.Channel,
                mutation.Version,
                excludeReleaseCode: releaseCode,
                transaction: transaction,
                cancellationToken: cancellationToken);

            if (duplicate)
            {
                await transaction.RollbackAsync(cancellationToken);
                return DuplicateRelease<ReleaseDetailResponse>();
            }

            var updated = ApplyMutation(
                current,
                mutation,
                request!.ReleaseNotes,
                request.InternalMemo);

            var changed = await _releaseRepository.UpdateDraftAsync(
                updated,
                transaction,
                cancellationToken);

            if (!changed)
            {
                await transaction.RollbackAsync(cancellationToken);
                return InvalidReleaseState<ReleaseDetailResponse>(
                    "릴리스 상태가 변경되어 수정할 수 없습니다.");
            }

            var saved = await _releaseRepository.GetByCodeAsync(
                releaseCode,
                transaction,
                cancellationToken);

            if (saved is null)
            {
                throw new InvalidOperationException("수정된 릴리스를 다시 조회할 수 없습니다.");
            }

            await CreateAuditAsync(
                AuditActions.Update,
                releaseCode,
                current,
                saved,
                actor!,
                transaction,
                cancellationToken);

            var artifacts = await _artifactRepository.GetActiveByReleaseAsync(
                releaseCode,
                transaction,
                cancellationToken);

            await transaction.CommitAsync(cancellationToken);

            return AdminServiceResult<ReleaseDetailResponse>.Ok(
                MapDetail(saved, artifacts),
                "Draft 릴리스를 수정했습니다.");
        }
        catch (UpdateDatabaseException exception)
            when (exception.FailureType == DatabaseFailureType.Duplicate)
        {
            await SafeRollbackAsync(transaction);
            return DuplicateRelease<ReleaseDetailResponse>();
        }
        catch
        {
            await SafeRollbackAsync(transaction);
            throw;
        }
    }

    public async Task<AdminServiceResult<DeleteReleaseResponse>> DeleteDraftAsync(
        long releaseCode,
        CancellationToken cancellationToken = default)
    {
        if (releaseCode <= 0)
        {
            return ReleaseNotFound<DeleteReleaseResponse>();
        }

        var actorFailure = RequireActor<DeleteReleaseResponse>(out var actor);
        if (actorFailure is not null)
        {
            return actorFailure;
        }

        await using var connection = await _dbContext.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(
            IsolationLevel.ReadCommitted,
            cancellationToken);

        try
        {
            var current = await _managementQueryRepository.GetByCodeForUpdateAsync(
                releaseCode,
                transaction,
                cancellationToken);

            if (current is null)
            {
                await transaction.RollbackAsync(cancellationToken);
                return ReleaseNotFound<DeleteReleaseResponse>();
            }

            if (current.ReleaseStatus != ReleaseStatus.Draft)
            {
                await transaction.RollbackAsync(cancellationToken);
                return InvalidReleaseState<DeleteReleaseResponse>(
                    "Draft 상태의 릴리스만 삭제할 수 있습니다.");
            }

            await CreateAuditAsync(
                AuditActions.DeleteDraft,
                releaseCode,
                current,
                after: null,
                actor!,
                transaction,
                cancellationToken);

            var deleted = await _releaseRepository.DeleteDraftAsync(
                releaseCode,
                transaction,
                cancellationToken);

            if (!deleted)
            {
                await transaction.RollbackAsync(cancellationToken);
                return InvalidReleaseState<DeleteReleaseResponse>(
                    "릴리스 상태가 변경되어 삭제할 수 없습니다.");
            }

            await transaction.CommitAsync(cancellationToken);

            return AdminServiceResult<DeleteReleaseResponse>.Ok(
                new DeleteReleaseResponse
                {
                    ReleaseCode = releaseCode
                },
                "Draft 릴리스를 삭제했습니다.");
        }
        catch (UpdateDatabaseException exception)
            when (exception.FailureType == DatabaseFailureType.ForeignKeyViolation)
        {
            await SafeRollbackAsync(transaction);
            return InvalidReleaseState<DeleteReleaseResponse>(
                "Artifact가 연결된 Draft 릴리스는 삭제할 수 없습니다.");
        }
        catch
        {
            await SafeRollbackAsync(transaction);
            throw;
        }
    }
}
