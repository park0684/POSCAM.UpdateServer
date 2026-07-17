namespace POSCAM.UpdateServer.Api.Models.Enums;

/// <summary>
/// UpdateServer API에서 사용하는 업무 및 공통 오류 코드.
/// </summary>
public enum UpdateErrorCode
{
    None = 0,

    InvalidLogin = 5001,
    TokenExpired = 5003,
    TokenInvalid = 5004,

    PermissionDenied = 7001,

    InvalidProduct = 8001,
    ProductInactive = 8002,
    InvalidVersion = 8003,
    InvalidOperatingSystem = 8004,
    InvalidArchitecture = 8005,
    InvalidChannel = 8006,

    ReleaseNotFound = 8010,
    DuplicateRelease = 8011,
    InvalidReleaseState = 8012,
    NoAvailableRelease = 8013,

    ArtifactNotFound = 8020,
    NoCompatibleArtifact = 8021,
    DuplicateArtifact = 8022,

    InvalidPackage = 8030,
    FileTooLarge = 8031,
    StorageError = 8032,
    PackageIntegrityError = 8033,

    ValidationError = 9001,
    DatabaseError = 9002,
    ExternalServiceUnavailable = 9003,
    RateLimitExceeded = 9004,
    UnknownError = 9999
}
