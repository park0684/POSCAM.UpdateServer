using POSCAM.UpdateServer.Api.Repositories;

namespace POSCAM.UpdateServer.Tests.Repositories;

public class ReleaseManagementQueryRepositorySqlTests
{
    [Fact]
    public void GetPagedSql_필터와_페이징을_파라미터로_처리한다()
    {
        var sql = ReleaseManagementQueryRepository.GetPagedSql;

        Assert.Contains("@ProductCode", sql);
        Assert.Contains("@Channel", sql);
        Assert.Contains("@ReleaseStatus", sql);
        Assert.Contains("@Keyword", sql);
        Assert.Contains("LIMIT @Offset, @PageSize", sql, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void GetPagedSql_숫자버전순으로_정렬한다()
    {
        var sql = Normalize(ReleaseManagementQueryRepository.GetPagedSql);

        Assert.Contains("rel_version_major DESC", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("rel_version_minor DESC", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("rel_version_build DESC", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("rel_version_revision DESC", sql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("ORDER BY rel_version DESC", sql, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void GetByCodeForUpdateSql_상태경쟁을_막기위해_FOR_UPDATE를_사용한다()
    {
        var sql = Normalize(ReleaseManagementQueryRepository.GetByCodeForUpdateSql);

        Assert.Contains("WHERE rel_code = @ReleaseCode", sql, StringComparison.OrdinalIgnoreCase);
        Assert.EndsWith("FOR UPDATE;", sql, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void 관리조회_SQL은_AuthServer_DB를_참조하지_않는다()
    {
        Assert.DoesNotContain("poscam_auth", ReleaseManagementQueryRepository.GetPagedSql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("poscam_auth", ReleaseManagementQueryRepository.CountSql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("poscam_auth", ReleaseManagementQueryRepository.GetByCodeForUpdateSql, StringComparison.OrdinalIgnoreCase);
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
