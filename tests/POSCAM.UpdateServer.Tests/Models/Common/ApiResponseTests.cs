using POSCAM.UpdateServer.Api.Models.Common;
using POSCAM.UpdateServer.Api.Models.Enums;

namespace POSCAM.UpdateServer.Tests.Models.Common;

public class ApiResponseTests
{
    [Fact]
    public void Ok_정상응답을_생성한다()
    {
        var response = ApiResponse<string>.Ok(
            "data",
            "정상");

        Assert.True(response.Success);
        Assert.Equal("정상", response.Message);
        Assert.Equal((int)UpdateErrorCode.None, response.ErrorCode);
        Assert.Equal("data", response.Data);
    }

    [Fact]
    public void Fail_오류코드와_메시지를_보존한다()
    {
        var response = ApiResponse<string>.Fail(
            UpdateErrorCode.InvalidVersion,
            "버전 형식이 올바르지 않습니다.");

        Assert.False(response.Success);
        Assert.Equal("버전 형식이 올바르지 않습니다.", response.Message);
        Assert.Equal((int)UpdateErrorCode.InvalidVersion, response.ErrorCode);
        Assert.Null(response.Data);
    }
}
