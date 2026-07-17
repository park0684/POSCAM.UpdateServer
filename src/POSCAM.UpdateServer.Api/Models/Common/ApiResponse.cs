using POSCAM.UpdateServer.Api.Models.Enums;

namespace POSCAM.UpdateServer.Api.Models.Common;

/// <summary>
/// UpdateServer의 모든 JSON API가 사용하는 공통 응답 형식.
/// </summary>
public sealed class ApiResponse<T>
{
    public bool Success { get; init; }

    public string Message { get; init; } = string.Empty;

    public int ErrorCode { get; init; }

    public T? Data { get; init; }

    /// <summary>
    /// 정상 응답을 생성한다.
    /// </summary>
    public static ApiResponse<T> Ok(
        T? data,
        string message = "요청이 정상적으로 처리되었습니다.")
    {
        return new ApiResponse<T>
        {
            Success = true,
            Message = message,
            ErrorCode = (int)UpdateErrorCode.None,
            Data = data
        };
    }

    /// <summary>
    /// 업무 또는 공통 오류 응답을 생성한다.
    /// </summary>
    public static ApiResponse<T> Fail(
        UpdateErrorCode errorCode,
        string message)
    {
        return new ApiResponse<T>
        {
            Success = false,
            Message = message,
            ErrorCode = (int)errorCode,
            Data = default
        };
    }
}
