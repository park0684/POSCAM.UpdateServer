# B06 - 릴리스 관리

## 1. 작업 목적
Active 제품, 릴리스 목록·상세, Draft 생성·수정·삭제를 구현한다.

## 2. 반드시 먼저 읽을 문서
- `AGENTS.md`
- `README.md`
- `docs`의 관련 정책
- `database/schema.sql`
- `codex/WORK_STATUS.md`
- api-contracts.md
- domain-policy.md
- B05 결과

문서와 현재 코드가 충돌하면 임의 구현하지 말고 `Blocked`로 보고한다.

## 3. 작업 전 현재 상태 확인
- B05 Completed
- 관리자 인증
- Release Repository
- 버전·상태

## 4. 수정 허용 범위
- DTO
- Service
- Controller
- 페이징·필터
- CREATE·UPDATE·DELETE_DRAFT 감사
- 테스트

목록 밖 파일이 반드시 필요하면 이유와 경로를 먼저 보고한다.

## 5. 금지 사항
- Artifact upload
- 게시·중지
- Published 수정
- Product CRUD

## 6. 구현 요구사항
- Active 제품
- 필터·페이징
- Draft만 수정·삭제
- 버전 정규화
- Duplicate 409/8011
- 강제 정책 검증
- Actor 기록

## 7. 오류 처리 및 안정성
- 상태 충돌 409/8012
- SQL 비노출

## 8. 빌드 및 테스트
- `dotnet build POSCAM.UpdateServer.sln -c Release`
- `dotnet test POSCAM.UpdateServer.sln -c Release --no-build`

실패 상태에서 다음 단계로 이동하지 않는다.

## 9. 완료 조건
- Draft CRUD·조회
- Published·Disabled 수정 차단
- 모든 관리자 API 권한 확인
- 빌드·테스트 성공

## 10. 완료 보고 형식
`codex/COMPLETION_REPORT_TEMPLATE.md` 형식을 사용하고 `codex/WORK_STATUS.md`의 해당 단계만 갱신한다.
