using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using POSCAM.UpdateClient.Models;

namespace POSCAM.UpdateClient.Services
{
    /// <summary>
    /// Update Check 요청에 사용할 대상 프로그램 버전을 결정한다.
    /// 증분 업데이트가 적용된 이후에는 설치 Manifest 버전을 우선한다.
    /// </summary>
    internal sealed class ApplicationVersionResolver
    {
        private readonly InstalledManifestStore _installedManifestStore;

        public ApplicationVersionResolver()
            : this(new InstalledManifestStore())
        {
        }

        internal ApplicationVersionResolver(
            InstalledManifestStore installedManifestStore)
        {
            _installedManifestStore = installedManifestStore
                ?? throw new ArgumentNullException(
                    nameof(installedManifestStore));
        }

        public string Resolve(StartupCheckOptions options)
        {
            if (options == null)
            {
                throw new ArgumentNullException(nameof(options));
            }

            var currentVersionOverride = options.CurrentVersionOverride;

            if (currentVersionOverride != null)
            {
                var normalizedOverride = currentVersionOverride.Trim();

                if (normalizedOverride.Length > 0)
                {
                    return normalizedOverride;
                }
            }

            if (string.IsNullOrWhiteSpace(options.InstallDirectory))
            {
                throw new InvalidDataException(
                    "설치 경로가 비어 있습니다.");
            }

            InstalledManifest? installedManifest = null;

            try
            {
                installedManifest = _installedManifestStore.Load(
                    options.InstallDirectory);
            }
            catch (Exception exception)
                when (exception is InvalidDataException
                    || exception is IOException
                    || exception is UnauthorizedAccessException)
            {
                // 설치 Manifest가 손상되거나 읽을 수 없으면 EXE 버전으로 확인한다.
                // 이후 Update Check 단계에서 Full Package 복구 경로를 선택한다.
            }

            if (installedManifest != null)
            {
                if (!UpdateProductIdentity.TryNormalize(
                        installedManifest.ProductCode,
                        installedManifest.Architecture,
                        out var installedProductCode,
                        out var installedArchitecture)
                    || !UpdateProductIdentity.TryNormalize(
                        options.ProductCode,
                        options.Architecture,
                        out var requestedProductCode,
                        out var requestedArchitecture)
                    || !string.Equals(
                        installedProductCode,
                        requestedProductCode,
                        StringComparison.Ordinal)
                    || !string.Equals(
                        installedArchitecture,
                        requestedArchitecture,
                        StringComparison.Ordinal)
                    || string.IsNullOrWhiteSpace(installedManifest.Version))
                {
                    installedManifest = null;
                }
                else
                {
                    return installedManifest.Version.Trim();
                }
            }

            if (string.IsNullOrWhiteSpace(options.ApplicationFileName))
            {
                throw new InvalidDataException(
                    "대상 프로그램 파일명이 비어 있습니다.");
            }

            var applicationFileName = options.ApplicationFileName.Trim();

            if (Path.IsPathRooted(applicationFileName)
                || applicationFileName.IndexOf('/') >= 0
                || applicationFileName.IndexOf('\\') >= 0
                || applicationFileName.IndexOf(':') >= 0
                || applicationFileName.IndexOfAny(
                    Path.GetInvalidFileNameChars()) >= 0)
            {
                throw new InvalidDataException(
                    "대상 프로그램은 설치 루트의 파일명만 지정할 수 있습니다.");
            }

            var applicationPath = Path.Combine(
                Path.GetFullPath(options.InstallDirectory.Trim()),
                applicationFileName);

            if (!File.Exists(applicationPath))
            {
                throw new FileNotFoundException(
                    "버전을 확인할 대상 프로그램을 찾을 수 없습니다.",
                    applicationPath);
            }

            var fileVersion = FileVersionInfo
                .GetVersionInfo(applicationPath)
                .FileVersion;

            if (fileVersion != null)
            {
                var normalizedFileVersion = fileVersion.Trim();

                if (normalizedFileVersion.Length > 0)
                {
                    return normalizedFileVersion;
                }
            }

            try
            {
                var assemblyVersion = AssemblyName
                    .GetAssemblyName(applicationPath)
                    .Version;

                if (assemblyVersion != null)
                {
                    return assemblyVersion.ToString();
                }
            }
            catch (BadImageFormatException)
            {
                // 관리형 어셈블리가 아니면 아래의 공통 실패로 처리한다.
            }

            throw new InvalidDataException(
                "대상 프로그램의 버전 정보를 확인할 수 없습니다.");
        }
    }
}
