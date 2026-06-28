# B09 운영 기능 구현 보고

## 작업 결과

- 상태: InProgress
- 구현: 완료
- 로컬 Release 빌드·전체 테스트: 확인 필요
- Docker build: 로컬 Docker 환경에서 확인 필요

## 변경 파일

### Rate Limit·CORS·Forwarded Headers

- `Infrastructure/Operations/OperationalPolicyNames.cs`: CORS·Rate 정책 이름
- `Infrastructure/Operations/OperationalConfiguration.cs`: Origin·프록시·AuthServer 설정 검증
- `Infrastructure/Operations/RateLimitResponseWriter.cs`: `429 / 9004` 공통 응답
- `Options/TrustedProxyOptions.cs`: 신뢰 프록시 IP와 ForwardLimit
- `Controllers/UpdatesController.cs`: 공개 Update Check Rate 정책 적용
- `Program.cs`: CORS, Rate Limiter, Forwarded Headers와 Middleware 순서
- `appsettings.json`: Forwarded Headers 기본 구조

### Health

- `Infrastructure/Health/DatabaseReadyHealthCheck.cs`: DB 연결과 `SELECT 1`
- `Infrastructure/Health/StorageReadyHealthCheck.cs`: `.staging` 쓰기·삭제 검사
- `Infrastructure/Health/HealthResponseWriter.cs`: Secret 없는 JSON Health 응답
- `Program.cs`: `/health/live`, `/health/ready`

### Request ID·로그

- `Infrastructure/Middleware/RequestIdMiddleware.cs`: Request ID 문자·길이 검증
- `Infrastructure/Middleware/RequestLoggingMiddleware.cs`: 구조화 요청 완료 로그
- `Infrastructure/Middleware/GlobalExceptionHandlingMiddleware.cs`: 오류 응답 Request ID 보존
- `Infrastructure/Middleware/UpdateManagementAuthorizationMiddleware.cs`: 인증 실패 Request ID 보존

### Docker·배포

- `Dockerfile`: .NET 8 multi-stage, non-root `app` 사용자
- `.dockerignore`: Build context 제외 목록
- `deploy/docker-compose.update.example.yml`: read-only root, tmpfs, trusted proxy, 명시 태그
- `deploy/update-server.env.example`: 의도적으로 유효하지 않은 Secret·Proxy placeholder
- `deploy/nginx-update.example.conf`: API·Health Forwarded Headers와 Request ID
- `deploy/deployment-checklist.md`: 운영 검증 체크리스트
- `docs/deployment-policy.md`: B09 운영 정책
- `tests/POSCAM.UpdateServer.Tests.csproj`: ASP.NET Core 통합 테스트 지원

## Middleware 순서

```text
Forwarded Headers
→ Request ID
→ 구조화 요청 로그
→ 전역 예외 처리
→ 제한 CORS
→ Rate Limiter
→ 관리자 AuthServer 권한 확인
→ Controller
```

이 순서의 목적:

- Rate Limit가 Nginx가 전달한 실제 Client IP를 사용한다.
- Request ID가 정상·오류·감사 로그 응답에 동일하게 연결된다.
- CORS OPTIONS preflight가 관리자 인증 호출 전에 종료된다.
- 요청 로그는 전역 예외 처리 이후 확정된 HTTP 상태를 기록한다.

## Rate Limit

대상:

```text
POST /api/v1/updates/check
```

기본 정책:

- Forwarded Header 처리 후 Remote IP 기준
- IP별 60회
- 60초 고정 창
- Queue 없음
- 초과 시 HTTP 429
- `RateLimitExceeded = 9004`
- `Cache-Control: no-store`
- 가능할 경우 `Retry-After` 반환

관리자 API와 Health API에는 해당 Rate 정책을 적용하지 않는다.

## CORS

- `Cors:AdminWebOrigins`에 등록된 정확한 Origin만 허용
- 와일드카드 Origin 금지
- 경로·Query·Fragment가 포함된 Origin 금지
- 허용 Method: GET, POST, PUT, DELETE, OPTIONS
- 허용 Header: Authorization, Content-Type, X-Request-ID
- 노출 Header: X-Request-ID
- Credentials 미허용
- 운영 환경에서 Origin 목록이 없으면 시작 검증 실패

## Forwarded Headers

- 처리 Header: X-Forwarded-For, X-Forwarded-Proto
- 기본 ForwardLimit: 1
- 설정한 단일 IP만 Known Proxy로 추가
- `0.0.0.0`, `::`, Broadcast, 잘못된 IP 금지
- 운영 환경에서 Known Proxy가 없으면 시작 검증 실패
- 전체 Docker Network 또는 모든 프록시 신뢰 설정을 사용하지 않음

