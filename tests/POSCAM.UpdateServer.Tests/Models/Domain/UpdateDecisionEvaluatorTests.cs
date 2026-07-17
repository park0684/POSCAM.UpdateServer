using POSCAM.UpdateServer.Api.Models.Domain;
using POSCAM.UpdateServer.Api.Models.Enums;

namespace POSCAM.UpdateServer.Tests.Models.Domain;

public class UpdateDecisionEvaluatorTests
{
    [Fact]
    public void Evaluate_현재버전이_릴리스보다_낮으면_일반업데이트다()
    {
        var policy = CreatePolicy(
            "2.0.0",
            isMandatory: false,
            forceUpdateBelowVersion: null);

        var decision = UpdateDecisionEvaluator.Evaluate(
            Parse("1.9.0"),
            policy);

        Assert.True(decision.UpdateAvailable);
        Assert.False(decision.Mandatory);
        Assert.Equal(UpdateDecisionReason.UpdateAvailable, decision.Reason);
        Assert.Equal("UPDATE_AVAILABLE", decision.ReasonCode);
    }

    [Fact]
    public void Evaluate_전체강제_릴리스는_모든_하위버전을_강제한다()
    {
        var policy = CreatePolicy(
            "2.0.0",
            isMandatory: true,
            forceUpdateBelowVersion: null);

        var decision = UpdateDecisionEvaluator.Evaluate(
            Parse("1.9.9"),
            policy);

        Assert.True(decision.UpdateAvailable);
        Assert.True(decision.Mandatory);
        Assert.Equal(UpdateDecisionReason.MandatoryRelease, decision.Reason);
        Assert.Equal("MANDATORY_RELEASE", decision.ReasonCode);
    }

    [Fact]
    public void Evaluate_강제기준보다_낮으면_강제업데이트다()
    {
        var policy = CreatePolicy(
            "2.0.0",
            isMandatory: false,
            forceUpdateBelowVersion: "1.5.0");

        var decision = UpdateDecisionEvaluator.Evaluate(
            Parse("1.4.9"),
            policy);

        Assert.True(decision.UpdateAvailable);
        Assert.True(decision.Mandatory);
        Assert.Equal(UpdateDecisionReason.ForceUpdateBelowVersion, decision.Reason);
        Assert.Equal("FORCE_UPDATE_BELOW_VERSION", decision.ReasonCode);
    }

    [Fact]
    public void Evaluate_강제기준과_같은버전은_강제하지_않는다()
    {
        var policy = CreatePolicy(
            "2.0.0",
            isMandatory: false,
            forceUpdateBelowVersion: "1.5.0");

        var decision = UpdateDecisionEvaluator.Evaluate(
            Parse("1.5.0"),
            policy);

        Assert.True(decision.UpdateAvailable);
        Assert.False(decision.Mandatory);
        Assert.Equal(UpdateDecisionReason.UpdateAvailable, decision.Reason);
    }

    [Fact]
    public void Evaluate_현재버전이_최신이면_업데이트가_없다()
    {
        var policy = CreatePolicy(
            "2.0.0",
            isMandatory: true,
            forceUpdateBelowVersion: null);

        var decision = UpdateDecisionEvaluator.Evaluate(
            Parse("2.0.0.0"),
            policy);

        Assert.False(decision.UpdateAvailable);
        Assert.False(decision.Mandatory);
        Assert.Equal(UpdateDecisionReason.AlreadyLatest, decision.Reason);
        Assert.Equal("ALREADY_LATEST", decision.ReasonCode);
    }

    [Fact]
    public void Evaluate_현재버전이_서버보다_높으면_다운그레이드하지_않는다()
    {
        var policy = CreatePolicy(
            "2.0.0",
            isMandatory: true,
            forceUpdateBelowVersion: null);

        var decision = UpdateDecisionEvaluator.Evaluate(
            Parse("2.0.1"),
            policy);

        Assert.False(decision.UpdateAvailable);
        Assert.False(decision.Mandatory);
        Assert.Equal(UpdateDecisionReason.ClientVersionAhead, decision.Reason);
        Assert.Equal("CLIENT_VERSION_AHEAD", decision.ReasonCode);
    }

    [Theory]
    [InlineData(UpdateDecisionReason.NoAvailableRelease, "NO_AVAILABLE_RELEASE")]
    [InlineData(UpdateDecisionReason.NoCompatibleArtifact, "NO_COMPATIBLE_ARTIFACT")]
    public void ReasonCode_업데이트없음_사유도_고정문자열을_반환한다(
        UpdateDecisionReason reason,
        string expectedCode)
    {
        Assert.Equal(expectedCode, reason.ToCode());
    }

    private static ReleaseUpdatePolicy CreatePolicy(
        string releaseVersion,
        bool isMandatory,
        string? forceUpdateBelowVersion)
    {
        var threshold = forceUpdateBelowVersion is null
            ? (UpdateVersion?)null
            : Parse(forceUpdateBelowVersion);

        var success = ReleaseUpdatePolicy.TryCreate(
            Parse(releaseVersion),
            isMandatory,
            threshold,
            out var policy,
            out var error);

        Assert.True(success, $"릴리스 정책 생성 실패: {error}");
        Assert.NotNull(policy);
        return policy!;
    }

    private static UpdateVersion Parse(string value)
    {
        Assert.True(UpdateVersion.TryParse(value, out var version, out _));
        return version;
    }
}
