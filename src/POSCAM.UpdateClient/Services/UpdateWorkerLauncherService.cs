using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using Newtonsoft.Json;

namespace POSCAM.UpdateClient.Services
{
    /// <summary>
    /// 현재 UpdateClient 실행 파일과 필수 런타임 파일을 작업 폴더로 복사한 뒤
    /// 원본 UpdateClient가 종료된 후 전체 패키지를 적용할 worker를 실행한다.
    /// </summary>
    internal sealed class UpdateWorkerLauncherService :
        IUpdateWorkerLauncherService
    {
        private readonly UpdateWorkPathService _pathService;
        private readonly string _sourceExecutablePath;
        private readonly string _jsonAssemblyPath;
        private readonly Action<ProcessStartInfo> _processStarter;

        public UpdateWorkerLauncherService(
            UpdateWorkPathService pathService)
            : this(
                pathService,
                Assembly.GetExecutingAssembly().Location,
                typeof(JsonConvert).Assembly.Location,
                StartProcess)
        {
        }

        internal UpdateWorkerLauncherService(
            UpdateWorkPathService pathService,
            string sourceExecutablePath,
            string jsonAssemblyPath,
            Action<ProcessStartInfo> processStarter)
        {
            _pathService = pathService
                ?? throw new ArgumentNullException(nameof(pathService));
            _sourceExecutablePath = sourceExecutablePath
                ?? throw new ArgumentNullException(nameof(sourceExecutablePath));
            _jsonAssemblyPath = jsonAssemblyPath
                ?? throw new ArgumentNullException(nameof(jsonAssemblyPath));
            _processStarter = processStarter
                ?? throw new ArgumentNullException(nameof(processStarter));
        }

        public void Launch(
            string installDirectory,
            string jobId,
            string planPath,
            string restartFileName,
            int waitTimeoutSeconds,
            int parentProcessId)
        {
            if (string.IsNullOrWhiteSpace(planPath))
            {
                throw new ArgumentException(
                    "적용 계획 경로가 비어 있습니다.",
                    nameof(planPath));
            }

            if (waitTimeoutSeconds <= 0 || waitTimeoutSeconds > 600)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(waitTimeoutSeconds));
            }

            if (parentProcessId <= 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(parentProcessId));
            }

            var workerDirectory = _pathService.GetWorkerDirectory(
                installDirectory,
                jobId);
            var normalizedPlanPath = Path.GetFullPath(planPath.Trim());
            var normalizedRestartFileName = _pathService.ValidateFileName(
                restartFileName);
            var sourceExecutablePath = Path.GetFullPath(
                _sourceExecutablePath.Trim());
            var jsonAssemblyPath = Path.GetFullPath(
                _jsonAssemblyPath.Trim());

            if (!File.Exists(sourceExecutablePath))
            {
                throw new FileNotFoundException(
                    "복사할 UpdateClient 실행 파일을 찾을 수 없습니다.",
                    sourceExecutablePath);
            }

            if (!File.Exists(jsonAssemblyPath))
            {
                throw new FileNotFoundException(
                    "UpdateClient JSON 런타임 파일을 찾을 수 없습니다.",
                    jsonAssemblyPath);
            }

            DeleteDirectoryIfExists(workerDirectory);
            Directory.CreateDirectory(workerDirectory);

            try
            {
                var workerExecutablePath = CopyRequiredFile(
                    sourceExecutablePath,
                    workerDirectory);
                CopyRequiredFile(jsonAssemblyPath, workerDirectory);
                CopyOptionalFile(
                    sourceExecutablePath + ".config",
                    workerDirectory);

                var startInfo = new ProcessStartInfo
                {
                    FileName = workerExecutablePath,
                    WorkingDirectory = workerDirectory,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    Arguments = BuildArguments(
                        normalizedPlanPath,
                        normalizedRestartFileName,
                        waitTimeoutSeconds,
                        parentProcessId)
                };

                _processStarter(startInfo);
            }
            catch
            {
                DeleteDirectoryIfExists(workerDirectory);
                throw;
            }
        }

        private static string BuildArguments(
            string planPath,
            string restartFileName,
            int waitTimeoutSeconds,
            int parentProcessId)
        {
            return "apply-worker"
                + " --plan " + Quote(planPath)
                + " --wait-process-id " + parentProcessId
                + " --restart " + Quote(restartFileName)
                + " --wait-timeout-seconds " + waitTimeoutSeconds;
        }

        private static string CopyRequiredFile(
            string sourcePath,
            string destinationDirectory)
        {
            var fileName = Path.GetFileName(sourcePath);

            if (string.IsNullOrWhiteSpace(fileName))
            {
                throw new InvalidDataException(
                    "worker 복사 파일명을 확인할 수 없습니다.");
            }

            var destinationPath = Path.Combine(
                destinationDirectory,
                fileName);
            File.Copy(sourcePath, destinationPath, true);
            return destinationPath;
        }

        private static void CopyOptionalFile(
            string sourcePath,
            string destinationDirectory)
        {
            if (File.Exists(sourcePath))
            {
                CopyRequiredFile(sourcePath, destinationDirectory);
            }
        }

        private static string Quote(string value)
        {
            return "\"" + value.Replace("\"", "\\\"") + "\"";
        }

        private static void StartProcess(ProcessStartInfo startInfo)
        {
            var process = Process.Start(startInfo);

            if (process == null)
            {
                throw new IOException(
                    "Full Package 적용 worker를 실행하지 못했습니다.");
            }

            process.Dispose();
        }

        private static void DeleteDirectoryIfExists(string path)
        {
            if (Directory.Exists(path))
            {
                Directory.Delete(path, true);
            }
        }
    }
}
