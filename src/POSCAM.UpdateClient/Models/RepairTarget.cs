namespace POSCAM.UpdateClient.Models
{
    /// <summary>
    /// 로컬 검사 결과 복구가 필요하다고 판단된 파일이다.
    /// </summary>
    internal sealed class RepairTarget
    {
        public string RelativePath { get; set; } = "";

        public string LocalPath { get; set; } = "";

        public long ExpectedSize { get; set; }

        public string ExpectedSha256 { get; set; } = "";

        public string DownloadUrl { get; set; } = "";

        public string Reason { get; set; } = "";
    }
}
