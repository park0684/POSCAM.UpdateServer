using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using POSCAM.UpdateServer.Api.Controllers;
using POSCAM.UpdateServer.Api.Models.Common;
using POSCAM.UpdateServer.Api.Models.Dtos.Updates;
using POSCAM.UpdateServer.Api.Models.Enums;
using POSCAM.UpdateServer.Api.Services;
using POSCAM.UpdateServer.Tests.TestDoubles;

namespace POSCAM.UpdateServer.Tests.Controllers;

public class UpdatesControllerTests
{
    [Fact]
    public async Task CheckAsync_성공응답은_200과_no_store를_반환한다()
    {
        var responseData = new UpdateCheckResponse
        {
            UpdateAvailable = false,
            Mandatory = false,
            ReasonCode = "ALREADY_LATEST",
            ProductCode = "PCCAM",
            CurrentVersion = "2.0.0",
            LatestVersion = "2.0.0",
            Channel = "stable",
            Os = "windows",
            Architecture = "x86"
        };

        var fakeService = new FakeUpdateCheckService
        {
            Result = UpdateCheckServiceResult.Ok(responseData)
        };

        var controller = CreateController(fakeService);
        var request = CreateRequest();

        var actionResult = await controller.CheckAsync(
            request,
            CancellationToken.None);

        var okResult = Assert.IsType<OkObjectResult>(actionResult.Result);
        var response = Assert.IsType<ApiResponse<UpdateCheckResponse>>(okResult.Value);

        Assert.True(response.Success);
        Assert.Equal((int)UpdateErrorCode.None, response.ErrorCode);
        Assert.Same(responseData, response.Data);
        Assert.Same(request, fakeService.LastRequest);
        Assert.Equal("no-store", controller.Response.Headers["Cache-Control"].ToString());
        Assert.Equal("no-cache", controller.Response.Headers["Pragma"].ToString());
        Assert.Equal("0", controller.Response.Headers["Expires"].ToString());
    }

    [Fact]
    public async Task CheckAsync_입력오류는_공통응답과_400을_반환한다()
    {
        var fakeService = new FakeUpdateCheckService
        {
            Result = UpdateCheckServiceResult.Fail(
                UpdateErrorCode.InvalidVersion,
                "현재 버전 형식이 올바르지 않습니다.")
        };

        var controller = CreateController(fakeService);

        var actionResult = await controller.CheckAsync(
            CreateRequest(),
            CancellationToken.None);

        var objectResult = Assert.IsType<ObjectResult>(actionResult.Result);
        var response = Assert.IsType<ApiResponse<UpdateCheckResponse>>(objectResult.Value);

        Assert.Equal(StatusCodes.Status400BadRequest, objectResult.StatusCode);
        Assert.False(response.Success);
        Assert.Equal((int)UpdateErrorCode.InvalidVersion, response.ErrorCode);
        Assert.Null(response.Data);
    }

    [Fact]
    public async Task CheckAsync_DB데이터오류는_500을_반환한다()
    {
        var fakeService = new FakeUpdateCheckService
        {
            Result = UpdateCheckServiceResult.Fail(
                UpdateErrorCode.DatabaseError,
                "업데이트 릴리스 데이터가 올바르지 않습니다.")
        };

        var controller = CreateController(fakeService);

        var actionResult = await controller.CheckAsync(
            CreateRequest(),
            CancellationToken.None);

        var objectResult = Assert.IsType<ObjectResult>(actionResult.Result);
        var response = Assert.IsType<ApiResponse<UpdateCheckResponse>>(objectResult.Value);

        Assert.Equal(StatusCodes.Status500InternalServerError, objectResult.StatusCode);
        Assert.False(response.Success);
        Assert.Equal((int)UpdateErrorCode.DatabaseError, response.ErrorCode);
    }

    [Fact]
    public void CheckAsync_익명호출을_명시적으로_허용한다()
    {
        var method = typeof(UpdatesController).GetMethod(
            nameof(UpdatesController.CheckAsync));

        Assert.NotNull(method);
        Assert.NotEmpty(
            method.GetCustomAttributes(
                typeof(AllowAnonymousAttribute),
                inherit: false));
        Assert.Empty(
            method.GetCustomAttributes(
                typeof(AuthorizeAttribute),
                inherit: false));
    }

    private static UpdatesController CreateController(
        FakeUpdateCheckService fakeService)
    {
        return new UpdatesController(fakeService)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext()
            }
        };
    }

    private static UpdateCheckRequest CreateRequest()
    {
        return new UpdateCheckRequest
        {
            ProductCode = "PCCAM",
            CurrentVersion = "2.0.0",
            Os = "windows",
            Architecture = "x86",
            Channel = "stable"
        };
    }
}
