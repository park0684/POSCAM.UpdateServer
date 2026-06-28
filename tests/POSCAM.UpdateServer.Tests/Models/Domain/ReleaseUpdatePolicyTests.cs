using POSCAM.UpdateServer.Api.Models.Domain;
using POSCAM.UpdateServer.Api.Models.Enums;

namespace POSCAM.UpdateServer.Tests.Models.Domain;

public class ReleaseUpdatePolicyTests
{
    [Fact]
    public void TryCreate_일반_업데이트_정책을_허용한다()
    {
        var releaseVersion = Parse("2.0.0");

        var success = ReleaseUpdatePolicy.TryCreate(
            releaseVersion,
            isMandatory: false,
            forceUpdateBelowVersion: null,
            out var policy,
            out var error);

        Assert.True(success);
        Assert.NotNull(policy);
        Assert.Equal(ReleasePolicyValidationError.None, error);
        Assert.False(policy.IsMandatory);
        Assert.Null(policy.ForceUpdateBelowVersion);
    }

    [Fact]
    public void TryCreate_전체강제와_기준버전을_동시에_허용하지_않는다()
    {
        var success = ReleaseUpdatePolicy.TryCreate(
            Parse("2.0.0"),
            isMandatory: true,
            forceUpdateBelowVersion: Parse("1.5.0"),
            out var policy,
            out var error);

        Assert.False(success);
        Assert.Null(policy);
        Assert.Equal(
            ReleasePolicyValidationError.MandatoryAndThresholdCannotCoexist,
            error);
    }

    [Fact]
    public void TryCreate_강제기준이_릴리스버전보다_높으면_거부한다()
    {
        var success = ReleaseUpdatePolicy.TryCreate(
            Parse("2.0.0"),
            isMandatory: false,
            forceUpdateBelowVersion: Parse("2.0.1"),
            out var policy,
            out var error);

        Assert.False(success);
        Assert.Null(policy);
        Assert.Equal(
            ReleasePolicyValidationError.ForceThresholdExceedsReleaseVersion,
            error);
    }

    [Fact]
    public void TryCreate_강제기준과_릴리스버전이_같으면_허용한다()
    {
        var releaseVersion = Parse("2.0.0");

        var success = ReleaseUpdatePolicy.TryCreate(
            releaseVersion,
            isMandatory: false,
            forceUpdateBelowVersion: releaseVersion,
            out var policy,
            out var error);

        Assert.True(success);
        Assert.NotNull(policy);
        Assert.Equal(ReleasePolicyValidationError.None, error);
    }

    private static UpdateVersion Parse(string value)
    {
        Assert.True(UpdateVersion.TryParse(value, out var version, out _));
        return version;
    }
}
