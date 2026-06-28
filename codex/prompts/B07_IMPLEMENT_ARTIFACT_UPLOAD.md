# B07 - Artifact 업로드

## 1. 작업 목적
Draft용 multipart ZIP 업로드, 검증, staging, 최종 이동, DB 등록과 안전한 교체를 구현한다.

## 2. 반드시 먼저 읽을 문서
- `AGENTS.md`
- `README.md`
- `docs`의 관련 정책
- `database/schema.sql`
- `codex/WORK_STATUS.md`
- storage-policy.md
- test-policy.md
- B06 결과

문서와 현재 코드가 충돌하면 임의 구현하지 말고 `Blocked`로 보고한다.

## 3. 작업 전 현재 상태 확인
- B06 Completed
- Storage Options
- Artifact Repository
- Actor
- 임시 테스트 경로

## 4. 수정 허용 범위
- Storage Service
- ZIP Validator
- SHA-256 Stream
- Upload Service·Controller
- 교체
- 테스트

목록 밖 파일이 반드시 필요하면 이유와 경로를 먼저 보고한다.

## 5. 금지 사항
- 전체 byte[]
- 원본 파일명 경로 사용
- Published 교체
- 운영 경로 테스트
- 파일 실행

## 6. 구현 요구사항
- 1GB Option
- Public ID·파일명·StorageKey
- staging 중 크기·SHA
- ZIP·Zip Slip·절대경로·Entry·Expanded 검사
- Root 내부 확인
- packages 이동
- DB 실패·Unique 충돌 정리
- 신규 Commit 후 기존 정리

## 7. 오류 처리 및 안정성
- 취소·끊김 staging 정리
- 경로 민감정보 비로그

## 8. 빌드 및 테스트
- `dotnet build POSCAM.UpdateServer.sln -c Release`
- `dotnet test POSCAM.UpdateServer.sln -c Release --no-build`

실패 상태에서 다음 단계로 이동하지 않는다.

## 9. 완료 조건
- 정상·손상·ZipSlip·용량·중복 테스트
- 전체 메모리 로드 없음
- Published 교체 없음
- 빌드·테스트 성공

## 10. 완료 보고 형식
`codex/COMPLETION_REPORT_TEMPLATE.md` 형식을 사용하고 `codex/WORK_STATUS.md`의 해당 단계만 갱신한다.
