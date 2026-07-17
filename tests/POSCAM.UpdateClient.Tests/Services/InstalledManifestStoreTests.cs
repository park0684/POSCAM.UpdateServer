using System;
using System.Collections.Generic;
using System.IO;
using POSCAM.UpdateClient.Models;
using POSCAM.UpdateClient.Services;
using Xunit;

namespace POSCAM.UpdateClient.Tests.Services
{
    public sealed class InstalledManifestStoreTests : IDisposable
    {
        private readonly string _installDirectory;
        private readonly InstalledManifestStore _store;

        public InstalledManifestStoreTests()
        {
            _installDirectory = Path.Combine(
                Path.GetTempPath(),
                "POSCAM.UpdateClient.ManifestStoreTests",
                Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_installDirectory);
            _store = new InstalledManifestStore();
        }

        [Fact]
        public void SaveAndLoad_ValidManifest_RoundTrips()
        {
            var manifest = CreateManifest();

            _store.Save(_installDirectory, manifest);
            var loaded = _store.Load(_installDirectory);

            Assert.NotNull(loaded);
            Assert.Equal("1.2.0", loaded!.Version);
            var file = Assert.Single(loaded.Files);
            Assert.Equal("PcCam.exe", file.Path);
            Assert.Equal(123, file.Size);
        }

        [Fact]
        public void Load_InvalidJson_ReturnsNullForFullPackageFallback()
        {
            var manifestPath = _store.GetManifestPath(_installDirectory);
            Directory.CreateDirectory(Path.GetDirectoryName(manifestPath)!);
            File.WriteAllText(manifestPath, "{ invalid json");

            var loaded = _store.Load(_installDirectory);

            Assert.Null(loaded);
        }

        [Fact]
        public void Load_UnsafeManagedPath_ReturnsNull()
        {
            var manifest = CreateManifest();
            manifest.Files[0].Path = "../outside.dll";
            var manifestPath = _store.GetManifestPath(_installDirectory);
            Directory.CreateDirectory(Path.GetDirectoryName(manifestPath)!);
            File.WriteAllText(
                manifestPath,
                Newtonsoft.Json.JsonConvert.SerializeObject(manifest));

            var loaded = _store.Load(_installDirectory);

            Assert.Null(loaded);
        }

        [Fact]
        public void FullPackageFallbackFlag_CanBeRequestedAndCleared()
        {
            Assert.False(_store.IsFullPackageFallbackRequested(
                _installDirectory));

            _store.RequestFullPackageFallback(
                _installDirectory,
                "IncrementalApplyFailed");

            Assert.True(_store.IsFullPackageFallbackRequested(
                _installDirectory));

            _store.ClearFullPackageFallback(_installDirectory);

            Assert.False(_store.IsFullPackageFallbackRequested(
                _installDirectory));
        }

        private static InstalledManifest CreateManifest()
        {
            return new InstalledManifest
            {
                ProductCode = "PCCAM",
                Architecture = "x86",
                Version = "1.2.0",
                InstalledAtUtc = DateTime.UtcNow,
                Files = new List<InstalledManifestFile>
                {
                    new InstalledManifestFile
                    {
                        Path = "PcCam.exe",
                        Size = 123,
                        Sha256 = new string('A', 64)
                    }
                }
            };
        }

        public void Dispose()
        {
            try
            {
                if (Directory.Exists(_installDirectory))
                {
                    Directory.Delete(_installDirectory, true);
                }
            }
            catch
            {
                // 테스트 정리 실패로 본 테스트 결과를 덮어쓰지 않는다.
            }
        }
    }
}
