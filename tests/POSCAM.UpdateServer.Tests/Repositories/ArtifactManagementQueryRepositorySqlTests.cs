using POSCAM.UpdateServer.Api.Repositories;

namespace POSCAM.UpdateServer.Tests.Repositories;

public class ArtifactManagementQueryRepositorySqlTests
{
    [Fact]
    public void GetByTargetForUpdateSql_대상키와_FOR_UPDATE를_사용한다()
    {
        var sql = Normalize(ArtifactManagementQueryRepository.GetByTargetForUpdateSql);

        Assert.Contains("art_release_code = @ReleaseCode", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("art_os = @OperatingSystem", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("art_architecture = @Architecture", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("art_package_type = @PackageType", sql, StringComparison.OrdinalIgnoreCase);
        Assert.EndsWith("FOR UPDATE;", sql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("poscam_auth", sql, StringComparison.OrdinalIgnoreCase);
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
