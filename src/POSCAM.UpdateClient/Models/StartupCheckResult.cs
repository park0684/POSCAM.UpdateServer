namespace POSCAM.UpdateClient.Models
{
    /// <summary>
    /// startup-check 수행 결과다.
    /// </summary>
    internal sealed class StartupCheckResult
    {
        public int ExitCode { get; set; }

        public bool FullPackageUpdateRequired { get; set; }

        public UpdateCheckResponse? UpdateResponse { get; set; }

        public RepairPlan RepairPlan { get; set; } = new RepairPlan();
    }
}
