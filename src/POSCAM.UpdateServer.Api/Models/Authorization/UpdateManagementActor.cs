namespace POSCAM.UpdateServer.Api.Models.Authorization;

/// <summary>
/// AuthServer가 확인한 현재 업데이트 관리 작업자 정보.
/// 요청 본문이나 브라우저가 전달한 값은 사용하지 않는다.
/// </summary>
public sealed class UpdateManagementActor
{
    public int UserCode { get; init; }

    public string UserName { get; init; } = string.Empty;

    public int UserRole { get; init; }
}
