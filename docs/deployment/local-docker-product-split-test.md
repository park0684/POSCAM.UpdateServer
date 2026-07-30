# POSCAM UpdateServer 로컬 Docker 제품 분리 테스트 배포

## 목적

운영 배포 전에 로컬 Docker 테스트 환경의 UpdateServer에 다음 변경을 적용하고 검증한다.

```text
PCCAM       레거시 전환용 제품
PCCAM_X86   PC CAM 32비트
PCCAM_X64   PC CAM 64비트
```

대상은 로컬 테스트 컨테이너로 제한한다.

```text
Container   poscam-update-server-local
Image       poscam-update-server:local
Network     poscam-internal
Port        127.0.0.1:8083 -> 8080
Environment Development
```

운영 컨테이너와 운영 DB는 이 절차에서 다루지 않는다.

## 기존 로컬 환경

기본 마운트와 연결값은 현재 로컬 테스트 환경을 유지한다.

```text
D:\_data\poscam\update-storage
  -> /app/update-storage

D:\_work\poscam\secrets\internal_service_key.txt
  -> /run/secrets/AuthServer__InternalServiceKey

D:\_work\poscam\secrets\update_connection_string.txt
  -> /run/secrets/ConnectionStrings__DefaultConnection

AuthServer             http://host.docker.internal:8081
AdminWeb               http://127.0.0.1:8082
AdminWeb alternate     http://localhost:8082
Package public URL     http://127.0.0.1:8088
```

## 실행

저장소 루트의 Windows PowerShell 5.1 이상 환경에서 실행한다.

```powershell
Set-Location D:\_work\POSCAM.UpdateServer

git checkout master
git pull

powershell -ExecutionPolicy Bypass `
  -File .\tools\Deploy-LocalDockerTest.ps1
```

Docker Desktop이 실행 중이어야 하며, 현재 Windows 계정에서 Docker 명령을 실행할 수 있어야 한다.

## 스크립트 처리 순서

1. 로컬 전용 컨테이너·이미지·포트인지 확인한다.
2. Docker 네트워크, 스토리지, Secret 파일을 확인한다.
3. 연결 문자열에서 로컬 DB 대상을 확인한다.
4. 현재 소스로 `poscam-update-server:local` 이미지를 빌드한다.
5. `V002__split_pccam_products.sql`을 로컬 `poscam_update` DB에 적용한다.
6. `PCCAM`, `PCCAM_X86`, `PCCAM_X64`가 활성 상태인지 확인한다.
7. 기존 `poscam-update-server-local` 컨테이너를 중지하고 백업 이름으로 보존한다.
8. 기존 네트워크·포트·마운트·환경변수로 새 컨테이너를 생성한다.
9. Ready/Live Health, 스토리지 쓰기, CORS, 제품별 업데이트 확인 API를 검증한다.
10. 검증 실패 시 새 컨테이너를 제거하고 기존 컨테이너를 자동 복구한다.

## DB 컨테이너 선택

스크립트는 연결 문자열과 실행 중인 컨테이너를 기준으로 DB 컨테이너를 찾는다.

우선순위:

```text
연결 문자열 Server와 같은 컨테이너
Port 3307 -> poscam-db-new
Port 3306 -> poscam-db
그 외 실행 중인 poscam-db-new 또는 poscam-db
```

자동 선택이 맞지 않을 때만 명시한다.

```powershell
powershell -ExecutionPolicy Bypass `
  -File .\tools\Deploy-LocalDockerTest.ps1 `
  -DatabaseContainer poscam-db-new
```

## 검증 대상

### Health

```text
GET http://127.0.0.1:8083/health/live
GET http://127.0.0.1:8083/health/ready
```

Ready Health에는 DB와 스토리지 확인이 포함된다.

### 제품 코드

로컬 DB의 `update_products`에서 다음 세 제품이 모두 활성 상태여야 한다.

```text
PCCAM       1
PCCAM_X86   1
PCCAM_X64   1
```

### 업데이트 확인 API

다음 조합이 모두 정상 응답해야 한다.

```text
PCCAM_X86 + x86
PCCAM_X64 + x64
PCCAM     + x86
```

아직 해당 제품 릴리스가 등록되지 않았다면 업데이트 없음 응답이어도 된다. 요청 검증 오류나 지원하지 않는 제품 오류가 발생하면 실패로 처리한다.

### 스토리지

컨테이너 사용자로 다음 경로에 임시 파일을 생성하고 삭제할 수 있어야 한다.

```text
/app/update-storage/.staging
```

기존에 발생했던 `UnauthorizedAccessException` 재발 여부를 이 단계에서 확인한다.

## 배포 증적

성공하면 다음 경로에 JSON 결과를 저장한다.

```text
artifacts\local-docker-deploy\local-docker-deploy-YYYYMMDD-HHMMSS.json
```

포함 항목:

- 이전 이미지 ID
- 신규 이미지 ID
- 백업 컨테이너 이름
- DB 제품 조회 결과
- Live/Ready Health 결과
- `PCCAM_X86`, `PCCAM_X64`, `PCCAM` 업데이트 확인 결과

## 기존 컨테이너 보존

기존 컨테이너는 기본적으로 다음 이름으로 중지 상태로 보존된다.

```text
poscam-update-server-local-backup-YYYYMMDD-HHMMSS
```

로컬 테스트가 끝난 뒤 제거한다.

```powershell
docker rm poscam-update-server-local-backup-YYYYMMDD-HHMMSS
```

검증 성공 즉시 제거하려면 다음 옵션을 사용한다.

```powershell
powershell -ExecutionPolicy Bypass `
  -File .\tools\Deploy-LocalDockerTest.ps1 `
  -RemoveBackupOnSuccess
```

## 수동 롤백

자동 롤백이 완료되지 않았을 때만 다음 순서로 복구한다.

```powershell
docker rm -f poscam-update-server-local

docker rename `
  poscam-update-server-local-backup-YYYYMMDD-HHMMSS `
  poscam-update-server-local

docker start poscam-update-server-local
```

## 제한사항

- 컨테이너 이름을 운영 이름으로 변경하지 않는다.
- 이미지 태그를 운영 태그로 변경하지 않는다.
- 포트 `5002` 또는 외부 도메인을 사용하지 않는다.
- `ASPNETCORE_ENVIRONMENT=Production`으로 실행하지 않는다.
- `UpdateStorage__PublicBaseUrl`을 운영 URL로 설정하지 않는다.
- 운영 Secret 파일이나 운영 DB 연결 문자열을 사용하지 않는다.
- 로컬 릴리스 데이터를 운영 DB로 복사하지 않는다.
- 실제 운영 배포는 로컬 제품 코드, 릴리스 등록, 전환 테스트 완료 후 별도로 진행한다.
