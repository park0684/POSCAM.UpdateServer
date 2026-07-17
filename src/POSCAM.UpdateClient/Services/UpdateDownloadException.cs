using System;

namespace POSCAM.UpdateClient.Services
{
    /// <summary>
    /// 업데이트 파일 다운로드 또는 검증 실패를 나타낸다.
    /// </summary>
    internal sealed class UpdateDownloadException : Exception
    {
        public UpdateDownloadException(string message)
            : base(message)
        {
        }

        public UpdateDownloadException(string message, Exception innerException)
            : base(message, innerException)
        {
        }
    }
}
