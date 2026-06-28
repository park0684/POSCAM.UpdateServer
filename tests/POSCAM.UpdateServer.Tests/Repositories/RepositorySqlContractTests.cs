using POSCAM.UpdateServer.Api.Repositories;

namespace POSCAM.UpdateServer.Tests.Repositories;

public class RepositorySqlContractTests
{
    [Fact]
    public void 모든_SQL은_AuthServer_DB를_참조하지_않는다()
    {
        foreach (var sql in GetAllSql())
        {
            Assert.DoesNotContain("poscam_auth", sql, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public void 쓰기_SQL은_UTC_TIMESTAMP를_사용한다()
    {
        var writeSql = new[]
        {
            UpdateReleaseRepository.CreateDraftSql,
            UpdateReleaseRepository.UpdateDraftSql,
            UpdateReleaseRepository.PublishSql,
            UpdateReleaseRepository.DisableSql,
            UpdateArtifactRepository.CreateSql,
            UpdateArtifactRepository.ReplaceSql,
            UpdateArtifactRepository.SetStatusSql,
            UpdateAuditLogRepository.CreateSql
        };

        foreach (var sql in writeSql)
        {
            Assert.Contains("UTC_TIMESTAMP()", sql, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public void 최신릴리스_조회는_숫자버전과_아키텍처우선순위를_사용한다()
    {
        var sql = Normalize(UpdateReleaseRepository.FindLatestCompatibleSql);

        Assert.Contains("r.rel_version_major DESC", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("r.rel_version_minor DESC", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("r.rel_version_build DESC", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("r.rel_version_revision DESC", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("a.art_architecture = @RequestedArchitecture THEN 2", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("a.art_architecture = @AnyArchitecture THEN 1", sql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("ORDER BY r.rel_version DESC", sql, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void 최신릴리스_조회는_제품_릴리스_Artifact_활성상태를_모두_검사한다()
    {
        var sql = Normalize(UpdateReleaseRepository.FindLatestCompatibleSql);

        Assert.Contains("p.prd_status = @ActiveProductStatus", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("r.rel_status = @PublishedStatus", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("a.art_status = @ActiveArtifactStatus", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("a.art_os = @OperatingSystem", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("a.art_package_type = @PackageType", sql, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Published_존재조회는_제품과_채널과_Published상태를_검사한다()
    {
        var sql = Normalize(UpdateReleaseRepository.HasPublishedReleaseSql);

        Assert.Contains("rel_product_code = @ProductCode", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("rel_channel = @Channel", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("rel_status = @PublishedStatus", sql, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void 조회_SQL은_Entity속성에_명시적_별칭을_사용한다()
    {
        Assert.Contains("prd_code AS ProductCode", UpdateProductRepository.GetByCodeSql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("rel_version_build AS VersionPatch", UpdateReleaseRepository.GetByCodeSql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("art_os AS OperatingSystem", UpdateArtifactRepository.GetByCodeSql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("ual_code AS AuditLogCode", UpdateAuditLogRepository.GetByTargetSql, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void 주요_조건값은_파라미터로_바인딩한다()
    {
        Assert.Contains("@ProductCode", UpdateReleaseRepository.FindLatestCompatibleSql);
        Assert.Contains("@Channel", UpdateReleaseRepository.FindLatestCompatibleSql);
        Assert.Contains("@OperatingSystem", UpdateReleaseRepository.FindLatestCompatibleSql);
        Assert.Contains("@RequestedArchitecture", UpdateReleaseRepository.FindLatestCompatibleSql);
        Assert.Contains("@PackageType", UpdateReleaseRepository.FindLatestCompatibleSql);
        Assert.Contains("@ReleaseCode", UpdateArtifactRepository.GetByTargetSql);
        Assert.Contains("@TargetType", UpdateAuditLogRepository.GetByTargetSql);
        Assert.Contains("@TargetCode", UpdateAuditLogRepository.GetByTargetSql);
    }

    private static IEnumerable<string> GetAllSql()
    {
        yield return UpdateProductRepository.GetByCodeSql;
        yield return UpdateProductRepository.GetActiveSql;
        yield return UpdateReleaseRepository.GetByCodeSql;
        yield return UpdateReleaseRepository.ExistsVersionSql;
        yield return UpdateReleaseRepository.HasPublishedReleaseSql;
        yield return UpdateReleaseRepository.CreateDraftSql;
        yield return UpdateReleaseRepository.UpdateDraftSql;
        yield return UpdateReleaseRepository.DeleteDraftSql;
        yield return UpdateReleaseRepository.PublishSql;
        yield return UpdateReleaseRepository.DisableSql;
        yield return UpdateReleaseRepository.FindLatestCompatibleSql;
        yield return UpdateArtifactRepository.GetByCodeSql;
        yield return UpdateArtifactRepository.GetActiveByReleaseSql;
        yield return UpdateArtifactRepository.GetByTargetSql;
        yield return UpdateArtifactRepository.CreateSql;
        yield return UpdateArtifactRepository.ReplaceSql;
        yield return UpdateArtifactRepository.SetStatusSql;
        yield return UpdateAuditLogRepository.CreateSql;
        yield return UpdateAuditLogRepository.GetByTargetSql;
        yield return UpdateAuditLogRepository.CountByTargetSql;
    }

    private static string Normalize(string sql)
    {
        return string.Join(
            ' ',
            sql.Split(
                new[] { ' ', '\r', '\n', '\t' },
                StringSplitOptions.RemoveEmptyEntries));
    }
}
