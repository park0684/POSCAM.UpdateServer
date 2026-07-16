using System;
using System.Configuration;
using POSCAM.UpdateClient.Models;

namespace POSCAM.UpdateClient.Services
{
    /// <summary>
    /// POSCAM.UpdateClient.exe.config에 저장된 배포 설정을 읽는다.
    /// 제품은 별도의 환경변수나 실행 명령 변경 없이 동일한 방식으로 실행하고,
    /// 배포 환경별로 설정 파일의 서버 주소만 다르게 구성한다.
    /// </summary>
    internal static class UpdateClientConfiguration
    {
        public const string UpdateServerBaseUrlKey = "UpdateServerBaseUrl";

        public static bool TryGetUpdateServerBaseUrl(out string baseUrl)
        {
            var configuredValue = ConfigurationManager.AppSettings[
                UpdateServerBaseUrlKey];

            return TryResolveUpdateServerBaseUrl(
                configuredValue,
                out baseUrl);
        }

        internal static bool TryResolveUpdateServerBaseUrl(
            string? configuredValue,
            out string baseUrl)
        {
            if (string.IsNullOrWhiteSpace(configuredValue))
            {
                baseUrl = StartupCheckOptions.DefaultBaseUrl;
                return true;
            }

            var candidate = configuredValue.Trim().TrimEnd('/');

            if (!Uri.TryCreate(candidate, UriKind.Absolute, out var uri)
                || (!string.Equals(
                        uri.Scheme,
                        Uri.UriSchemeHttp,
                        StringComparison.OrdinalIgnoreCase)
                    && !string.Equals(
                        uri.Scheme,
                        Uri.UriSchemeHttps,
                        StringComparison.OrdinalIgnoreCase)))
            {
                baseUrl = "";
                return false;
            }

            baseUrl = candidate;
            return true;
        }
    }
}
