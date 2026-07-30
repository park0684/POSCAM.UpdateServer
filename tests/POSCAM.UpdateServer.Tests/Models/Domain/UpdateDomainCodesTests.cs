using POSCAM.UpdateServer.Api.Models.Domain;

namespace POSCAM.UpdateServer.Tests.Models.Domain;

public class UpdateDomainCodesTests
{
    [Theory]
    [InlineData("x86", "x86", 2)]
    [InlineData("x64", "x64", 2)]
    [InlineData("x86", "any", 1)]
    [InlineData("x64", "any", 1)]
    [InlineData("x86", "x64", 0)]
    [InlineData("x64", "x86", 0)]
    [InlineData("x86", "X86", 0)]
    [InlineData("arm64", "any", 0)]
    public void GetCompatibilityRank_exact가_any보다_우선한다(
        string requestedArchitecture,
        string artifactArchitecture,
        int expectedRank)
    {
        var rank = ArtifactArchitectures.GetCompatibilityRank(
            requestedArchitecture,
            artifactArchitecture);

        Assert.Equal(expectedRank, rank);
    }

    [Theory]
    [InlineData("PCCAM", true)]
    [InlineData("PCCAM_X86", true)]
    [InlineData("PCCAM_X64", true)]
    [InlineData("CAMVIEWER", true)]
    [InlineData("UPDATER", true)]
    [InlineData("pccam", false)]
    [InlineData("pccam_x86", false)]
    [InlineData("UNKNOWN", false)]
    public void ProductCodes_대문자_고정코드만_허용한다(
        string productCode,
        bool expected)
    {
        Assert.Equal(expected, ProductCodes.IsSupported(productCode));
    }

    [Theory]
    [InlineData("PCCAM", "x86", true)]
    [InlineData("PCCAM", "x64", true)]
    [InlineData("PCCAM_X86", "x86", true)]
    [InlineData("PCCAM_X86", "x64", false)]
    [InlineData("PCCAM_X64", "x64", true)]
    [InlineData("PCCAM_X64", "x86", false)]
    [InlineData("CAMVIEWER", "x64", true)]
    [InlineData("UPDATER", "x86", true)]
    [InlineData("PCCAM_X86", "any", false)]
    [InlineData("UNKNOWN", "x86", false)]
    public void ProductCodes_제품과_아키텍처_조합을_검증한다(
        string productCode,
        string architecture,
        bool expected)
    {
        Assert.Equal(
            expected,
            ProductCodes.IsArchitectureCompatible(productCode, architecture));
    }

    [Theory]
    [InlineData("stable", true)]
    [InlineData("beta", true)]
    [InlineData("internal", true)]
    [InlineData("Stable", false)]
    [InlineData("release", false)]
    public void ReleaseChannels_정확히_일치하는_채널만_허용한다(
        string channel,
        bool expected)
    {
        Assert.Equal(expected, ReleaseChannels.IsSupported(channel));
    }
}
