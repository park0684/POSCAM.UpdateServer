using POSCAM.UpdateServer.Api.Models.Domain;
using POSCAM.UpdateServer.Api.Models.Enums;

namespace POSCAM.UpdateServer.Tests.Models.Domain;

public class UpdateVersionTests
{
    [Theory]
    [InlineData("1.2.3", 1, 2, 3, 0, "1.2.3")]
    [InlineData("1.2.3.4", 1, 2, 3, 4, "1.2.3.4")]
    [InlineData("0.0.0", 0, 0, 0, 0, "0.0.0")]
    [InlineData("65535.65535.65535.65535", 65535, 65535, 65535, 65535, "65535.65535.65535.65535")]
    public void TryParse_유효한_버전을_숫자구성요소로_변환한다(
        string input,
        int major,
        int minor,
        int patch,
        int revision,
        string normalized)
    {
        var success = UpdateVersion.TryParse(
            input,
            out var version,
            out var error);

        Assert.True(success);
        Assert.Equal(UpdateVersionParseError.None, error);
        Assert.Equal(major, version.Major);
        Assert.Equal(minor, version.Minor);
        Assert.Equal(patch, version.Patch);
        Assert.Equal(revision, version.Revision);
        Assert.Equal(normalized, version.ToString());
    }

    [Fact]
    public void TryParse_Revision이_0이면_세자리로_정규화한다()
    {
        Assert.True(UpdateVersion.TryParse("1.2.0.0", out var version, out _));

        Assert.Equal("1.2.0", version.ToString());
    }

    [Fact]
    public void CompareTo_문자열이_아닌_숫자기준으로_비교한다()
    {
        var version110 = Parse("1.10.0");
        var version19 = Parse("1.9.0");

        Assert.True(version110 > version19);
        Assert.True(version19 < version110);
    }

    [Fact]
    public void Equality_세자리와_Revision0_네자리를_동일하게_본다()
    {
        var threePart = Parse("1.2.0");
        var fourPart = Parse("1.2.0.0");

        Assert.Equal(threePart, fourPart);
        Assert.True(threePart == fourPart);
    }

    [Theory]
    [InlineData(null, UpdateVersionParseError.Required)]
    [InlineData("", UpdateVersionParseError.Required)]
    [InlineData("   ", UpdateVersionParseError.Required)]
    [InlineData("1.2", UpdateVersionParseError.InvalidFormat)]
    [InlineData("1.2.3.4.5", UpdateVersionParseError.InvalidFormat)]
    [InlineData("v1.2.3", UpdateVersionParseError.InvalidFormat)]
    [InlineData("1.2.3-beta", UpdateVersionParseError.InvalidFormat)]
    [InlineData("1.2.a", UpdateVersionParseError.InvalidFormat)]
    [InlineData("1..3", UpdateVersionParseError.InvalidFormat)]
    [InlineData(" 1.2.3", UpdateVersionParseError.InvalidFormat)]
    [InlineData("1.2.3 ", UpdateVersionParseError.InvalidFormat)]
    [InlineData("١.٢.٣", UpdateVersionParseError.InvalidFormat)]
    [InlineData("01.2.3", UpdateVersionParseError.LeadingZeroNotAllowed)]
    [InlineData("1.02.3", UpdateVersionParseError.LeadingZeroNotAllowed)]
    [InlineData("1.2.03", UpdateVersionParseError.LeadingZeroNotAllowed)]
    [InlineData("1.2.3.00", UpdateVersionParseError.LeadingZeroNotAllowed)]
    [InlineData("65536.0.0", UpdateVersionParseError.ComponentOutOfRange)]
    [InlineData("1.65536.0", UpdateVersionParseError.ComponentOutOfRange)]
    [InlineData("1.2.65536", UpdateVersionParseError.ComponentOutOfRange)]
    [InlineData("1.2.3.65536", UpdateVersionParseError.ComponentOutOfRange)]
    public void TryParse_잘못된_버전을_명시적_오류로_거부한다(
        string? input,
        UpdateVersionParseError expectedError)
    {
        var success = UpdateVersion.TryParse(
            input,
            out _,
            out var error);

        Assert.False(success);
        Assert.Equal(expectedError, error);
    }

    [Fact]
    public void TryCreate_음수_구성요소를_거부한다()
    {
        var success = UpdateVersion.TryCreate(
            1,
            2,
            3,
            -1,
            out _,
            out var error);

        Assert.False(success);
        Assert.Equal(UpdateVersionParseError.ComponentOutOfRange, error);
    }

    private static UpdateVersion Parse(string value)
    {
        var success = UpdateVersion.TryParse(
            value,
            out var version,
            out var error);

        Assert.True(success, $"버전 파싱 실패: {value}, Error={error}");
        return version;
    }
}
