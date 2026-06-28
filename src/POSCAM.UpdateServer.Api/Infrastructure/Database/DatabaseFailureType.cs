namespace POSCAM.UpdateServer.Api.Infrastructure.Database;

/// <summary>
/// MariaDB 오류를 UpdateServer에서 처리 가능한 범주로 분류한다.
/// </summary>
public enum DatabaseFailureType
{
    Unknown = 0,
    Duplicate = 1,
    ForeignKeyViolation = 2,
    ConnectionFailed = 3
}
