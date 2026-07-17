using System.Collections.Generic;

namespace POSCAM.UpdateClient.Models
{
    /// <summary>
    /// Manifest와 로컬 파일을 비교한 결과 생성되는 복구 계획이다.
    /// </summary>
    internal sealed class RepairPlan
    {
        public List<RepairTarget> Targets { get; set; }
            = new List<RepairTarget>();

        public bool HasRepairTargets
        {
            get { return Targets.Count > 0; }
        }
    }
}
