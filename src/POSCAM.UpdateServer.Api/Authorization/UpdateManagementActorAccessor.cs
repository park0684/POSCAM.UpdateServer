using POSCAM.UpdateServer.Api.Models.Authorization;

namespace POSCAM.UpdateServer.Api.Authorization;

/// <summary>
/// 하나의 HTTP 요청 범위에서만 Actor를 보관한다.
/// </summary>
public sealed class UpdateManagementActorAccessor : IUpdateManagementActorAccessor
{
    private UpdateManagementActor? _actor;

    public UpdateManagementActor? Actor => _actor;

    public void SetActor(UpdateManagementActor actor)
    {
        ArgumentNullException.ThrowIfNull(actor);

        if (_actor is not null)
        {
            throw new InvalidOperationException("현재 요청의 업데이트 관리 작업자가 이미 설정되었습니다.");
        }

        _actor = actor;
    }
}
