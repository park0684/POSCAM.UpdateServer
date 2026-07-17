using Newtonsoft.Json;

namespace POSCAM.UpdateClient.Models
{
    /// <summary>
    /// 공개 Update Check API 요청이다.
    /// </summary>
    internal sealed class UpdateCheckRequest
    {
        [JsonProperty("productCode")]
        public string ProductCode { get; set; } = "";

        [JsonProperty("currentVersion")]
        public string CurrentVersion { get; set; } = "";

        [JsonProperty("os")]
        public string Os { get; set; } = "";

        [JsonProperty("architecture")]
        public string Architecture { get; set; } = "";

        [JsonProperty("channel")]
        public string Channel { get; set; } = "";
    }
}
