# B10 - 최종 검증

## 1. 작업 목적
전체 구현을 정책과 대조하고 컴파일 오류, 실제 동작 오류, 불필요한 중복만 수정한다.

## 2. 반드시 먼저 읽을 문서
- `AGENTS.md`
- `README.md`
- `docs`의 관련 정책
- `database/schema.sql`
- `codex/WORK_STATUS.md`
- definition-of-done.md
- B01~B09 보고와 diff

문서와 현재 코드가 충돌하면 임의 구현하지 말고 `Blocked`로 보고한다.

## 3. 작업 전 현재 상태 확인
- B01~B09 Completed
- 전체 코드·테스트
- schema·migration·seed
- Docker·Compose·Nginx
- Secret·금지 의존성

## 4. 수정 허용 범위
- 확인된 결함 수정
- 중복 제거
- 최종 보고
- WORK_STATUS

목록 밖 파일이 반드시 필요하면 이유와 경로를 먼저 보고한다.

## 5. 금지 사항
- 새 기능
- 정책 변경
- 운영 Migration
- commit·push·deploy

## 6. 구현 요구사항
- Token 직접 검증·TokenSecret·poscam_auth SQL 검색
- Published 수정 API 확인
- AuthServer 없이 공개 확인
- Auth 장애 관리자 503
- Path·Zip·SHA·동시 업로드
- schema와 V001 비교
- API JSON 이름
- Secret 검색

## 7. 오류 처리 및 안정성
- 확인된 결함만 수정

## 8. 빌드 및 테스트
- `dotnet build POSCAM.UpdateServer.sln -c Release`
- `dotnet test POSCAM.UpdateServer.sln -c Release --no-build`
- 가능하면 Docker build
- 금지 문자열 검색

실패 상태에서 다음 단계로 이동하지 않는다.

## 9. 완료 조건
- 완료 기준 통과
- 남은 세 분류 문제 없음
- 운영 체크리스트만 남음
- C00 시작 가능

## 10. 완료 보고 형식
`codex/COMPLETION_REPORT_TEMPLATE.md` 형식을 사용하고 `codex/WORK_STATUS.md`의 해당 단계만 갱신한다.
