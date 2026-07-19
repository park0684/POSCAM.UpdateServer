using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading;

namespace POSCAM.UpdateClient.Services
{
    /// <summary>
    /// repair-plan.json 읽기·저장·조건부 삭제와 정리 작업을
    /// UpdateClient 프로세스 사이에서 직렬화한다.
    /// </summary>
    internal static class UpdatePlanLock
    {
        private static readonly TimeSpan LockTimeout =
            TimeSpan.FromSeconds(30);

        public static void Execute(
            string planPath,
            Action action)
        {
            if (action == null)
            {
                throw new ArgumentNullException(nameof(action));
            }

            Execute<object?>(
                planPath,
                () =>
                {
                    action();
                    return null;
                });
        }

        public static T Execute<T>(
            string planPath,
            Func<T> action)
        {
            if (string.IsNullOrWhiteSpace(planPath))
            {
                throw new ArgumentException(
                    "적용 계획 경로가 비어 있습니다.",
                    nameof(planPath));
            }

            if (action == null)
            {
                throw new ArgumentNullException(nameof(action));
            }

            var fullPlanPath = Path.GetFullPath(
                planPath.Trim());
            var mutexName = CreateMutexName(fullPlanPath);

            using (var mutex = new Mutex(false, mutexName))
            {
                var acquired = false;

                try
                {
                    try
                    {
                        acquired = mutex.WaitOne(LockTimeout);
                    }
                    catch (AbandonedMutexException)
                    {
                        acquired = true;
                    }

                    if (!acquired)
                    {
                        throw new IOException(
                            "업데이트 적용 계획 잠금을 획득하지 못했습니다.");
                    }

                    return action();
                }
                finally
                {
                    if (acquired)
                    {
                        mutex.ReleaseMutex();
                    }
                }
            }
        }

        private static string CreateMutexName(string fullPlanPath)
        {
            var normalizedPath = fullPlanPath.ToUpperInvariant();
            byte[] hash;

            using (var sha256 = SHA256.Create())
            {
                hash = sha256.ComputeHash(
                    Encoding.UTF8.GetBytes(normalizedPath));
            }

            var builder = new StringBuilder(hash.Length * 2);

            foreach (var value in hash)
            {
                builder.Append(value.ToString("X2"));
            }

            return @"Local\POSCAM.UpdateClient.Plan." + builder;
        }
    }
}
