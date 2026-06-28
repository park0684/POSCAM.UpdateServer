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
}
