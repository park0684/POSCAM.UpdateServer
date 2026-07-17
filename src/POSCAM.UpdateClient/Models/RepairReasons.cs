namespace POSCAM.UpdateClient.Models
{
    /// <summary>
    /// 로컬 파일이 복구 또는 증분 업데이트 대상으로 판정된 이유다.
    /// </summary>
    internal static class RepairReasons
    {
        public const string Missing = "Missing";
        public const string SizeMismatch = "SizeMismatch";
        public const string HashMismatch = "HashMismatch";
        public const string Removed = "Removed";
    }
}
