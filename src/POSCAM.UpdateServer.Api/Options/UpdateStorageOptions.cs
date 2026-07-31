using System;
using System.ComponentModel.DataAnnotations;

namespace POSCAM.UpdateServer.Api.Options;

/// <summary>
/// 업데이트 ZIP 저장소와 검증 한도를 관리한다.
/// </summary>
public sealed class UpdateStorageOptions
{
    public const string SectionName = "UpdateStorage";

    [Required]
    public string RootPath { get; set; } = "/app/update-storage";

    [Required]
    public string PublicBaseUrl { get; set; } = "https://update.poscam.co.kr";

    [Range(typeof(long), "1", "9223372036854775807")]
    public long MaxUploadBytes { get; set; } = 1_073_741_824;

    [Range(1, int.MaxValue)]
    public int MaxArchiveEntries { get; set; } = 10_000;

    [Range(typeof(long), "1", "9223372036854775807")]
    public long MaxExpandedBytes { get; set; } = 4_294_967_296;

    /// <summary>
    /// 구형 UpdateClient가 자기 자신을 FileRepair 대상으로 처리하지 않도록
    /// files[] Manifest를 비우고 Full Package 적용을 강제할 릴리스 목록이다.
    /// 형식: PRODUCT_CODE:MAJOR.MINOR.PATCH[.REVISION]
    /// 예: PCCAM_X64:3.2.2
    /// </summary>
    public string[] ForceFullPackageReleaseKeys { get; set; }
        = Array.Empty<string>();

    public bool ShouldForceFullPackage(
        string? productCode,
        string? version)
    {
        if (string.IsNullOrWhiteSpace(productCode)
            || string.IsNullOrWhiteSpace(version)
            || ForceFullPackageReleaseKeys == null
            || ForceFullPackageReleaseKeys.Length == 0)
        {
            return false;
        }

        var expectedKey = productCode.Trim()
            + ":"
            + version.Trim();

        foreach (var configuredKey in ForceFullPackageReleaseKeys)
        {
            if (string.IsNullOrWhiteSpace(configuredKey))
            {
                continue;
            }

            if (string.Equals(
                configuredKey.Trim(),
                expectedKey,
                StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }
}
