# B01 - 솔루션과 기본 프로젝트

## 1. 작업 목적
.NET 8 Web API와 테스트 프로젝트의 최소 실행 기반을 생성한다.

## 2. 반드시 먼저 읽을 문서
- `AGENTS.md`
- `README.md`
- `docs`의 관련 정책
- `database/schema.sql`
- `codex/WORK_STATUS.md`
- B00 결과

문서와 현재 코드가 충돌하면 임의 구현하지 말고 `Blocked`로 보고한다.

## 3. 작업 전 현재 상태 확인
- B00 Completed
- 기존 프로젝트 유무
- .NET SDK
- 패키지 호환

## 4. 수정 허용 범위
- 솔루션
- src/POSCAM.UpdateServer.Api
- tests/POSCAM.UpdateServer.Tests
- ApiResponse
- UpdateErrorCode
- 예외 Middleware
- Options
- live Health

목록 밖 파일이 반드시 필요하면 이유와 경로를 먼저 보고한다.

## 5. 금지 사항
- 업무 Repository·Controller
- 자동 Migration
- 토큰 처리
- 과도한 프로젝트 분할

## 6. 구현 요구사항
- .NET 8 Nullable
- Dapper·MySqlConnector
- 개발환경 Swagger
- 민감정보 없는 500/9999
- 시작 시 Options 검증 구조

## 7. 오류 처리 및 안정성
- 예외 로그에 Request ID 준비
- Token·Secret 비로그

## 8. 빌드 및 테스트
- `dotnet restore`
- `dotnet build POSCAM.UpdateServer.sln -c Release`
- `dotnet test POSCAM.UpdateServer.sln -c Release --no-build`

실패 상태에서 다음 단계로 이동하지 않는다.

## 9. 완료 조건
- 두 프로젝트 생성
- 업무 기능 미구현
- 빌드·기본 테스트 성공

## 10. 완료 보고 형식
`codex/COMPLETION_REPORT_TEMPLATE.md` 형식을 사용하고 `codex/WORK_STATUS.md`의 해당 단계만 갱신한다.
