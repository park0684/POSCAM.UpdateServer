using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using POSCAM.UpdateServer.Api.Controllers;

namespace POSCAM.UpdateServer.Tests.Controllers;

public class AdminReleaseRouteTests
{
    [Theory]
    [InlineData(typeof(AdminProductsController), "api/v1/admin/products")]
    [InlineData(typeof(AdminReleasesController), "api/v1/admin/releases")]
    [InlineData(typeof(AdminArtifactsController), "api/v1/admin/releases/{releaseCode:long}/artifacts")]
    [InlineData(typeof(AdminReleaseLifecycleController), "api/v1/admin/releases")]
    [InlineData(typeof(AdminArtifactLifecycleController), "api/v1/admin/artifacts")]
    [InlineData(typeof(AdminAuditsController), "api/v1/admin")]
    public void 관리자_Controller는_공통보호경로_아래에_있다(
        Type controllerType,
        string expectedRoute)
    {
        var route = controllerType
            .GetCustomAttributes(typeof(RouteAttribute), inherit: false)
            .Cast<RouteAttribute>()
            .Single();

        Assert.Equal(expectedRoute, route.Template);
        Assert.StartsWith("api/v1/admin", route.Template, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(typeof(AdminProductsController))]
    [InlineData(typeof(AdminReleasesController))]
    [InlineData(typeof(AdminArtifactsController))]
    [InlineData(typeof(AdminReleaseLifecycleController))]
    [InlineData(typeof(AdminArtifactLifecycleController))]
    [InlineData(typeof(AdminAuditsController))]
    public void 관리자_Controller에는_AllowAnonymous를_사용하지_않는다(
        Type controllerType)
    {
        Assert.Empty(
            controllerType.GetCustomAttributes(
                typeof(AllowAnonymousAttribute),
                inherit: true));

        foreach (var method in controllerType.GetMethods())
        {
            Assert.Empty(
                method.GetCustomAttributes(
                    typeof(AllowAnonymousAttribute),
                    inherit: true));
        }
    }
}
