namespace POSCAM.UpdateServer.Api.Options;

/// <summary>
/// X-Forwarded-For와 X-Forwarded-Proto를 신뢰할 역방향 프록시 IP 목록.
/// 광범위한 네트워크 전체를 신뢰하지 않고 운영자가 확인한 IP만 등록한다.
/// </summary>
public sealed class TrustedProxyOptions
{
    public const string SectionName = "ForwardedHeaders";

    public List<string> KnownProxies { get; set; } = new();

    public int ForwardLimit { get; set; } = 1;
}
