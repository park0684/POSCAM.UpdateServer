using System;
using System.IO;
using System.Text;
using Newtonsoft.Json;
using POSCAM.UpdateClient.Models;

namespace POSCAM.UpdateClient.Services
{
    /// <summary>
    /// 적용 계획을 부분 파일이 노출되지 않도록 임시 파일을 거쳐 저장하고 읽는다.
    /// 동일 설치 경로의 여러 UpdateClient 프로세스가 계획을 동시에 변경하지 않도록
    /// 프로세스 간 잠금을 사용한다.
    /// </summary>
    internal sealed class UpdateApplyPlanStore
    {
        private static readonly Encoding Utf8WithoutBom =
            new UTF8Encoding(false);

        public UpdateApplyPlan Load(string planPath)
        {
            var fullPlanPath = NormalizePlanPath(planPath);

            return UpdatePlanLock.Execute(
                fullPlanPath,
                () => LoadCore(fullPlanPath));
        }

        public string Save(
            string planPath,
            UpdateApplyPlan plan)
        {
            if (plan == null)
            {
                throw new ArgumentNullException(nameof(plan));
            }

            var fullPlanPath = NormalizePlanPath(planPath);

            return UpdatePlanLock.Execute(
                fullPlanPath,
                () => SaveCore(fullPlanPath, plan));
        }

        public void Delete(string planPath)
        {
            if (string.IsNullOrWhiteSpace(planPath))
            {
                return;
            }

            var fullPlanPath = Path.GetFullPath(
                planPath.Trim());

            UpdatePlanLock.Execute(
                fullPlanPath,
                () => DeleteCore(fullPlanPath));
        }

        public bool DeleteIfJobMatches(
            string planPath,
            string expectedJobId)
        {
            if (string.IsNullOrWhiteSpace(expectedJobId))
            {
                throw new ArgumentException(
                    "비교할 업데이트 JobId가 비어 있습니다.",
                    nameof(expectedJobId));
            }

            var fullPlanPath = NormalizePlanPath(planPath);
            var normalizedExpectedJobId = expectedJobId.Trim();

            return UpdatePlanLock.Execute(
                fullPlanPath,
                () =>
                {
                    if (!File.Exists(fullPlanPath))
                    {
                        return false;
                    }

                    var plan = LoadCore(fullPlanPath);

                    if (!string.Equals(
                        plan.JobId,
                        normalizedExpectedJobId,
                        StringComparison.OrdinalIgnoreCase))
                    {
                        return false;
                    }

                    DeleteCore(fullPlanPath);
                    return true;
                });
        }

        private static UpdateApplyPlan LoadCore(string fullPlanPath)
        {
            if (!File.Exists(fullPlanPath))
            {
                throw new FileNotFoundException(
                    "적용 계획 파일을 찾을 수 없습니다.",
                    fullPlanPath);
            }

            UpdateApplyPlan? plan;

            try
            {
                var json = File.ReadAllText(
                    fullPlanPath,
                    Encoding.UTF8);
                plan = JsonConvert.DeserializeObject<
                    UpdateApplyPlan>(json);
            }
            catch (JsonException exception)
            {
                throw new InvalidDataException(
                    "적용 계획 JSON을 해석할 수 없습니다.",
                    exception);
            }

            if (plan == null)
            {
                throw new InvalidDataException(
                    "적용 계획 내용이 비어 있습니다.");
            }

            if (plan.Targets == null)
            {
                plan.Targets = new System.Collections.Generic.List<
                    UpdateApplyTarget>();
            }

            return plan;
        }

        private static string SaveCore(
            string fullPlanPath,
            UpdateApplyPlan plan)
        {
            var stateDirectory = Path.GetDirectoryName(
                fullPlanPath);

            if (string.IsNullOrWhiteSpace(stateDirectory))
            {
                throw new InvalidDataException(
                    "적용 계획 디렉터리를 확인할 수 없습니다.");
            }

            Directory.CreateDirectory(stateDirectory);

            var temporaryPath = fullPlanPath + ".tmp";
            DeleteIfExists(temporaryPath);

            try
            {
                var json = JsonConvert.SerializeObject(
                    plan,
                    Formatting.Indented);

                File.WriteAllText(
                    temporaryPath,
                    json,
                    Utf8WithoutBom);
                DeleteIfExists(fullPlanPath);
                File.Move(temporaryPath, fullPlanPath);

                return fullPlanPath;
            }
            catch
            {
                DeleteIfExists(temporaryPath);
                throw;
            }
        }

        private static void DeleteCore(string fullPlanPath)
        {
            DeleteIfExists(fullPlanPath);
            DeleteIfExists(fullPlanPath + ".tmp");
        }

        private static string NormalizePlanPath(string planPath)
        {
            if (string.IsNullOrWhiteSpace(planPath))
            {
                throw new ArgumentException(
                    "적용 계획 경로가 비어 있습니다.",
                    nameof(planPath));
            }

            return Path.GetFullPath(planPath.Trim());
        }

        private static void DeleteIfExists(string path)
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }
}
