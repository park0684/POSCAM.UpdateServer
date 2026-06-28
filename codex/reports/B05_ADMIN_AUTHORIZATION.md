# B05 관리자 인증 연동 구현 보고

## 구현 범위

- AuthServer 전용 typed HttpClient
- `POST /api/internal/update-management/authorize` 호출
- `Authorization` Header 그대로 전달
- `X-POSCAM-Service-Key` 서버 환경 설정 전달
- `X-Request-ID` 전달
- `/api/v1/admin/*` 공통 인증 Middleware
- 요청 범위 Actor Accessor
- 401·403·503 응답 매핑
- 연결 실패·Timeout·비정상 JSON 처리
- Client·Middleware·Actor 단위 테스트

## 관리자 요청 흐름

```text
/api/v1/admin/* 요청
→ Request ID 생성
→ AuthServer 내부 authorize API 호출
→ AuthServer가 Token·현재 사용자·역할·UpdateManage=12 확인
→ 성공 Actor를 요청 범위에 저장
→ 관리자 Controller 실행
```

공개 경로와 Health Check는 관리자 인증 Middleware에서 제외한다.

## 보안 정책

UpdateServer는 다음 작업을 하지 않는다.

- AccountToken 분해
- Token payload 읽기
- HMAC 또는 JWT 검증
- TokenSecret 보관·공유
- AuthServer DB 조회
- 관리자 권한 결과 캐시

Bearer Token과 내부 서비스 키는 로그에 기록하지 않는다.

## 설정

```text
AuthServer__BaseUrl=http://poscam-auth-api:8080
AuthServer__InternalServiceKey={secret}
AuthServer__TimeoutSeconds=5
```

서비스 키는 appsettings.json에 실제 값을 기록하지 않고 환경변수로 주입한다.

## 상태 매핑

| AuthServer 결과 | UpdateServer 결과 |
|---|---|
| 200 + 정상 Actor | 요청 계속 진행 |
| 401 + ErrorCode 5003 | HTTP 401 / TokenExpired 5003 |
| 기타 정상 형식 401 | HTTP 401 / TokenInvalid 5004 |
| 403 | HTTP 403 / PermissionDenied 7001 |
| 연결 실패 | HTTP 503 / ExternalServiceUnavailable 9003 |
| Timeout | HTTP 503 / ExternalServiceUnavailable 9003 |
| 5xx | HTTP 503 / ExternalServiceUnavailable 9003 |
| 비정상·빈 JSON | HTTP 503 / ExternalServiceUnavailable 9003 |
| 잘못된 성공 Actor | HTTP 503 / ExternalServiceUnavailable 9003 |

AuthServer의 상세 오류 메시지는 브라우저 응답에 그대로 전달하지 않는다.

## Actor

AuthServer 성공 응답의 다음 값만 사용한다.

- UserCode
- UserName
- UserRole

Actor는 Scoped Accessor에 요청당 한 번만 저장한다. 이후 릴리스·Artifact·감사 로그 서비스는 요청 데이터가 아니라 이 Actor를 사용한다.

## Public API 독립성

다음 경로는 AuthServer를 호출하지 않는다.

```text
POST /api/v1/updates/check
GET /health/live
```

따라서 AuthServer 장애가 공개 업데이트 확인과 정적 Package 다운로드를 차단하지 않는다.

## 검증 상태

- B04: 사용자 로컬 Release 빌드 성공, 120/120 테스트 성공
- B05: 정적 검토 완료
- B05 Release 빌드와 전체 테스트는 사용자 로컬 확인 필요
