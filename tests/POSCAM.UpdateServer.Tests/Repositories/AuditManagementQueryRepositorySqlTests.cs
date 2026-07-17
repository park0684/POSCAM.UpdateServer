using POSCAM.UpdateServer.Api.Repositories;

namespace POSCAM.UpdateServer.Tests.Repositories;

public class AuditManagementQueryRepositorySqlTests
{
    [Fact]
    public void GetPagedSql_모든필터와_페이징을_파라미터로_사용한다()
    {
        var sql = AuditManagementQueryRepository.GetPagedSql;

        Assert.Contains("@Action", sql);
        Assert.Contains("@TargetType", sql);
        Assert.Contains("@TargetCode", sql);
        Assert.Contains("@ActorUserCode", sql);
        Assert.Contains("@RequestId", sql);
        Assert.Contains("@FromUtc", sql);
        Assert.Contains("@ToUtc", sql);
        Assert.Contains("LIMIT @Offset, @PageSize", sql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("poscam_auth", sql, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void GetReleaseHistorySql_Release와_연결Artifact이력을_포함한다()
    {
        var sql = Normalize(AuditManagementQueryRepository.GetReleaseHistorySql);

        Assert.Contains("ual_target_type = 'RELEASE'", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("ual_target_type = 'ARTIFACT'", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("a.art_release_code = @ReleaseCode", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("l.ual_target_code = CAST(a.art_code AS CHAR)", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("ORDER BY l.ual_idate DESC, l.ual_code DESC", sql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("UPDATE ", sql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("DELETE ", sql, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void 감사조회SQL은_읽기전용이고_AuthDB를_참조하지_않는다()
    {
        var sqlStatements = new[]
        {
            AuditManagementQueryRepository.GetPagedSql,
            AuditManagementQueryRepository.CountSql,
            AuditManagementQueryRepository.GetReleaseHistorySql,
            AuditManagementQueryRepository.CountReleaseHistorySql
        };

        foreach (var sql in sqlStatements)
        {
            Assert.DoesNotContain("poscam_auth", sql, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("INSERT ", sql, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("UPDATE ", sql, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("DELETE ", sql, StringComparison.OrdinalIgnoreCase);
        }
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
