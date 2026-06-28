# B00 Starter 분석 결과

## 판정

- 정책 충돌: 없음
- AuthServer 계약 충돌: 없음
- DB schema와 V001 충돌: 없음
- 프로젝트 구조: 정상
- B01 진행 가능: 가능

## 현재 프로젝트 상태

- `src/POSCAM.UpdateServer.Api`: .NET 8 기본 Web API 템플릿
- `tests/POSCAM.UpdateServer.Tests`: .NET 8 xUnit 템플릿
- 솔루션의 두 프로젝트 경로 정상
- 테스트 프로젝트 표시명은 B01에서 `POSCAM.UpdateServer.Tests`로 정리
- API 프로젝트 참조 패키지는 OpenAPI와 Swagger만 존재
- 테스트 프로젝트의 API ProjectReference는 없음
- WeatherForecast 예제와 빈 UnitTest1 존재

## B01 확정 작업

1. 공통 `ApiResponse<T>`
2. `UpdateErrorCode`
3. Request ID Middleware
4. 전역 예외 Middleware
5. Options 모델 및 설정 바인딩
6. `/health/live`
7. Dapper·MySqlConnector
8. 테스트 ProjectReference
9. 기반 단위 테스트

## 범위 밖

- Repository와 실제 DB 연결
- 공개 Update Check 업무 API
- AuthServer HttpClient
- 릴리스·Artifact 업무 기능
- CORS·Rate Limiter 실제 Middleware
- Ready Health Check
- Dockerfile
