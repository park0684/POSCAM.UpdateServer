using POSCAM.UpdateServer.Api.Infrastructure.Database;

namespace POSCAM.UpdateServer.Tests.Infrastructure.Database;

public class DatabaseExceptionTranslatorTests
{
    [Theory]
    [InlineData(1062, DatabaseFailureType.Duplicate)]
    [InlineData(1451, DatabaseFailureType.ForeignKeyViolation)]
    [InlineData(1452, DatabaseFailureType.ForeignKeyViolation)]
    [InlineData(0, DatabaseFailureType.ConnectionFailed)]
    [InlineData(1042, DatabaseFailureType.ConnectionFailed)]
    [InlineData(1045, DatabaseFailureType.ConnectionFailed)]
    [InlineData(1049, DatabaseFailureType.ConnectionFailed)]
    [InlineData(2002, DatabaseFailureType.ConnectionFailed)]
    [InlineData(2003, DatabaseFailureType.ConnectionFailed)]
    [InlineData(2005, DatabaseFailureType.ConnectionFailed)]
    [InlineData(1213, DatabaseFailureType.Unknown)]
    public void Classify_공급자오류를_정해진_DB오류로_분류한다(
        int providerErrorNumber,
        DatabaseFailureType expected)
    {
        var actual = DatabaseExceptionTranslator.Classify(providerErrorNumber);

        Assert.Equal(expected, actual);
    }
}
