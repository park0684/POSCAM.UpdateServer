using System;
using System.IO;
using System.Text;
using Newtonsoft.Json;
using POSCAM.UpdateClient.Models;

namespace POSCAM.UpdateClient.Services
{
    /// <summary>
    /// 적용 계획을 부분 파일이 노출되지 않도록 임시 파일을 거쳐 저장하고 읽는다.
    /// </summary>
    internal sealed class UpdateApplyPlanStore
    {
        private static readonly Encoding Utf8WithoutBom =
            new UTF8Encoding(false);

        public UpdateApplyPlan Load(string planPath)
        {
            if (string.IsNullOrWhiteSpace(planPath))
            {
                throw new ArgumentException(
                    "적용 계획 경로가 비어 있습니다.",
                    nameof(planPath));
            }

            var fullPlanPath = Path.GetFullPath(planPath.Trim());

            if (!File.Exists(fullPlanPath))
            {
                throw new FileNotFoundException(
                    "적용 계획 파일을 찾을 수 없습니다.",
                    fullPlanPath);
            }

            UpdateApplyPlan? plan;

            try
            {
                var json = File.ReadAllText(fullPlanPath, Encoding.UTF8);
                plan = JsonConvert.DeserializeObject<UpdateApplyPlan>(json);
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

        public string Save(
            string planPath,
            UpdateApplyPlan plan)
        {
            if (string.IsNullOrWhiteSpace(planPath))
            {
                throw new ArgumentException(
                    "적용 계획 경로가 비어 있습니다.",
                    nameof(planPath));
            }

            if (plan == null)
            {
                throw new ArgumentNullException(nameof(plan));
            }

            var fullPlanPath = Path.GetFullPath(planPath.Trim());
            var stateDirectory = Path.GetDirectoryName(fullPlanPath);

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

        public void Delete(string planPath)
        {
            if (string.IsNullOrWhiteSpace(planPath))
            {
                return;
            }

            var fullPlanPath = Path.GetFullPath(planPath.Trim());
            DeleteIfExists(fullPlanPath);
            DeleteIfExists(fullPlanPath + ".tmp");
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
