# 배포 정책

- 컨테이너: poscam-update-api
- 내부 8080
- 호스트 127.0.0.1:5002
- 네트워크 poscam-internal
- 도메인 https://update.poscam.co.kr

```text
IIS → Ubuntu Nginx
  ├─ /api/*      → 127.0.0.1:5002
  └─ /packages/* → /var/poscam/update-storage/packages
```

`/api/internal/*`은 외부에서 404로 차단한다.

## 운영 Middleware 순서

```text
Forwarded Headers
→ Request ID
→ 구조화 요청 로그
→ 전역 예외 처리
→ 제한 CORS
→ Update Check Rate Limit
→ 관리자 AuthServer 권한 확인
→ Controller
```

- Forwarded Headers는 운영자가 확인한 정확한 프록시 IP만 신뢰한다.
- 현재 배포 경로는 IIS와 Ubuntu Nginx의 2단계이므로 두 IP를 모두 등록하고 `ForwardLimit=2`를 사용한다.
- Nginx는 IIS가 전달한 기존 X-Forwarded-For를 보존해 실제 Client IP 체인을 UpdateServer에 전달한다.
- `0.0.0.0`, `::`, 전체 네트워크 또는 광범위한 CIDR 신뢰는 사용하지 않는다.
- Request 로그는 Method, Path, Status, 처리시간, Request ID, Remote IP만 포함한다.
- Authorization, Cookie, X-POSCAM-Service-Key 값은 기록하지 않는다.

## CORS

- `Cors:AdminWebOrigins`에 등록한 정확한 Origin만 허용한다.
- `AllowAnyOrigin`을 사용하지 않는다.
- 운영 Origin은 HTTPS만 허용한다.
- 허용 Method: GET, POST, PUT, DELETE, OPTIONS
- 허용 Header: Authorization, Content-Type, X-Request-ID
- 노출 Header: X-Request-ID
- Cookie 기반 인증을 사용하지 않으므로 Credentials는 허용하지 않는다.
- CORS preflight는 관리자 권한 확인보다 먼저 처리한다.

## Rate Limit

- 대상: `POST /api/v1/updates/check`
- 기준: 신뢰된 IIS·Nginx Forwarded Headers를 적용한 뒤의 실제 Client IP
- 기본값: IP별 60회/60초
- Queue: 없음
- 초과 응답: HTTP 429, ErrorCode 9004
- 관리자 API와 Health API에는 이 정책을 적용하지 않는다.

## Health

- `/health/live`: 프로세스 자체만 확인
- `/health/ready`: `poscam_update` DB와 Artifact Storage 쓰기 가능 여부 확인
- AuthServer는 Ready 조건에서 제외
- DB 장애: live 200, ready 503
- Storage 장애: live 200, ready 503
- Health 응답에는 연결 문자열, Secret, 물리 경로, 예외 메시지를 노출하지 않는다.

## Docker

- 이미지 태그에 `latest`를 사용하지 않는다.
- .NET 8 공식 runtime의 non-root `app` 사용자로 실행한다.
- Root filesystem은 read-only로 구성하고 `/tmp`만 tmpfs로 제공한다.
- `/app/update-storage`만 Host volume으로 쓰기 허용한다.
- Host 저장소는 컨테이너 `app` 사용자가 쓰고 Nginx가 `packages`를 읽을 수 있어야 한다.
- 시작 시 Migration을 자동 실행하지 않는다.
