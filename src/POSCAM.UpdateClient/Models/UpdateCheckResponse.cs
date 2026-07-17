using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace POSCAM.UpdateClient.Models
{
    /// <summary>
    /// 공개 Update Check API의 응답 데이터다.
    /// </summary>
    internal sealed class UpdateCheckResponse
    {
        [JsonProperty("updateAvailable")]
        public bool UpdateAvailable { get; set; }

        [JsonProperty("mandatory")]
        public bool Mandatory { get; set; }

        [JsonProperty("reasonCode")]
        public string ReasonCode { get; set; } = "";

        [JsonProperty("productCode")]
        public string ProductCode { get; set; } = "";

        [JsonProperty("currentVersion")]
        public string CurrentVersion { get; set; } = "";

        [JsonProperty("latestVersion")]
        public string? LatestVersion { get; set; }

        [JsonProperty("forceUpdateBelowVersion")]
        public string? ForceUpdateBelowVersion { get; set; }

        [JsonProperty("channel")]
        public string Channel { get; set; } = "";

        [JsonProperty("os")]
        public string Os { get; set; } = "";

        [JsonProperty("architecture")]
        public string Architecture { get; set; } = "";

        [JsonProperty("packageType")]
        public string? PackageType { get; set; }

        [JsonProperty("packageUrl")]
        public string? PackageUrl { get; set; }

        [JsonProperty("fileName")]
        public string? FileName { get; set; }

        [JsonProperty("fileSize")]
        public long? FileSize { get; set; }

        [JsonProperty("sha256")]
        public string? Sha256 { get; set; }

        [JsonProperty("releaseNotes")]
        public string? ReleaseNotes { get; set; }

        [JsonProperty("publishedAt")]
        public DateTime? PublishedAt { get; set; }

        [JsonProperty("files")]
        public List<UpdateManifestFile> Files { get; set; }
            = new List<UpdateManifestFile>();
    }
}
