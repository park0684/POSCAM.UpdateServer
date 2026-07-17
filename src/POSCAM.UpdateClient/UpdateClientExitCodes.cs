namespace POSCAM.UpdateClient
{
    internal static class UpdateClientExitCodes
    {
        public const int Success = 0;
        public const int UnknownCommand = 1;
        public const int NotImplemented = 2;
        public const int ApplyRequired = 10;
        public const int VerificationFailed = 20;
        public const int UpdateCheckFailed = 30;
        public const int DownloadFailed = 40;
        public const int ApplyFailed = 50;
    }
}
