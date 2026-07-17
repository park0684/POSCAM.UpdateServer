namespace POSCAM.UpdateClient.Models
{
    /// <summary>
    /// 업데이트 파일을 임시 작업 경로로 다운로드하고 검증하기 위한 요청이다.
    /// </summary>
    internal sealed class UpdateFileDownloadRequest
    {
        public string DownloadUrl { get; set; } = "";

        public string DestinationPath { get; set; } = "";

        public long ExpectedSize { get; set; }

        public string ExpectedSha256 { get; set; } = "";
    }
}
