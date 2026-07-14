namespace POSCAM.UpdateClient.Services
{
    internal interface IUpdateWorkerLauncherService
    {
        void Launch(
            string installDirectory,
            string jobId,
            string planPath,
            string restartFileName,
            int waitTimeoutSeconds,
            int parentProcessId);
    }
}
