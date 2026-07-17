using Newtonsoft.Json;

namespace POSCAM.UpdateClient.Models
{
    /// <summary>
    /// 설치가 완료된 업데이트 관리 파일의 최종 상태다.
    /// 사용자 설정과 로그처럼 업데이트 관리 대상이 아닌 파일은 포함하지 않는다.
    /// </summary>
    internal sealed class InstalledManifestFile
    {
        [JsonProperty("path")]
        public string Path { get; set; } = "";

        [JsonProperty("size")]
        public long Size { get; set; }

        [JsonProperty("sha256")]
        public string Sha256 { get; set; } = "";
    }
}
