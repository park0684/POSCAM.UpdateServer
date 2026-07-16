using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace POSCAM.UpdateClient.Models
{
    /// <summary>
    /// 클라이언트 설치 경로에 실제 적용된 릴리스와 관리 파일 목록이다.
    /// 다음 버전에서 제거된 파일을 안전하게 식별할 때 사용한다.
    /// </summary>
    internal sealed class InstalledManifest
    {
        [JsonProperty("manifestVersion")]
        public int ManifestVersion { get; set; } = 1;

        [JsonProperty("productCode")]
        public string ProductCode { get; set; } = "";

        [JsonProperty("architecture")]
        public string Architecture { get; set; } = "";

        [JsonProperty("version")]
        public string Version { get; set; } = "";

        [JsonProperty("installedAtUtc")]
        public DateTime InstalledAtUtc { get; set; }

        [JsonProperty("files")]
        public List<InstalledManifestFile> Files { get; set; }
            = new List<InstalledManifestFile>();
    }
}