## Health

### Live

```text
GET /health/live
```

프로세스 자체만 확인한다. DB, Storage, AuthServer를 포함하지 않는다.

### Ready

```text
GET /health/ready
```

다음만 확인한다.

- `poscam_update` DB 연결과 `SELECT 1`
- Artifact Storage `.staging` 쓰기·삭제 가능 여부

AuthServer는 Ready 조건에서 제외한다. 따라서 AuthServer 장애가 공개 Update Check와 UpdateServer Ready 상태를 직접 중단하지 않는다.

Health 응답에는 다음을 노출하지 않는다.

- 연결 문자열
- DB 비밀번호
- 내부 서비스 키
- 물리 Storage 경로
- 원본 예외 메시지·Stack Trace

## Request ID·로그

허용 Request ID 문자:

```text
A-Z a-z 0-9 - _ . :
```

- 최대 100자
- 공백, Slash, Backslash, CR/LF 등은 거부하고 새 32자 GUID 생성
- 응답 `X-Request-ID`에 동일한 값 반환
- 전역 오류와 관리자 인증 실패 응답에서도 보존

요청 완료 로그 항목:

- Method
- Path
- Status Code
- 처리시간
- Request ID
- Remote IP Scope

다음 값은 읽거나 기록하지 않는다.

- Authorization
- Cookie
- X-POSCAM-Service-Key
- Request Body

## 운영 Options 실패 정책

운영 환경에서는 다음 설정이 없거나 잘못되면 Secret 값을 출력하지 않고 시작 검증에 실패한다.

- 유효한 AuthServer BaseUrl
- 32자 이상의 InternalServiceKey
- 최소 1개 AdminWeb Origin
- 최소 1개 신뢰 Proxy IP
- 1~30초 AuthServer Timeout
- 양수 Rate Limit 값
- 유효한 UpdateStorage 설정

`deploy/update-server.env.example`의 `CHANGE_ME`는 의도적으로 검증을 통과하지 못하도록 유지한다.

## Docker

- Build image: `mcr.microsoft.com/dotnet/sdk:8.0-bookworm-slim`
- Runtime image: `mcr.microsoft.com/dotnet/aspnet:8.0-bookworm-slim`
- 실행 사용자: non-root `app`
- 내부 포트: 8080
- Root filesystem: read-only
- `/tmp`: tmpfs
- 쓰기 Volume: `/app/update-storage`
- `DOTNET_EnableDiagnostics=0`
- 자동 Migration 없음
- `latest` 태그 사용 금지

## 테스트

추가 테스트 범위:

- 허용 Origin 관리자 preflight 성공
- 허용되지 않은 Origin CORS Header 미반환
- CORS가 관리자 인증보다 먼저 실행됨
- Update Check 고정 창 Rate Limit와 `429 / 9004`
- 안전·위험 Request ID
- Live에 DB·Storage·AuthServer 미포함
- Ready에 DB·Storage 포함, AuthServer 제외
- 와일드카드·경로·비HTTP Origin 차단
- 신뢰 Proxy IP와 ForwardLimit 검증
- 운영 AuthServer 서비스 키 길이 검증
- 요청 로그의 Authorization·Cookie·Service Key 제외
- Storage Ready 쓰기·삭제 검사
- 전역 오류 응답 Request ID 보존

## 검증 명령

```powershell
cd D:\_work\POSCAM.UpdateServer
git switch feature/initial-update-server
git pull

dotnet restore POSCAM.UpdateServer.sln
dotnet build POSCAM.UpdateServer.sln -c Release
dotnet test POSCAM.UpdateServer.sln -c Release --no-build
```

예상 전체 테스트 수는 약 290개이다. 정확한 개수보다 오류·경고·실패·건너뜀 여부가 우선이다.

Docker가 설치된 환경에서는 추가 실행:

```powershell
docker build -t poscam-update-server:b09-test .
```

검증 기준:

- Restore 성공
- API Release 빌드 성공
- Tests Release 빌드 성공
- 컴파일 오류 0
- 경고 0
- 테스트 실패 0
- 건너뜀 0
- 가능하면 Docker build 성공
- Docker 실행 사용자가 root가 아님

## 남은 문제

- 컴파일 오류: 로컬 검증 필요
- 실제 동작 오류: 실제 MariaDB·Nginx·Docker Volume 연동은 B10 최종 검증 필요
- 불필요한 중복: 정적 검토 완료
- 다음 단계 선행조건: B09 Release 빌드·전체 테스트 성공

## 정책 이탈 여부

- 없음
