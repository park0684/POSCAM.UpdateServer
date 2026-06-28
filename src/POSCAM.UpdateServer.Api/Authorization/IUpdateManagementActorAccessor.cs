using POSCAM.UpdateServer.Api.Models.Authorization;

namespace POSCAM.UpdateServer.Api.Authorization;

/// <summary>
/// 현재 관리자 요청에서 AuthServer가 확인한 Actor를 제공한다.
/// </summary>
public interface IUpdateManagementActorAccessor
{
    UpdateManagementActor? Actor { get; }

    void SetActor(UpdateManagementActor actor);
}
