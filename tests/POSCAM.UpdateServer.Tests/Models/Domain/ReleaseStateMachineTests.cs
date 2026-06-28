using POSCAM.UpdateServer.Api.Models.Domain;
using POSCAM.UpdateServer.Api.Models.Enums;

namespace POSCAM.UpdateServer.Tests.Models.Domain;

public class ReleaseStateMachineTests
{
    [Theory]
    [InlineData(ReleaseStatus.Draft, ReleaseStatus.Published)]
    [InlineData(ReleaseStatus.Published, ReleaseStatus.Disabled)]
    public void Evaluate_정방향_상태전이를_허용한다(
        ReleaseStatus currentStatus,
        ReleaseStatus nextStatus)
    {
        var result = ReleaseStateMachine.Evaluate(
            currentStatus,
            nextStatus);

        Assert.True(result.IsAllowed);
        Assert.Equal(ReleaseTransitionError.None, result.Error);
        Assert.True(ReleaseStateMachine.CanTransition(currentStatus, nextStatus));
    }

    [Theory]
    [InlineData(ReleaseStatus.Draft, ReleaseStatus.Draft, ReleaseTransitionError.SameStatus)]
    [InlineData(ReleaseStatus.Published, ReleaseStatus.Published, ReleaseTransitionError.SameStatus)]
    [InlineData(ReleaseStatus.Disabled, ReleaseStatus.Disabled, ReleaseTransitionError.SameStatus)]
    [InlineData(ReleaseStatus.Published, ReleaseStatus.Draft, ReleaseTransitionError.PublishedCannotReturnToDraft)]
    [InlineData(ReleaseStatus.Disabled, ReleaseStatus.Draft, ReleaseTransitionError.DisabledReleaseCannotTransition)]
    [InlineData(ReleaseStatus.Disabled, ReleaseStatus.Published, ReleaseTransitionError.DisabledReleaseCannotTransition)]
    [InlineData(ReleaseStatus.Draft, ReleaseStatus.Disabled, ReleaseTransitionError.InvalidTransition)]
    public void Evaluate_허용되지_않은_상태전이를_거부한다(
        ReleaseStatus currentStatus,
        ReleaseStatus nextStatus,
        ReleaseTransitionError expectedError)
    {
        var result = ReleaseStateMachine.Evaluate(
            currentStatus,
            nextStatus);

        Assert.False(result.IsAllowed);
        Assert.Equal(expectedError, result.Error);
        Assert.False(ReleaseStateMachine.CanTransition(currentStatus, nextStatus));
    }
}
