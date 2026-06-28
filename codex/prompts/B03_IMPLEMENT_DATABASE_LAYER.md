# B03 - Dapper DB 계층

## 1. 작업 목적
확정 schema 기준 DB Context와 네 Repository를 구현한다.

## 2. 반드시 먼저 읽을 문서
- `AGENTS.md`
- `README.md`
- `docs`의 관련 정책
- `database/schema.sql`
- `codex/WORK_STATUS.md`
- database-policy.md
- B02 결과

문서와 현재 코드가 충돌하면 임의 구현하지 말고 `Blocked`로 보고한다.

## 3. 작업 전 현재 상태 확인
- B02 Completed
- schema·V001
- 도메인 모델
- 패키지

## 4. 수정 허용 범위
- IDbContext·DapperContext
- Product·Release·Artifact·Audit Repository
- 매핑
- DB 오류
- 테스트

목록 밖 파일이 반드시 필요하면 이유와 경로를 먼저 보고한다.

## 5. 금지 사항
- poscam_auth SQL
- 자동 Migration
- ORM 추가
- 스키마 변경

## 6. 구현 요구사항
- DefaultConnection
- underscore mapping
- UTC_TIMESTAMP()
- 숫자 버전 정렬
- 호환 Artifact가 있는 최고 Release 쿼리
- 파라미터 바인딩

## 7. 오류 처리 및 안정성
- Duplicate·FK·연결 실패 구분
- Connection String 비로그

## 8. 빌드 및 테스트
- `dotnet build POSCAM.UpdateServer.sln -c Release`
- `dotnet test POSCAM.UpdateServer.sln -c Release --no-build`

실패 상태에서 다음 단계로 이동하지 않는다.

## 9. 완료 조건
- schema 일치
- Auth DB 참조 없음
- 자동 Migration 없음
- 빌드·테스트 성공

## 10. 완료 보고 형식
`codex/COMPLETION_REPORT_TEMPLATE.md` 형식을 사용하고 `codex/WORK_STATUS.md`의 해당 단계만 갱신한다.
