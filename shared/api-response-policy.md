# API 응답 정책

```csharp
public sealed class ApiResponse<T>
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public int ErrorCode { get; set; }
    public T? Data { get; set; }
}
```

| HTTP | 의미 |
|---:|---|
| 200 | 정상 및 업데이트 없음 |
| 201 | 생성 |
| 400 | 요청값 오류 |
| 401 | 인증 실패 |
| 403 | 권한 없음 |
| 404 | 대상 없음 |
| 409 | 중복·상태 충돌 |
| 413 | 크기 초과 |
| 415 | 패키지 형식 오류 |
| 429 | Rate Limit |
| 500 | 서버 오류 |
| 503 | 관리자 인증 의존성 장애 |

정상 사유: UPDATE_AVAILABLE, MANDATORY_RELEASE, FORCE_UPDATE_BELOW_VERSION, ALREADY_LATEST, CLIENT_VERSION_AHEAD, NO_AVAILABLE_RELEASE, NO_COMPATIBLE_ARTIFACT.
