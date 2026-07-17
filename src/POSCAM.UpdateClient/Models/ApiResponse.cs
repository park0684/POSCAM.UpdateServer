using Newtonsoft.Json;

namespace POSCAM.UpdateClient.Models
{
    /// <summary>
    /// UpdateServer의 공통 JSON 응답 형식이다.
    /// </summary>
    internal sealed class ApiResponse<T>
    {
        [JsonProperty("success")]
        public bool Success { get; set; }

        [JsonProperty("message")]
        public string Message { get; set; } = "";

        [JsonProperty("errorCode")]
        public int ErrorCode { get; set; }

        [JsonProperty("data")]
        public T? Data { get; set; }
    }
}
