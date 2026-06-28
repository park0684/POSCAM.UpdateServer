using POSCAM.UpdateServer.Api.Models.Dtos.Updates;
using POSCAM.UpdateServer.Api.Models.Enums;

namespace POSCAM.UpdateServer.Api.Services;

/// <summary>
/// Update Check 업무 처리 결과.
/// Controller는 성공 여부와 오류 코드만 HTTP 응답으로 변환한다.
/// </summary>
public sealed class UpdateCheckServiceResult
{
    public bool Success { get; init; }

    public string Message { get; init; } = string.Empty;

    public UpdateErrorCode ErrorCode { get; init; }

    public UpdateCheckResponse? Data { get; init; }

    public static UpdateCheckServiceResult Ok(
        UpdateCheckResponse data,
        string message = "업데이트 확인이 완료되었습니다.")
    {
        return new UpdateCheckServiceResult
        {
            Success = true,
            Message = message,
            ErrorCode = UpdateErrorCode.None,
            Data = data
        };
    }

    public static UpdateCheckServiceResult Fail(
        UpdateErrorCode errorCode,
        string message)
    {
        return new UpdateCheckServiceResult
        {
            Success = false,
            Message = message,
            ErrorCode = errorCode,
            Data = null
        };
    }
}
