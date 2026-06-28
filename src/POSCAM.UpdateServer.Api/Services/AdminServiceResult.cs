using POSCAM.UpdateServer.Api.Models.Enums;

namespace POSCAM.UpdateServer.Api.Services;

public sealed class AdminServiceResult<T>
{
    public bool Success { get; init; }
    public int HttpStatusCode { get; init; }
    public UpdateErrorCode ErrorCode { get; init; }
    public string Message { get; init; } = string.Empty;
    public T? Data { get; init; }

    public static AdminServiceResult<T> Ok(
        T data,
        string message,
        int httpStatusCode = StatusCodes.Status200OK)
    {
        return new AdminServiceResult<T>
        {
            Success = true,
            HttpStatusCode = httpStatusCode,
            ErrorCode = UpdateErrorCode.None,
            Message = message,
            Data = data
        };
    }

    public static AdminServiceResult<T> Fail(
        int httpStatusCode,
        UpdateErrorCode errorCode,
        string message)
    {
        return new AdminServiceResult<T>
        {
            Success = false,
            HttpStatusCode = httpStatusCode,
            ErrorCode = errorCode,
            Message = message,
            Data = default
        };
    }
}
