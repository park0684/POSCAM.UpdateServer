namespace POSCAM.UpdateClient.Services
{
    internal interface IApplicationRestartService
    {
        void Restart(
            string installDirectory,
            string applicationFileName,
            bool skipUpdateOnce);
    }
}
