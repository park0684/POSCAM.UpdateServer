# B05 - 관리자 인증 연동

## 1. 작업 목적
AccountToken을 직접 검증하지 않고 AuthServer 내부 API를 사용하는 공통 관리자 인증 계층을 구현한다.

## 2. 반드시 먼저 읽을 문서
- `AGENTS.md`
- `README.md`
- `docs`의 관련 정책
- `database/schema.sql`
- `codex/WORK_STATUS.md`
- authserver-contract.md
- AuthServer A05 결과
- B04 결과

문서와 현재 코드가 충돌하면 임의 구현하지 말고 `Blocked`로 보고한다.

## 3. 작업 전 현재 상태 확인
- B04·A05 Completed
- HttpClientFactory
- 예외·응답
- Filter/Service 방식

## 4. 수정 허용 범위
- AuthServerOptions
- Client
- Authorization Service/Filter
- DI
- 테스트

목록 밖 파일이 반드시 필요하면 이유와 경로를 먼저 보고한다.

## 5. 금지 사항
- 토큰 파싱
- HMAC·JWT 검증
- TokenSecret
- Auth DB
- 장기 캐시

## 6. 구현 요구사항
- Authorization 전달
- 서비스 키 Header
- Actor 요청 범위 보관
- 401·403 매핑
- 연결·Timeout·비정상 응답 503
- 매 요청 확인

## 7. 오류 처리 및 안정성
- 토큰·키 비로그
- Timeout 명시

## 8. 빌드 및 테스트
- `dotnet build POSCAM.UpdateServer.sln -c Release`
- `dotnet test POSCAM.UpdateServer.sln -c Release --no-build`

실패 상태에서 다음 단계로 이동하지 않는다.

## 9. 완료 조건
- 직접 검증 없음
- 상태 매핑 정확
- Actor 사용 가능
- 빌드·테스트 성공

## 10. 완료 보고 형식
`codex/COMPLETION_REPORT_TEMPLATE.md` 형식을 사용하고 `codex/WORK_STATUS.md`의 해당 단계만 갱신한다.
