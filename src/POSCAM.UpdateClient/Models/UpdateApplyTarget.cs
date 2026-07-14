using Newtonsoft.Json;

namespace POSCAM.UpdateClient.Models
{
    /// <summary>
    /// 검증이 완료되어 apply 단계에서 교체할 파일 정보다.
    /// </summary>
    internal sealed class UpdateApplyTarget
    {
        [JsonProperty("relativePath")]
        public string RelativePath { get; set; } = "";

        [JsonProperty("downloadedPath")]
        public string DownloadedPath { get; set; } = "";

        [JsonProperty("expectedSize")]
        public long ExpectedSize { get; set; }

        [JsonProperty("expectedSha256")]
        public string ExpectedSha256 { get; set; } = "";

        [JsonProperty("reason")]
        public string Reason { get; set; } = "";
    }
}
