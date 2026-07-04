using POSCAM.UpdateServer.Api.Repositories;

namespace POSCAM.UpdateServer.Tests.Repositories;

public class UpdateArtifactFileRepositorySqlTests
{
    [Fact]
    public void GetActiveByArtifactSql_ArtifactCode와_ActiveStatus를_사용한다()
    {
        var sql = Normalize(UpdateArtifactFileRepository.GetActiveByArtifactSql);

        Assert.Contains("FROM update_artifact_files", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("artifact_code = @ArtifactCode", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("file_status = @ActiveStatus", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("ORDER BY file_path ASC", sql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("poscam_auth", sql, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ExistsByArtifactSql_ArtifactCode로_존재여부를_확인한다()
    {
        var sql = Normalize(UpdateArtifactFileRepository.ExistsByArtifactSql);

        Assert.Contains("FROM update_artifact_files", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("artifact_code = @ArtifactCode", sql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("poscam_auth", sql, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void CreateSql_파일별_Manifest_필드를_저장한다()
    {
        var sql = Normalize(UpdateArtifactFileRepository.CreateSql);

        Assert.Contains("INSERT INTO update_artifact_files", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("file_public_id", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("file_path", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("file_sha256", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("file_storage_key", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("file_download_path", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("UTC_TIMESTAMP()", sql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("poscam_auth", sql, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void DeleteByArtifactSql_ArtifactCode_기준으로_삭제한다()
    {
        var sql = Normalize(UpdateArtifactFileRepository.DeleteByArtifactSql);

        Assert.Contains("DELETE FROM update_artifact_files", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("artifact_code = @ArtifactCode", sql, StringComparison.OrdinalIgnoreCase);
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
