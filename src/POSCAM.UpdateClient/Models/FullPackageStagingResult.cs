using System.Collections.Generic;

namespace POSCAM.UpdateClient.Models
{
    /// <summary>
    /// Full ZIP 패키지를 안전하게 추출한 결과다.
    /// 아직 설치 경로의 원본 파일은 변경하지 않는다.
    /// </summary>
    internal sealed class FullPackageStagingResult
    {
        public string StagingDirectory { get; set; } = "";

        public List<UpdateApplyTarget> Targets { get; set; }
            = new List<UpdateApplyTarget>();
    }
}
