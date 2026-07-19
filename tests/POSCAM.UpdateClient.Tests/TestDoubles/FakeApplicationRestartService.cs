using System;
using System.Collections.Generic;
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

        public bool LastSkipUpdateOnce { get; private set; }

        public List<bool> SkipUpdateOnceValues { get; }
            = new List<bool>();

        public void Restart(
            string installDirectory,
            string applicationFileName,
            bool skipUpdateOnce)
        {
            CallCount++;
            LastInstallDirectory = installDirectory;
            LastApplicationFileName = applicationFileName;
            LastSkipUpdateOnce = skipUpdateOnce;
            SkipUpdateOnceValues.Add(skipUpdateOnce);

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
