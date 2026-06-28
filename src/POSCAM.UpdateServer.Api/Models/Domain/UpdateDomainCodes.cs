namespace POSCAM.UpdateServer.Api.Models.Domain;

/// <summary>
/// 업데이트 대상 제품 코드.
/// DB와 API에서는 대문자 고정값을 사용한다.
/// </summary>
public static class ProductCodes
{
    public const string Pccam = "PCCAM";
    public const string CamViewer = "CAMVIEWER";
    public const string Updater = "UPDATER";

    public static bool IsSupported(string? value)
    {
        return value is Pccam or CamViewer or Updater;
    }
}

/// <summary>
/// 릴리스 채널 코드.
/// 채널은 자동 변환하지 않고 정확히 일치하는 값만 허용한다.
/// </summary>
public static class ReleaseChannels
{
    public const string Stable = "stable";
    public const string Beta = "beta";
    public const string Internal = "internal";

    public static bool IsSupported(string? value)
    {
        return value is Stable or Beta or Internal;
    }
}

/// <summary>
/// 업데이트 패키지가 지원하는 운영체제 코드.
/// </summary>
public static class UpdateOperatingSystems
{
    public const string Windows = "windows";

    public static bool IsSupported(string? value)
    {
        return value is Windows;
    }
}

/// <summary>
/// Artifact 아키텍처 코드와 호환 우선순위.
/// exact 아키텍처는 2, any는 1, 호환되지 않으면 0을 반환한다.
/// </summary>
public static class ArtifactArchitectures
{
    public const string X86 = "x86";
    public const string X64 = "x64";
    public const string Any = "any";

    public static bool IsSupported(string? value)
    {
        return value is X86 or X64 or Any;
    }

    public static int GetCompatibilityRank(
        string? requestedArchitecture,
        string? artifactArchitecture)
    {
        if (!IsSupported(requestedArchitecture)
            || !IsSupported(artifactArchitecture))
        {
            return 0;
        }

        if (string.Equals(
                requestedArchitecture,
                artifactArchitecture,
                StringComparison.Ordinal))
        {
            return 2;
        }

        return string.Equals(
                artifactArchitecture,
                Any,
                StringComparison.Ordinal)
            ? 1
            : 0;
    }
}

/// <summary>
/// 초기 UpdateServer가 지원하는 패키지 유형.
/// </summary>
public static class PackageTypes
{
    public const string Full = "full";

    public static bool IsSupported(string? value)
    {
        return value is Full;
    }
}

/// <summary>
/// 변경 이력에 저장하는 감사 작업 코드.
/// </summary>
public static class AuditActions
{
    public const string Create = "CREATE";
    public const string Update = "UPDATE";
    public const string Upload = "UPLOAD";
    public const string ReplaceDraftArtifact = "REPLACE_DRAFT_ARTIFACT";
    public const string Publish = "PUBLISH";
    public const string Disable = "DISABLE";
    public const string DeleteDraft = "DELETE_DRAFT";
}
