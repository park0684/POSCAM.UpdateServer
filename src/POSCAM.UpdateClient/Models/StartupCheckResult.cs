using System;

namespace POSCAM.UpdateClient.Models
{
    /// <summary>
    /// startup-check 수행 결과다.
    /// </summary>
    internal sealed class StartupCheckResult
    {
        private static readonly string[] FullPackageProtectedFileNames =
        {
            "PcCam.exe",
            "POSCAM.UpdateClient.exe"
        };

        private bool _fullPackageUpdateRequired;
        private bool _incrementalUpdateRequired;

        public int ExitCode { get; set; }

        /// <summary>
        /// 명시적으로 Full Package가 필요하거나 상위 버전 업데이트 계획에
        /// PC CAM 본체 또는 UpdateClient가 포함되면 true를 반환한다.
        /// 두 핵심 실행 파일은 파일 단위 적용으로 교체하지 않고 worker 기반
        /// Full Package 적용으로 전환한다.
        /// </summary>
        public bool FullPackageUpdateRequired
        {
            get
            {
                return _fullPackageUpdateRequired
                    || RequiresFullPackageForProtectedExecutable();
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

        private bool RequiresFullPackageForProtectedExecutable()
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

                foreach (var protectedFileName in
                    FullPackageProtectedFileNames)
                {
                    if (string.Equals(
                        normalizedPath,
                        protectedFileName,
                        StringComparison.OrdinalIgnoreCase))
                    {
                        return true;
                    }
                }
            }

            return false;
        }
    }
}
