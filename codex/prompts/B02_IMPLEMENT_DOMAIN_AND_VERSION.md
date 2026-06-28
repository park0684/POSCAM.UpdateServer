# B02 - 도메인과 버전

## 1. 작업 목적
도메인 상태, 버전 파싱·정규화·비교, 강제 업데이트 판정을 구현한다.

## 2. 반드시 먼저 읽을 문서
- `AGENTS.md`
- `README.md`
- `docs`의 관련 정책
- `database/schema.sql`
- `codex/WORK_STATUS.md`
- domain-policy.md
- B01 결과

문서와 현재 코드가 충돌하면 임의 구현하지 말고 `Blocked`로 보고한다.

## 3. 작업 전 현재 상태 확인
- B01 Completed
- 기본 구조
- ErrorCode
- 테스트 구성

## 4. 수정 허용 범위
- Entity·Model
- 상태 Enum·상수
- UpdateVersion
- 상태 전이
- 단위 테스트

목록 밖 파일이 반드시 필요하면 이유와 경로를 먼저 보고한다.

## 5. 금지 사항
- DB·Controller
- 문자열 버전 정렬
- prerelease 지원

## 6. 구현 요구사항
- 3·4자리 파싱
- 0~65535
- 1.2.0=1.2.0.0
- Revision 정규화
- 강제 필드 상호배타
- Draft→Published→Disabled
- exact architecture 우선

## 7. 오류 처리 및 안정성
- 잘못된 버전은 명시적 도메인 실패

## 8. 빌드 및 테스트
- `dotnet build POSCAM.UpdateServer.sln -c Release`
- `dotnet test POSCAM.UpdateServer.sln -c Release --no-build`

실패 상태에서 다음 단계로 이동하지 않는다.

## 9. 완료 조건
- 필수 버전·강제·상태 테스트
- 문자열 정렬 없음
- 빌드·테스트 성공

## 10. 완료 보고 형식
`codex/COMPLETION_REPORT_TEMPLATE.md` 형식을 사용하고 `codex/WORK_STATUS.md`의 해당 단계만 갱신한다.
