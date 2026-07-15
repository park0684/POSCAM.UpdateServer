using System;
using System.Collections.Generic;

namespace POSCAM.UpdateClient.Models
{
    /// <summary>
    /// startup-check 명령의 실행 옵션이다.
    /// </summary>
    internal sealed class StartupCheckOptions
    {
        public const string DefaultBaseUrl = "https://update.poscam.co.kr";
        public const string DefaultOperatingSystem = "windows";
        public const string DefaultChannel = "stable";

        public string BaseUrl { get; set; } = DefaultBaseUrl;

        public string ProductCode { get; set; } = "";

        public string OperatingSystem { get; set; } = DefaultOperatingSystem;

        public string Architecture { get; set; } = "";

        public string Channel { get; set; } = DefaultChannel;

        public string InstallDirectory { get; set; } = "";

        public string ApplicationFileName { get; set; } = "";

        public string? CurrentVersionOverride { get; set; }

        public static bool TryParse(
            string[] args,
            out StartupCheckOptions? options)
        {
            options = null;

            if (args == null || args.Length < 9)
            {
                return false;
            }

            var values = new Dictionary<string, string>(
                StringComparer.OrdinalIgnoreCase);

            for (var index = 1; index < args.Length; index += 2)
            {
                var key = args[index];

                if (string.IsNullOrWhiteSpace(key)
                    || !key.StartsWith("--", StringComparison.Ordinal)
                    || index + 1 >= args.Length)
                {
                    return false;
                }

                var value = args[index + 1];

                if (string.IsNullOrWhiteSpace(value)
                    || values.ContainsKey(key))
                {
                    return false;
                }

                values.Add(key, value.Trim());
            }

            foreach (var key in values.Keys)
            {
                if (!IsSupportedKey(key))
                {
                    return false;
                }
            }

            if (!values.TryGetValue(
                    "--install-dir",
                    out var installDirectory)
                || !values.TryGetValue(
                    "--app",
                    out var applicationFileName)
                || !values.TryGetValue(
                    "--product-code",
                    out var productCode)
                || !values.TryGetValue(
                    "--architecture",
                    out var architecture)
                || !UpdateProductIdentity.TryNormalize(
                    productCode,
                    architecture,
                    out var normalizedProductCode,
                    out var normalizedArchitecture))
            {
                return false;
            }

            var parsed = new StartupCheckOptions
            {
                InstallDirectory = installDirectory,
                ApplicationFileName = applicationFileName,
                ProductCode = normalizedProductCode,
                Architecture = normalizedArchitecture
            };

            if (values.TryGetValue("--base-url", out var baseUrl))
            {
                parsed.BaseUrl = baseUrl;
            }

            if (values.TryGetValue("--os", out var operatingSystem))
            {
                parsed.OperatingSystem = operatingSystem;
            }

            if (values.TryGetValue("--channel", out var channel))
            {
                parsed.Channel = channel;
            }

            if (values.TryGetValue(
                "--current-version",
                out var currentVersion))
            {
                parsed.CurrentVersionOverride = currentVersion;
            }

            options = parsed;
            return true;
        }

        private static bool IsSupportedKey(string key)
        {
            return string.Equals(
                    key,
                    "--install-dir",
                    StringComparison.OrdinalIgnoreCase)
                || string.Equals(
                    key,
                    "--app",
                    StringComparison.OrdinalIgnoreCase)
                || string.Equals(
                    key,
                    "--base-url",
                    StringComparison.OrdinalIgnoreCase)
                || string.Equals(
                    key,
                    "--product-code",
                    StringComparison.OrdinalIgnoreCase)
                || string.Equals(
                    key,
                    "--os",
                    StringComparison.OrdinalIgnoreCase)
                || string.Equals(
                    key,
                    "--architecture",
                    StringComparison.OrdinalIgnoreCase)
                || string.Equals(
                    key,
                    "--channel",
                    StringComparison.OrdinalIgnoreCase)
                || string.Equals(
                    key,
                    "--current-version",
                    StringComparison.OrdinalIgnoreCase);
        }
    }
}
