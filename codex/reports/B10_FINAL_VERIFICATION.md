# B10 최종 검증 보고

## 작업 결과

- 상태: Completed
- 전체 정적 검토: 완료
- 확인된 결함 수정: 완료
- 로컬 Release 빌드: 성공, 오류 0, 경고 0
- 전체 테스트: 294/294 성공, 실패 0, 건너뜀 0
- Docker build: 성공
- Docker 실행 사용자: `app`

## 변경 파일

- `src/POSCAM.UpdateServer.Api/Storage/ZipPackageValidator.cs`
  - 이름이 `/`로 끝나는 디렉터리 Entry도 실제 Stream을 확인한다.
  - 데이터가 포함된 디렉터리 Entry를 `InvalidPackage`로 거부한다.
  - 모든 ZIP Entry Stream 검증 정책과 Expanded Bytes 우회 가능성을 해소한다.
- `tests/POSCAM.UpdateServer.Tests/Storage/ZipPackageValidatorTests.cs`
  - 데이터 포함 디렉터리 Entry 회귀 테스트를 추가한다.
- `deploy/.gitignore`
  - 실제 `update-server.env`, `.env`, `.env.*` 파일을 Git 추적에서 제외한다.
  - `update-server.env.example`은 계속 추적한다.
- `codex/WORK_STATUS.md`
  - B10을 Completed로 갱신한다.
- `codex/reports/B10_FINAL_VERIFICATION.md`
  - 최종 검토와 로컬 검증 결과를 기록한다.

## 최종 정적 검토 결과

### 관리자 인증 경계

- UpdateServer에서 AccountToken을 파싱하거나 서명을 검증하지 않는다.
- `TokenSecret`, JWT 검증 코드, AuthServer DLL·프로젝트 참조가 없다.
- 관리자 요청은 `POST /api/internal/update-management/authorize`로 매번 확인한다.
- Bearer 토큰은 해석하지 않고 전달한다.
- 내부 서비스 키는 Header로만 전달하고 로그에 기록하지 않는다.
- AuthServer의 401·403·5xx·Timeout 계약이 각각 401·403·503으로 매핑된다.
- AuthServer 장애가 공개 Update Check와 package 다운로드 경로에 영향을 주지 않는다.
- AuthServer `feature/update-server-auth-contract` 브랜치의 Route, Header, Actor JSON 계약과 일치한다.
- AuthServer의 AccountToken 만료 오류는 `TokenExpired=5003`으로 반환된다.

### 공개 Update Check

- `POST /api/v1/updates/check`는 익명이며 AuthServer 의존성이 없다.
- 제품·버전·OS·Architecture·Channel을 엄격하게 검증한다.
- 버전 비교는 숫자 구성요소로 수행한다.
- `1.2.0`과 `1.2.0.0`을 동일하게 처리한다.
- 채널은 exact match이다.
- Client가 최신보다 높으면 자동 다운그레이드하지 않는다.
- 강제 업데이트와 기준 버전 정책이 확정 규칙과 일치한다.
- 호환 Artifact가 있는 가장 높은 Published Release를 선택한다.
- exact Architecture가 동일 Release의 `any`보다 우선한다.
- 업데이트 없음은 HTTP 200과 `reasonCode`로 반환한다.
- package URL은 서버가 생성한 Storage Key를 segment 단위로 escape한다.
- Update Check 응답은 no-store이다.
- IP별 60회/60초 Rate Limit와 `429 / 9004`가 적용된다.

### Release·Artifact 상태

- Release 생성·수정·삭제는 Draft에서만 가능하다.
- 게시 전 활성 Artifact 파일의 존재, 크기, SHA-256, ZIP을 다시 검증한다.
- 상태 전이는 Draft → Published → Disabled만 허용한다.
- Published → Draft, Disabled → Published 전이는 없다.
- Published Release와 Artifact를 교체하는 API가 없다.
- 일반 Disable은 package 파일과 Artifact 상태를 변경하지 않는다.
- 긴급 Quarantine은 Artifact를 Disabled로 만들고 Published Release를 함께 중지한다.
- Quarantine DB 반영 실패 시 파일을 packages 위치로 복구한다.
- Draft Artifact 교체는 새 파일과 DB Commit이 성공한 뒤 기존 파일을 정리한다.
- 동시 업로드는 Release·Artifact 잠금과 DB Unique 제약으로 보호한다.

### Storage·ZIP

- 원본 사용자 파일명은 저장 경로에 사용하지 않는다.
- Public ID, 파일명, Storage Key는 서버가 생성한다.
- 업로드는 Stream으로 처리하며 실제 크기와 SHA-256을 서버가 계산한다.
- 최대 업로드 크기, Entry 수, Expanded Bytes를 검사한다.
- 절대경로, Drive 경로, UNC, `..`, NTFS ADS, NUL, Root 탈출을 거부한다.
- 손상·읽기 불가 ZIP과 파일이 없는 ZIP을 거부한다.
- 모든 파일 Entry Stream을 읽어 실제 크기와 ZIP 무결성을 확인한다.
- 디렉터리 Entry Stream도 확인하며 데이터 포함 디렉터리 Entry를 거부한다.
- Published package URL은 덮어쓰지 않는다.
- 실제 ZIP 바이너리를 DB에 저장하지 않는다.

