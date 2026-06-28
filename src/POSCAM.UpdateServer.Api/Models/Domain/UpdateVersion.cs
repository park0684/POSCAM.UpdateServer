using System.Globalization;
using POSCAM.UpdateServer.Api.Models.Enums;

namespace POSCAM.UpdateServer.Api.Models.Domain;

/// <summary>
/// POSCAM 업데이트 버전을 숫자 구성요소로 보관하고 비교하는 값 객체.
/// 지원 형식은 Major.Minor.Patch 또는 Major.Minor.Patch.Revision이다.
/// </summary>
public readonly record struct UpdateVersion : IComparable<UpdateVersion>
{
    public const int MaxComponentValue = 65_535;

    public int Major { get; }

    public int Minor { get; }

    public int Patch { get; }

    public int Revision { get; }

    private UpdateVersion(
        int major,
        int minor,
        int patch,
        int revision)
    {
        Major = major;
        Minor = minor;
        Patch = patch;
        Revision = revision;
    }

    /// <summary>
    /// 숫자 구성요소로 버전을 생성한다.
    /// 모든 값은 0~65535 범위여야 한다.
    /// </summary>
    public static bool TryCreate(
        int major,
        int minor,
        int patch,
        int revision,
        out UpdateVersion version,
        out UpdateVersionParseError error)
    {
        if (!IsComponentInRange(major)
            || !IsComponentInRange(minor)
            || !IsComponentInRange(patch)
            || !IsComponentInRange(revision))
        {
            version = default;
            error = UpdateVersionParseError.ComponentOutOfRange;
            return false;
        }

        version = new UpdateVersion(major, minor, patch, revision);
        error = UpdateVersionParseError.None;
        return true;
    }

    /// <summary>
    /// 버전 문자열을 엄격하게 해석한다.
    /// 공백, 접두어, 접미어, 선행 0, 두 자리 또는 다섯 자리 이상 버전은 허용하지 않는다.
    /// </summary>
    public static bool TryParse(
        string? value,
        out UpdateVersion version,
        out UpdateVersionParseError error)
    {
        version = default;

        if (string.IsNullOrWhiteSpace(value))
        {
            error = UpdateVersionParseError.Required;
            return false;
        }

        if (!string.Equals(value, value.Trim(), StringComparison.Ordinal))
        {
            error = UpdateVersionParseError.InvalidFormat;
            return false;
        }

        var parts = value.Split('.', StringSplitOptions.None);

        if (parts.Length is not (3 or 4))
        {
            error = UpdateVersionParseError.InvalidFormat;
            return false;
        }

        var components = new int[4];

        for (var index = 0; index < parts.Length; index++)
        {
            var part = parts[index];

            if (part.Length == 0 || !ContainsOnlyAsciiDigits(part))
            {
                error = UpdateVersionParseError.InvalidFormat;
                return false;
            }

            if (part.Length > 1 && part[0] == '0')
            {
                error = UpdateVersionParseError.LeadingZeroNotAllowed;
                return false;
            }

            if (!int.TryParse(
                    part,
                    NumberStyles.None,
                    CultureInfo.InvariantCulture,
                    out var component)
                || !IsComponentInRange(component))
            {
                error = UpdateVersionParseError.ComponentOutOfRange;
                return false;
            }

            components[index] = component;
        }

        version = new UpdateVersion(
            components[0],
            components[1],
            components[2],
            parts.Length == 4 ? components[3] : 0);

        error = UpdateVersionParseError.None;
        return true;
    }

    public int CompareTo(UpdateVersion other)
    {
        var result = Major.CompareTo(other.Major);
        if (result != 0)
        {
            return result;
        }

        result = Minor.CompareTo(other.Minor);
        if (result != 0)
        {
            return result;
        }

        result = Patch.CompareTo(other.Patch);
        if (result != 0)
        {
            return result;
        }

        return Revision.CompareTo(other.Revision);
    }

    /// <summary>
    /// Revision이 0이면 세 자리, 0보다 크면 네 자리 문자열로 정규화한다.
    /// </summary>
    public override string ToString()
    {
        return Revision == 0
            ? FormattableString.Invariant($"{Major}.{Minor}.{Patch}")
            : FormattableString.Invariant($"{Major}.{Minor}.{Patch}.{Revision}");
    }

    public static bool operator <(UpdateVersion left, UpdateVersion right)
    {
        return left.CompareTo(right) < 0;
    }

    public static bool operator <=(UpdateVersion left, UpdateVersion right)
    {
        return left.CompareTo(right) <= 0;
    }

    public static bool operator >(UpdateVersion left, UpdateVersion right)
    {
        return left.CompareTo(right) > 0;
    }

    public static bool operator >=(UpdateVersion left, UpdateVersion right)
    {
        return left.CompareTo(right) >= 0;
    }

    private static bool IsComponentInRange(int value)
    {
        return value is >= 0 and <= MaxComponentValue;
    }

    private static bool ContainsOnlyAsciiDigits(string value)
    {
        foreach (var character in value)
        {
            if (character is < '0' or > '9')
            {
                return false;
            }
        }

        return true;
    }
}
