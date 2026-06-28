using Microsoft.Extensions.Configuration;
using POSCAM.UpdateServer.Api.Infrastructure.Database;

namespace POSCAM.UpdateServer.Tests.Infrastructure.Database;

public class DapperContextTests
{
    [Fact]
    public void Constructor_DefaultConnection이_없으면_명확히_실패한다()
    {
        var configuration = new ConfigurationManager();

        var exception = Assert.Throws<InvalidOperationException>(
            () => new DapperContext(configuration));

        Assert.Contains("DefaultConnection", exception.Message);
    }

    [Fact]
    public void Constructor_AuthServer_DB연결을_거부한다()
    {
        var configuration = CreateConfiguration(
            "Server=localhost;Port=3306;Database=poscam_auth;Uid=test;Pwd=test;");

        var exception = Assert.Throws<InvalidOperationException>(
            () => new DapperContext(configuration));

        Assert.Contains("poscam_auth", exception.Message);
    }

    [Fact]
    public void Constructor_UpdateServer_DB연결문자열은_허용한다()
    {
        var configuration = CreateConfiguration(
            "Server=localhost;Port=3306;Database=poscam_update;Uid=test;Pwd=test;");

        var context = new DapperContext(configuration);

        Assert.NotNull(context);
    }

    private static IConfiguration CreateConfiguration(string connectionString)
    {
        var configuration = new ConfigurationManager();
        configuration["ConnectionStrings:DefaultConnection"] = connectionString;
        return configuration;
    }
}
