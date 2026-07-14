using POSCAM.UpdateClient.Services;

namespace POSCAM.UpdateClient.Tests.TestDoubles
{
    internal sealed class FakeUpdateWorkerLauncherService :
        IUpdateWorkerLauncherService
    {
        public int CallCount { get; private set; }

        public string? InstallDirectory { get; private set; }

        public string? JobId { get; private set; }

        public string? PlanPath { get; private set; }

        public string? RestartFileName { get; private set; }

        public int WaitTimeoutSeconds { get; private set; }

        public int ParentProcessId { get; private set; }

        public System.Exception? ExceptionToThrow { get; set; }

        public void Launch(
            string installDirectory,
            string jobId,
            string planPath,
            string restartFileName,
            int waitTimeoutSeconds,
            int parentProcessId)
        {
            if (ExceptionToThrow != null)
            {
                throw ExceptionToThrow;
            }

            CallCount++;
            InstallDirectory = installDirectory;
            JobId = jobId;
            PlanPath = planPath;
            RestartFileName = restartFileName;
            WaitTimeoutSeconds = waitTimeoutSeconds;
            ParentProcessId = parentProcessId;
        }
    }
}
