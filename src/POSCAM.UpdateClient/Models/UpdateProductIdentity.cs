using System;

namespace POSCAM.UpdateClient.Models
{
    /// <summary>
    /// 업데이트 요청과 적용 계획에서 사용하는 제품·아키텍처 식별값을 검증한다.
    /// 클라이언트는 실제 설치 프로그램의 아키텍처만 전달하므로 any는 허용하지 않는다.
    /// </summary>
    internal static class UpdateProductIdentity
    {
        public const string PcCamProductCode = "PCCAM";
        public const string CamViewerProductCode = "CAMVIEWER";
        public const string UpdaterProductCode = "UPDATER";
        public const string X86Architecture = "x86";
        public const string X64Architecture = "x64";

        public static bool TryNormalize(
            string? productCode,
            string? architecture,
            out string normalizedProductCode,
            out string normalizedArchitecture)
        {
            normalizedProductCode = "";
            normalizedArchitecture = "";

            if (productCode == null || architecture == null)
            {
                return false;
            }

            var candidateProductCode = productCode
                .Trim()
                .ToUpperInvariant();
            var candidateArchitecture = architecture
                .Trim()
                .ToLowerInvariant();

            if (candidateProductCode.Length == 0
                || candidateArchitecture.Length == 0
                || !IsSupportedProductCode(candidateProductCode)
                || !IsSupportedArchitecture(candidateArchitecture))
            {
                return false;
            }

            normalizedProductCode = candidateProductCode;
            normalizedArchitecture = candidateArchitecture;
            return true;
        }

        private static bool IsSupportedProductCode(string value)
        {
            return string.Equals(
                    value,
                    PcCamProductCode,
                    StringComparison.Ordinal)
                || string.Equals(
                    value,
                    CamViewerProductCode,
                    StringComparison.Ordinal)
                || string.Equals(
                    value,
                    UpdaterProductCode,
                    StringComparison.Ordinal);
        }

        private static bool IsSupportedArchitecture(string value)
        {
            return string.Equals(
                    value,
                    X86Architecture,
                    StringComparison.Ordinal)
                || string.Equals(
                    value,
                    X64Architecture,
                    StringComparison.Ordinal);
        }
    }
}
