# B04 - 공개 Update Check

## 1. 작업 목적
익명 클라이언트에 호환되는 가장 높은 Published Release를 반환한다.

## 2. 반드시 먼저 읽을 문서
- `AGENTS.md`
- `README.md`
- `docs`의 관련 정책
- `database/schema.sql`
- `codex/WORK_STATUS.md`
- api-contracts.md
- domain-policy.md
- B03 결과

문서와 현재 코드가 충돌하면 임의 구현하지 말고 `Blocked`로 보고한다.

## 3. 작업 전 현재 상태 확인
- B03 Completed
- 호환 조회
- UpdateVersion
- ApiResponse

## 4. 수정 허용 범위
- Request·Response DTO
- Service
- 공개 Controller
- 테스트

목록 밖 파일이 반드시 필요하면 이유와 경로를 먼저 보고한다.

## 5. 금지 사항
- 인증 요구
- 파일 전송 Controller
- 관리자 API
- 다운그레이드

## 6. 구현 요구사항
- 입력 검증
- Active Product
- Published
- 호환 Artifact
- 최고 버전
- exact 우선
- mandatory 판정
- PublicBaseUrl+StorageKey URL
- no-store

## 7. 오류 처리 및 안정성
- 잘못된 입력 400
- 업데이트 없음 200/Success=true

## 8. 빌드 및 테스트
- `dotnet build POSCAM.UpdateServer.sln -c Release`
- `dotnet test POSCAM.UpdateServer.sln -c Release --no-build`

실패 상태에서 다음 단계로 이동하지 않는다.

## 9. 완료 조건
- 모든 reasonCode 테스트
- 익명 가능
- AuthServer 무의존
- 빌드·테스트 성공

## 10. 완료 보고 형식
`codex/COMPLETION_REPORT_TEMPLATE.md` 형식을 사용하고 `codex/WORK_STATUS.md`의 해당 단계만 갱신한다.
