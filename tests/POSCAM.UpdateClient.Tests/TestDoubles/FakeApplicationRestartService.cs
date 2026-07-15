using System;
using POSCAM.UpdateClient.Services;

namespace POSCAM.UpdateClient.Tests.TestDoubles
{
    internal sealed class FakeApplicationRestartService :
        IApplicationRestartService
    {
        public Exception? ExceptionToThrow { get; set; }

        public Func<int, Exception?>? ExceptionFactory { get; set; }

        public int CallCount { get; private set; }

        public string LastInstallDirectory { get; private set; } = "";

        public string LastApplicationFileName { get; private set; } = "";

        public void Restart(
            string installDirectory,
            string applicationFileName)
        {
            CallCount++;
            LastInstallDirectory = installDirectory;
            LastApplicationFileName = applicationFileName;

            var generatedException = ExceptionFactory?.Invoke(CallCount);

            if (generatedException != null)
            {
                throw generatedException;
            }

            if (ExceptionToThrow != null)
            {
                throw ExceptionToThrow;
            }
        }
    }
}
