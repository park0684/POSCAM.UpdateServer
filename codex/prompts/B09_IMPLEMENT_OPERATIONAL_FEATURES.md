# B09 - 운영 기능

## 1. 작업 목적
Rate Limit, 제한 CORS, Health, Forwarded Headers, Request ID, 로그, Dockerfile을 완성한다.

## 2. 반드시 먼저 읽을 문서
- `AGENTS.md`
- `README.md`
- `docs`의 관련 정책
- `database/schema.sql`
- `codex/WORK_STATUS.md`
- deployment-policy.md
- deploy 예제
- B08 결과

문서와 현재 코드가 충돌하면 임의 구현하지 말고 `Blocked`로 보고한다.

## 3. 작업 전 현재 상태 확인
- B08 Completed
- Middleware 순서
- Options
- 실제 endpoint
- Health 가능 방식

## 4. 수정 허용 범위
- Program·Middleware
- Rate Limit
- CORS
- Health
- Request ID·로그
- Dockerfile
- 배포 문서
- 테스트

목록 밖 파일이 반드시 필요하면 이유와 경로를 먼저 보고한다.

## 5. 금지 사항
- AllowAnyOrigin
- AuthServer Ready 포함
- 운영 Swagger 무조건 공개
- 자동 Migration
- latest 강제

## 6. 구현 요구사항
- 60/60 Rate Limit
- 설정 Origin만 CORS
- OPTIONS 순서
- live·ready
- 안전한 Forwarded Headers
- Request ID 연결
- 민감 Header 제외
- non-root Dockerfile

## 7. 오류 처리 및 안정성
- Options 누락 시 Secret 없는 명확한 실패

## 8. 빌드 및 테스트
- `dotnet build POSCAM.UpdateServer.sln -c Release`
- `dotnet test POSCAM.UpdateServer.sln -c Release --no-build`
- 가능하면 Docker build

실패 상태에서 다음 단계로 이동하지 않는다.

## 9. 완료 조건
- 배포 예제 일치
- AuthServer Ready 무영향
- Rate·CORS 테스트
- 빌드·테스트 성공

## 10. 완료 보고 형식
`codex/COMPLETION_REPORT_TEMPLATE.md` 형식을 사용하고 `codex/WORK_STATUS.md`의 해당 단계만 갱신한다.
