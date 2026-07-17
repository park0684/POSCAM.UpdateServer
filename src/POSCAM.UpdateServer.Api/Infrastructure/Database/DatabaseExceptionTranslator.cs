using MySqlConnector;

namespace POSCAM.UpdateServer.Api.Infrastructure.Database;

/// <summary>
/// MySQL/MariaDB 공급자 오류 번호를 UpdateServer 공통 DB 오류로 변환한다.
/// 원본 예외 메시지는 내부 예외로만 보존하고 외부 응답에는 노출하지 않는다.
/// </summary>
public static class DatabaseExceptionTranslator
{
    private const int DuplicateEntry = 1062;
    private const int CannotDeleteOrUpdateParentRow = 1451;
    private const int CannotAddOrUpdateChildRow = 1452;

    private static readonly HashSet<int> ConnectionErrorNumbers = new()
    {
        0,
        1042,
        1045,
        1049,
        2002,
        2003,
        2005
    };

    public static UpdateDatabaseException Translate(MySqlException exception)
    {
        ArgumentNullException.ThrowIfNull(exception);

        var failureType = Classify(exception.Number);

        var message = failureType switch
        {
            DatabaseFailureType.Duplicate => "중복된 데이터로 인해 DB 작업을 완료하지 못했습니다.",
            DatabaseFailureType.ForeignKeyViolation => "연결된 데이터 제약조건으로 인해 DB 작업을 완료하지 못했습니다.",
            DatabaseFailureType.ConnectionFailed => "업데이트 데이터베이스에 연결할 수 없습니다.",
            _ => "업데이트 데이터베이스 작업 중 오류가 발생했습니다."
        };

        return new UpdateDatabaseException(
            failureType,
            exception.Number,
            message,
            exception);
    }

    public static DatabaseFailureType Classify(int providerErrorNumber)
    {
        if (providerErrorNumber == DuplicateEntry)
        {
            return DatabaseFailureType.Duplicate;
        }

        if (providerErrorNumber is CannotDeleteOrUpdateParentRow or CannotAddOrUpdateChildRow)
        {
            return DatabaseFailureType.ForeignKeyViolation;
        }

        return ConnectionErrorNumbers.Contains(providerErrorNumber)
            ? DatabaseFailureType.ConnectionFailed
            : DatabaseFailureType.Unknown;
    }
}
