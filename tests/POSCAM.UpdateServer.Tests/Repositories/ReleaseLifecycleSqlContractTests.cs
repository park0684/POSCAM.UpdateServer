using POSCAM.UpdateServer.Api.Repositories;

namespace POSCAM.UpdateServer.Tests.Repositories;

public class ReleaseLifecycleSqlContractTests
{
    [Fact]
    public void PublishSql_Draft에서만_Published와_UTC게시시각을_설정한다()
    {
        var sql = Normalize(UpdateReleaseRepository.PublishSql);

        Assert.Contains("rel_status = @PublishedStatus", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("rel_published_at = UTC_TIMESTAMP()", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("rel_udate = UTC_TIMESTAMP()", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("rel_status = @DraftStatus", sql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("update_artifacts", sql, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void DisableSql_Published에서만_Disabled로_전환하고_파일이나Artifact를_삭제하지_않는다()
    {
        var sql = Normalize(UpdateReleaseRepository.DisableSql);

        Assert.Contains("rel_status = @DisabledStatus", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("rel_status = @PublishedStatus", sql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("DELETE", sql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("update_artifacts", sql, StringComparison.OrdinalIgnoreCase);
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
