namespace POSCAM.UpdateServer.Api.Models.Dtos.Updates;

/// <summary>
/// 익명 클라이언트가 현재 실행 버전과 환경을 전달하는 업데이트 확인 요청.
/// 값은 자동 보정하지 않고 확정된 코드와 버전 형식에 정확히 일치해야 한다.
/// </summary>
public sealed class UpdateCheckRequest
{
    public string? ProductCode { get; set; }

    public string? CurrentVersion { get; set; }

    public string? Os { get; set; }

    public string? Architecture { get; set; }

    public string? Channel { get; set; }
}
