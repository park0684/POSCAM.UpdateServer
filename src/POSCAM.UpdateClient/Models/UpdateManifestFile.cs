namespace POSCAM.UpdateClient.Models
{
    /// <summary>
    /// UpdateServer의 공개 Update Check 응답에 포함되는 파일별 Manifest 정보다.
    /// </summary>
    internal sealed class UpdateManifestFile
    {
        /// <summary>
        /// 설치 루트를 기준으로 하는 파일 상대 경로다.
        /// </summary>
        public string Path { get; set; } = "";

        /// <summary>
        /// 게시된 파일의 크기(Byte)다.
        /// </summary>
        public long Size { get; set; }

        /// <summary>
        /// 게시된 파일의 SHA-256 해시다.
        /// </summary>
        public string Sha256 { get; set; } = "";

        /// <summary>
        /// 프로그램 실행에 필요한 필수 파일인지 나타낸다.
        /// </summary>
        public bool Required { get; set; }

        /// <summary>
        /// 파일별 공개 다운로드 URL이다.
        /// </summary>
        public string DownloadUrl { get; set; } = "";
    }
}
