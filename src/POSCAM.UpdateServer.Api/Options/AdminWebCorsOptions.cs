namespace POSCAM.UpdateServer.Api.Options;

/// <summary>
/// 브라우저 직접 Artifact 업로드를 허용할 AdminWeb Origin 목록.
/// 실제 CORS 정책 등록은 B09에서 수행한다.
/// </summary>
public sealed class AdminWebCorsOptions
{
    public const string SectionName = "Cors";

    public List<string> AdminWebOrigins { get; set; } = new();
}