### DB

- UpdateServer는 `poscam_update`만 사용한다.
- `poscam_auth` SQL과 교차 DB FK가 없다.
- Repository SQL은 파라미터를 사용한다.
- 시간 기록은 `UTC_TIMESTAMP()`를 사용한다.
- `database/schema.sql`과 `database/migrations/V001__create_update_schema.sql`은 파일 SHA까지 동일하다.
- Seed 제품 코드는 `PCCAM`, `CAMVIEWER`, `UPDATER`로 확정 정책과 일치한다.
- 자동 Migration, `Database.Migrate`, `EnsureCreated` 실행 경로가 없다.

### API·오류 응답

- JSON은 전역 camelCase를 사용한다.
- 공통 응답은 `success`, `message`, `errorCode`, `data`이다.
- 생성 201, 검증 400, 인증 401, 권한 403, 없음 404, 충돌 409, 크기 413, 패키지 415, Rate 429, 서버 500, Auth 의존성 503을 구분한다.
- 전역 오류 응답은 SQL, Stack Trace, Secret, 물리 경로를 반환하지 않는다.
- 오류 응답에서도 `X-Request-ID`를 보존한다.

### 운영·배포

- IIS → Ubuntu Nginx → UpdateServer의 2단계 프록시 설정과 `ForwardLimit=2`가 일치한다.
- 정확한 IIS·Nginx IP만 Known Proxy로 등록한다.
- CORS는 설정된 HTTPS AdminWeb Origin만 허용한다.
- OPTIONS preflight는 관리자 인증 호출 전에 처리된다.
- `/health/live`는 프로세스만, `/health/ready`는 DB와 Storage만 확인한다.
- AuthServer는 Ready 조건에 포함되지 않는다.
- Request 로그는 Method, Path, Status, 시간, Request ID와 Remote IP만 기록한다.
- Authorization, Cookie, 서비스 키와 Request Body를 요청 로그에 기록하지 않는다.
- 운영 Swagger는 공개하지 않는다.
- Dockerfile은 non-root `app` 사용자를 사용한다.
- Compose는 read-only root filesystem, `/tmp` tmpfs, Storage volume을 사용한다.
- Nginx는 `/api/internal/*`을 외부 404로 차단하고 packages autoindex를 비활성화한다.
- package는 GET·HEAD만 허용하고 immutable cache와 `nosniff`를 설정한다.
- 이미지 태그에 `latest`를 강제하지 않는다.
- 실제 배포 환경 파일은 Git 무시 대상이다.

## 확인된 결함과 처리

### ZIP 디렉터리 Entry 검증 누락

기존에는 이름이 `/`로 끝나는 Entry를 디렉터리로 판단하고 Stream을 읽지 않았다. 데이터가 포함된 비정상 디렉터리 Entry가 실제 Stream 검증을 우회할 수 있으므로, 디렉터리 Entry도 Stream을 확인하고 데이터가 포함되면 `InvalidPackage`로 거부하도록 수정했다.

### 실제 배포 환경 파일 Git 제외 누락

실제 `deploy/update-server.env` 파일을 실수로 Git에 추가할 수 있었으므로 `deploy/.gitignore`를 추가했다. 예제 파일은 계속 추적한다.

## 검증 결과

실행 명령:

```powershell
cd D:\_work\POSCAM.UpdateServer

git switch feature/initial-update-server
git pull

dotnet restore POSCAM.UpdateServer.sln
dotnet build POSCAM.UpdateServer.sln -c Release
dotnet test POSCAM.UpdateServer.sln -c Release --no-build

docker build -t poscam-update-server:b10-test .
docker image inspect poscam-update-server:b10-test --format "{{.Config.User}}"
```

결과:

- Restore 성공
- API Release 빌드 성공
- Tests Release 빌드 성공
- 컴파일 오류 0
- 경고 0
- 테스트 294/294 성공
- 테스트 실패 0
- 건너뜀 0
- Docker image build 성공
- Docker 실행 사용자 `app`

테스트 실행 중 출력된 DB readiness `warn`과 `fail` 로그는 연결되지 않은 테스트용 DB가 `/health/ready`에서 `503 / Unhealthy`로 처리되는지를 확인하기 위한 의도된 테스트 로그이며 테스트 실패가 아니다.

## 남은 문제

- 컴파일 오류: 없음
- 실제 동작 오류: 정적 검토와 자동 테스트에서 추가 발견 없음
- 불필요한 중복: 추가 발견 없음
- 운영 확인 사항: 실제 MariaDB·IIS·Nginx·Docker Volume 통합은 배포 체크리스트에서 수행
- 다음 단계 선행조건: 충족, C00 시작 가능

## 정책 이탈 여부

- 없음
