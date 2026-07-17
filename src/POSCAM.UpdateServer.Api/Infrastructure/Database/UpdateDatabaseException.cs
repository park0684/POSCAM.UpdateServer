namespace POSCAM.UpdateServer.Api.Infrastructure.Database;

/// <summary>
/// 외부에 Connection String, SQL, DB 서버 상세를 노출하지 않고
/// 서비스 계층이 DB 오류 종류만 판단할 수 있도록 감싼 예외.
/// </summary>
public sealed class UpdateDatabaseException : Exception
{
    public DatabaseFailureType FailureType { get; }

    public int ProviderErrorNumber { get; }

    public UpdateDatabaseException(
        DatabaseFailureType failureType,
        int providerErrorNumber,
        string message,
        Exception innerException)
        : base(message, innerException)
    {
        FailureType = failureType;
        ProviderErrorNumber = providerErrorNumber;
    }
}
