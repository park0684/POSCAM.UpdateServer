# DB 정책

- DB: `poscam_update`
- 전용 최소권한 계정 사용
- AuthServer DB 조회 금지
- Dapper·MySqlConnector
- underscore mapping 활성화
- 쓰기 SQL은 `UTC_TIMESTAMP()`
- 숫자 버전 정렬
- Unique와 FK는 DB에서 강제
- 업무 값은 Service 검증
- Migration 자동 실행 금지
