using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using POSCAM.UpdateClient.Models;

namespace POSCAM.UpdateClient.Services
{
    /// <summary>
    /// startup-check 판정 결과에 따라 파일을 다운로드하고 적용 계획을 저장한다.
    /// 원본 프로그램 파일은 변경하지 않는다.
    /// </summary>
    internal sealed class StartupUpdatePreparationService
    {
        private readonly IUpdateFileDownloadService _downloadService;
        private readonly UpdateWorkPathService _workPathService;
        private readonly UpdateApplyPlanStore _planStore;

        public StartupUpdatePreparationService(
            IUpdateFileDownloadService downloadService,
            UpdateWorkPathService workPathService,
            UpdateApplyPlanStore planStore)
        {
            _downloadService = downloadService
                ?? throw new ArgumentNullException(nameof(downloadService));
            _workPathService = workPathService
                ?? throw new ArgumentNullException(nameof(workPathService));
            _planStore = planStore
                ?? throw new ArgumentNullException(nameof(planStore));
        }

        public async Task<int> PrepareAsync(
            StartupCheckOptions options,
            StartupCheckResult checkResult,
            CancellationToken cancellationToken)
        {
            if (options == null)
            {
                throw new ArgumentNullException(nameof(options));
            }

            if (checkResult == null)
            {
                throw new ArgumentNullException(nameof(checkResult));
            }

            UpdateWorkPaths paths;

            try
            {
                paths = _workPathService.Create(
                    options.InstallDirectory);
                _planStore.Delete(paths.ActivePlanPath);
            }
            catch (Exception exception)
                when (exception is ArgumentException
                    || exception is InvalidDataException
                    || exception is NotSupportedException
                    || exception is PathTooLongException)
            {
                return UpdateClientExitCodes.VerificationFailed;
            }
            catch (Exception exception)
                when (exception is IOException
                    || exception is UnauthorizedAccessException)
            {
                return UpdateClientExitCodes.DownloadFailed;
            }

            if (checkResult.ExitCode
                != UpdateClientExitCodes.ApplyRequired)
            {
                return checkResult.ExitCode;
            }

            try
            {
                var applicationFileName = _workPathService
                    .ValidateFileName(options.ApplicationFileName);

                Directory.CreateDirectory(paths.JobDirectory);

                var plan = new UpdateApplyPlan
                {
                    JobId = paths.JobId,
                    InstallDirectory = paths.InstallDirectory,
                    ApplicationFileName = applicationFileName,
                    CreatedAtUtc = DateTime.UtcNow,
                    LatestVersion = checkResult.UpdateResponse?.LatestVersion
                };

                if (checkResult.FullPackageUpdateRequired)
                {
                    await PrepareFullPackageAsync(
                        paths,
                        checkResult,
                        plan,
                        cancellationToken)
                        .ConfigureAwait(false);
                }
                else
                {
                    await PrepareFileRepairAsync(
                        paths,
                        checkResult,
                        plan,
                        cancellationToken)
                        .ConfigureAwait(false);
                }

                _planStore.Save(paths.ActivePlanPath, plan);
                return UpdateClientExitCodes.ApplyRequired;
            }
            catch (OperationCanceledException)
                when (cancellationToken.IsCancellationRequested)
            {
                CleanupFailedJob(paths);
                throw;
            }
            catch (Exception exception)
                when (exception is ArgumentException
                    || exception is InvalidDataException
                    || exception is NotSupportedException
                    || exception is PathTooLongException)
            {
                CleanupFailedJob(paths);
                return UpdateClientExitCodes.VerificationFailed;
            }
            catch (Exception exception)
                when (exception is UpdateDownloadException
                    || exception is IOException
                    || exception is UnauthorizedAccessException)
            {
                CleanupFailedJob(paths);
                return UpdateClientExitCodes.DownloadFailed;
            }
        }

