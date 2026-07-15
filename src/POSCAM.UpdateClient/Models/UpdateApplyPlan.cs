using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace POSCAM.UpdateClient.Models
{
    /// <summary>
    /// startup-check가 생성하고 apply 명령이 소비하는 적용 계획이다.
    /// </summary>
    internal sealed class UpdateApplyPlan
    {
        [JsonProperty("planVersion")]
        public int PlanVersion { get; set; } = 1;

        [JsonProperty("jobId")]
        public string JobId { get; set; } = "";

        [JsonProperty("productCode")]
        public string ProductCode { get; set; } = "";

        [JsonProperty("architecture")]
        public string Architecture { get; set; } = "";

        [JsonProperty("installDirectory")]
        public string InstallDirectory { get; set; } = "";

        [JsonProperty("applicationFileName")]
        public string ApplicationFileName { get; set; } = "";

        [JsonProperty("mode")]
        public string Mode { get; set; } = "";

        [JsonProperty("createdAtUtc")]
        public DateTime CreatedAtUtc { get; set; }

        [JsonProperty("latestVersion")]
        public string? LatestVersion { get; set; }

        [JsonProperty("packageType")]
        public string? PackageType { get; set; }

        [JsonProperty("packageFileName")]
        public string? PackageFileName { get; set; }

        [JsonProperty("packagePath")]
        public string? PackagePath { get; set; }

        [JsonProperty("packageSize")]
        public long? PackageSize { get; set; }

        [JsonProperty("packageSha256")]
        public string? PackageSha256 { get; set; }

        [JsonProperty("targets")]
        public List<UpdateApplyTarget> Targets { get; set; }
            = new List<UpdateApplyTarget>();
    }
}
