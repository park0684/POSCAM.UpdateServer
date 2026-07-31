using System;

namespace POSCAM.UpdateClient.Models
{
    /// <summary>
    /// startup-check 수행 결과다.
    /// </summary>
    internal sealed class StartupCheckResult
    {
        private const string UpdateClientFileName =
            "POSCAM.UpdateClient.exe";

        private bool _fullPackageUpdateRequired;
        private bool _incrementalUpdateRequired;

        public int ExitCode { get; set; }

        /// <summary>
        /// 명시적으로 Full Package가 필요하거나 실행 중인 UpdateClient 자체가
        /// 파일 복구 대상이면 true를 반환한다. UpdateClient는 실행 중인 자기
        /// 자신을 FileRepair로 직접 교체할 수 없으므로 worker 기반 Full Package
        /// 적용으로 전환해야 한다.
        /// </summary>
        public bool FullPackageUpdateRequired
        {
            get
            {
                return _fullPackageUpdateRequired
                    || RequiresFullPackageForSelfUpdate();
            }
            set { _fullPackageUpdateRequired = value; }
        }

        public bool IncrementalUpdateRequired
        {
            get
            {
                return _incrementalUpdateRequired
                    && !FullPackageUpdateRequired;
            }
            set { _incrementalUpdateRequired = value; }
        }

        public UpdateCheckResponse? UpdateResponse { get; set; }

        public RepairPlan RepairPlan { get; set; } = new RepairPlan();

        public InstalledManifest? TargetManifest { get; set; }

        private bool RequiresFullPackageForSelfUpdate()
        {
            if (RepairPlan == null || RepairPlan.Targets == null)
            {
                return false;
            }

            foreach (var target in RepairPlan.Targets)
            {
                if (target == null
                    || string.IsNullOrWhiteSpace(target.RelativePath))
                {
                    continue;
                }

                var normalizedPath = target.RelativePath
                    .Trim()
                    .Replace('\\', '/');

                if (string.Equals(
                    normalizedPath,
                    UpdateClientFileName,
                    StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