        private async Task PrepareFullPackageAsync(
            UpdateWorkPaths paths,
            StartupCheckResult checkResult,
            UpdateApplyPlan plan,
            CancellationToken cancellationToken)
        {
            var response = checkResult.UpdateResponse;

            if (response == null
                || string.IsNullOrWhiteSpace(response.PackageUrl)
                || string.IsNullOrWhiteSpace(response.FileName)
                || !response.FileSize.HasValue
                || response.FileSize.Value < 0
                || string.IsNullOrWhiteSpace(response.Sha256))
            {
                throw new InvalidDataException(
                    "Full Package 업데이트 정보가 올바르지 않습니다.");
            }

            var packageFileName = _workPathService
                .ValidateFileName(response.FileName);
            var relativeDownloadPath = "package/" + packageFileName;
            var destinationPath = _workPathService.ResolveJobFilePath(
                paths.JobDirectory,
                relativeDownloadPath);
            var expectedSha256 = response.Sha256.Trim().ToUpperInvariant();

            var downloadedPath = await _downloadService
                .DownloadAndVerifyAsync(
                    new UpdateFileDownloadRequest
                    {
                        DownloadUrl = response.PackageUrl,
                        DestinationPath = destinationPath,
                        ExpectedSize = response.FileSize.Value,
                        ExpectedSha256 = expectedSha256
                    },
                    cancellationToken)
                .ConfigureAwait(false);

            plan.Mode = UpdateApplyModes.FullPackage;
            plan.PackageType = response.PackageType;
            plan.PackageFileName = packageFileName;
            plan.PackagePath = downloadedPath;
            plan.PackageSize = response.FileSize.Value;
            plan.PackageSha256 = expectedSha256;
        }

        private async Task PrepareFileRepairAsync(
            UpdateWorkPaths paths,
            StartupCheckResult checkResult,
            UpdateApplyPlan plan,
            CancellationToken cancellationToken)
        {
            var repairTargets = checkResult.RepairPlan?.Targets;

            if (repairTargets == null || repairTargets.Count == 0)
            {
                throw new InvalidDataException(
                    "파일 복구 대상이 없습니다.");
            }

            plan.Mode = UpdateApplyModes.FileRepair;

            foreach (var target in repairTargets)
            {
                if (target == null)
                {
                    throw new InvalidDataException(
                        "파일 복구 대상이 null입니다.");
                }

                var relativeDownloadPath = "files/"
                    + target.RelativePath.Replace('\\', '/');
                var destinationPath = _workPathService.ResolveJobFilePath(
                    paths.JobDirectory,
                    relativeDownloadPath);
                var expectedSha256 = target.ExpectedSha256
                    .Trim()
                    .ToUpperInvariant();

                var downloadedPath = await _downloadService
                    .DownloadAndVerifyAsync(
                        new UpdateFileDownloadRequest
                        {
                            DownloadUrl = target.DownloadUrl,
                            DestinationPath = destinationPath,
                            ExpectedSize = target.ExpectedSize,
                            ExpectedSha256 = expectedSha256
                        },
                        cancellationToken)
                    .ConfigureAwait(false);

                plan.Targets.Add(new UpdateApplyTarget
                {
                    RelativePath = target.RelativePath,
                    DownloadedPath = downloadedPath,
                    ExpectedSize = target.ExpectedSize,
                    ExpectedSha256 = expectedSha256,
                    Reason = target.Reason
                });
            }
        }

        private void CleanupFailedJob(UpdateWorkPaths paths)
        {
            try
            {
                _planStore.Delete(paths.ActivePlanPath);
            }
            catch
            {
                // 실패 정리 중 예외가 원래 종료 코드를 덮어쓰지 않도록 한다.
            }

            try
            {
                if (Directory.Exists(paths.JobDirectory))
                {
                    Directory.Delete(paths.JobDirectory, true);
                }
            }
            catch
            {
                // 실패 작업 폴더 정리는 후속 로그/정리 단계에서 재시도한다.
            }
        }
    }
}
