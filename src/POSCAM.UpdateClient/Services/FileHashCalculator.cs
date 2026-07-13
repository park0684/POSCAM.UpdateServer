using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace POSCAM.UpdateClient.Services
{
    /// <summary>
    /// 로컬 파일의 SHA-256을 스트림 방식으로 계산한다.
    /// </summary>
    internal sealed class FileHashCalculator
    {
        public string CalculateSha256(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath))
            {
                throw new ArgumentException(
                    "파일 경로가 비어 있습니다.",
                    nameof(filePath));
            }

            using (var stream = new FileStream(
                filePath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                81920,
                FileOptions.SequentialScan))
            using (var sha256 = SHA256.Create())
            {
                var hash = sha256.ComputeHash(stream);
                var builder = new StringBuilder(hash.Length * 2);

                foreach (var value in hash)
                {
                    builder.Append(value.ToString("X2"));
                }

                return builder.ToString();
            }
        }
    }
}
