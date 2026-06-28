# POSCAM.UpdateServer

PCCAM, CAMVIEWER, UPDATER의 버전 확인, 릴리스 관리, ZIP 메타데이터, 게시와 감사 로그를 제공하는 독립 .NET 8 Web API이다.

## 기술
- .NET 8 ASP.NET Core Web API
- Dapper
- MySqlConnector
- MariaDB
- Docker Compose
- Nginx 정적 package 제공

## 시작
`codex/prompts/B00_ANALYZE_STARTER.md`부터 한 단계씩 수행한다. B00은 무수정 분석이다.

## 금지
- TokenSecret 공유
- AuthServer DB 직접 조회
- Published 파일 덮어쓰기
- 시작 시 자동 Migration
