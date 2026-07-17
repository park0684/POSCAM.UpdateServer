namespace POSCAM.UpdateClient.Models
{
    /// <summary>
    /// 한 번의 startup-check 작업에서 사용하는 로컬 경로 모음이다.
    /// </summary>
    internal sealed class UpdateWorkPaths
    {
        public string JobId { get; set; } = "";

        public string InstallDirectory { get; set; } = "";

        public string UpdateRootDirectory { get; set; } = "";

        public string DownloadsRootDirectory { get; set; } = "";

        public string JobDirectory { get; set; } = "";

        public string StateDirectory { get; set; } = "";

        public string ActivePlanPath { get; set; } = "";
    }
}
