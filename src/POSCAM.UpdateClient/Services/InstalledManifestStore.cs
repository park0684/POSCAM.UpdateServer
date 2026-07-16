using System;
using System.IO;
using System.Text;
using Newtonsoft.Json;
using POSCAM.UpdateClient.Models;

namespace POSCAM.UpdateClient.Services
{
    /// <summary>
    /// 설치 완료 Manifest와 증분 업데이트 실패 후 Full Package 전환 상태를 관리한다.
    /// </summary>
    internal sealed class InstalledManifestStore
    {
        private const string ManifestFileName = "installed-manifest.json";
        private const string FullPackageFallbackFileName =
            "full-package-fallback.flag";

        public InstalledManifest? Load(string installDirectory)
        {
            var path = GetManifestPath(installDirectory);
            if (!File.Exists(path))
            {
                return null;
            }

            try
            {
                var json = File.ReadAllText(path, Encoding.UTF8);
                var manifest = JsonConvert.DeserializeObject<InstalledManifest>(json);

                if (manifest == null
                    || manifest.ManifestVersion != 1
                    || string.IsNullOrWhiteSpace(manifest.ProductCode)
                    || string.IsNullOrWhiteSpace(manifest.Architecture)
                    || string.IsNullOrWhiteSpace(manifest.Version)
                    || manifest.Files == null)
                {
                    return null;
                }

                return manifest;
            }
            catch (JsonException)
            {
                return null;
            }
        }

        public void Save(
            string installDirectory,
            InstalledManifest manifest)
        {
            if (manifest == null)
            {
                throw new ArgumentNullException(nameof(manifest));
            }

            if (manifest.ManifestVersion != 1
                || string.IsNullOrWhiteSpace(manifest.ProductCode)
                || string.IsNullOrWhiteSpace(manifest.Architecture)
                || string.IsNullOrWhiteSpace(manifest.Version)
                || manifest.Files == null)
            {
                throw new InvalidDataException(
                    "저장할 설치 Manifest가 올바르지 않습니다.");
            }

            var path = GetManifestPath(installDirectory);
            var json = JsonConvert.SerializeObject(
                manifest,
                Formatting.Indented);
            WriteTextAtomically(path, json);
        }

        public void Delete(string installDirectory)
        {
            var path = GetManifestPath(installDirectory);
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }

        public bool IsFullPackageFallbackRequested(
            string installDirectory)
        {
            return File.Exists(GetFullPackageFallbackPath(installDirectory));
        }

        public void RequestFullPackageFallback(
            string installDirectory,
            string reason)
        {
            var safeReason = string.IsNullOrWhiteSpace(reason)
                ? "IncrementalApplyFailed"
                : reason.Trim();
            var content = DateTime.UtcNow.ToString("O")
                + Environment.NewLine
                + safeReason;

            WriteTextAtomically(
                GetFullPackageFallbackPath(installDirectory),
                content);
        }

        public void ClearFullPackageFallback(string installDirectory)
        {
            var path = GetFullPackageFallbackPath(installDirectory);
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }

        public string GetManifestPath(string installDirectory)
        {
            return Path.Combine(
                GetStateDirectory(installDirectory),
                ManifestFileName);
        }

        private string GetFullPackageFallbackPath(string installDirectory)
        {
            return Path.Combine(
                GetStateDirectory(installDirectory),
                FullPackageFallbackFileName);
        }

        private static string GetStateDirectory(string installDirectory)
        {
            if (string.IsNullOrWhiteSpace(installDirectory))
            {
                throw new ArgumentException(
                    "설치 경로가 비어 있습니다.",
                    nameof(installDirectory));
            }

            var installRoot = Path.GetFullPath(installDirectory.Trim());
            return Path.Combine(installRoot, "_update", "state");
        }

        private static void WriteTextAtomically(
            string destinationPath,
            string content)
        {
            var directory = Path.GetDirectoryName(destinationPath);
            if (string.IsNullOrWhiteSpace(directory))
            {
                throw new InvalidDataException(
                    "업데이트 상태 저장 경로를 확인할 수 없습니다.");
            }

            Directory.CreateDirectory(directory);
            var temporaryPath = destinationPath + ".tmp";

            try
            {
                File.WriteAllText(
                    temporaryPath,
                    content,
                    new UTF8Encoding(false));
                File.Copy(temporaryPath, destinationPath, true);
            }
            finally
            {
                if (File.Exists(temporaryPath))
                {
                    File.Delete(temporaryPath);
                }
            }
        }
    }
}
