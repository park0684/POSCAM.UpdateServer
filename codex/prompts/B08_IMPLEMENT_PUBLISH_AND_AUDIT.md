# B08 - 게시·중지·감사

## 1. 작업 목적
게시 전 재검증, 상태 전이, 배포 중지, 긴급 격리와 감사 조회를 구현한다.

## 2. 반드시 먼저 읽을 문서
- `AGENTS.md`
- `README.md`
- `docs`의 관련 정책
- `database/schema.sql`
- `codex/WORK_STATUS.md`
- domain-policy.md
- storage-policy.md
- B07 결과

문서와 현재 코드가 충돌하면 임의 구현하지 말고 `Blocked`로 보고한다.

## 3. 작업 전 현재 상태 확인
- B07 Completed
- 상태 처리
- Storage
- Audit Repository

## 4. 수정 허용 범위
- Publish·Disable API
- 격리
- 감사 목록·릴리스 이력
- 테스트

목록 밖 파일이 반드시 필요하면 이유와 경로를 먼저 보고한다.

## 5. 금지 사항
- Published→Draft
- Disabled→Published
- Published 수정
- 일반 Disable 파일 삭제
- 감사 수정·삭제

## 6. 구현 요구사항
- 파일 존재·크기·SHA·ZIP 재검증
- PublishedAt UTC
- Disabled 조회 제외
- 일반 Disable 파일 유지
- 긴급 quarantine
- 감사 페이징·필터

## 7. 오류 처리 및 안정성
- 파일·DB 부분 실패 순서 테스트

## 8. 빌드 및 테스트
- `dotnet build POSCAM.UpdateServer.sln -c Release`
- `dotnet test POSCAM.UpdateServer.sln -c Release --no-build`

실패 상태에서 다음 단계로 이동하지 않는다.

## 9. 완료 조건
- 무결성 실패 409/8033
- 상태 전이 정확
- Disable·격리 구분
- 감사 Actor·전후 기록
- 빌드·테스트 성공

## 10. 완료 보고 형식
`codex/COMPLETION_REPORT_TEMPLATE.md` 형식을 사용하고 `codex/WORK_STATUS.md`의 해당 단계만 갱신한다.
