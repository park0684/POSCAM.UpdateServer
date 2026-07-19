using System;
using System.Diagnostics;
using System.IO;

namespace POSCAM.UpdateClient.Services
{
    internal sealed class ApplicationRestartService : IApplicationRestartService
    {
        private const string SkipUpdateOnceArgument =
            "--skip-update-once";

        private readonly Action<ProcessStartInfo> _processStarter;

        public ApplicationRestartService()
            : this(StartProcess)
        {
        }

        internal ApplicationRestartService(
            Action<ProcessStartInfo> processStarter)
        {
            _processStarter = processStarter
                ?? throw new ArgumentNullException(
                    nameof(processStarter));
        }

        public void Restart(
            string installDirectory,
            string applicationFileName,
            bool skipUpdateOnce)
        {
            if (string.IsNullOrWhiteSpace(installDirectory))
            {
                throw new ArgumentException(
                    "설치 경로가 비어 있습니다.",
                    nameof(installDirectory));
            }

            if (string.IsNullOrWhiteSpace(applicationFileName))
            {
                throw new ArgumentException(
                    "재실행 파일명이 비어 있습니다.",
                    nameof(applicationFileName));
            }

            var installRoot = Path.GetFullPath(installDirectory.Trim());
            var applicationPath = Path.GetFullPath(
                Path.Combine(installRoot, applicationFileName.Trim()));
            var rootPrefix = installRoot.TrimEnd(
                    Path.DirectorySeparatorChar,
                    Path.AltDirectorySeparatorChar)
                + Path.DirectorySeparatorChar;

            if (!applicationPath.StartsWith(
                rootPrefix,
                StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException(
                    "재실행 경로가 설치 루트를 벗어납니다.");
            }

            if (!File.Exists(applicationPath))
            {
                throw new FileNotFoundException(
                    "재실행할 프로그램을 찾을 수 없습니다.",
                    applicationPath);
            }

            _processStarter(new ProcessStartInfo
            {
                FileName = applicationPath,
                WorkingDirectory = installRoot,
                UseShellExecute = true,
                Arguments = skipUpdateOnce
                    ? SkipUpdateOnceArgument
                    : ""
            });
        }

        private static void StartProcess(ProcessStartInfo startInfo)
        {
            var process = Process.Start(startInfo);

            if (process == null)
            {
                throw new IOException(
                    "업데이트 후 프로그램을 재실행하지 못했습니다.");
            }

            process.Dispose();
        }
    }
}
