# B11 Manifest 파일별 복구 업데이트 완료 보고서

## 1. 작업 목적

B11의 목적은 기존 Full ZIP 업데이트 구조를 유지하면서, 동일 버전에서도 클라이언트가 로컬 필수 파일의 누락 또는 손상을 감지하고 필요한 파일만 복구할 수 있도록 서버가 파일별 Manifest를 제공하는 것이다.

서버는 클라이언트 PC의 실제 로컬 파일 상태를 알 수 없으므로 `NeedRepair`를 직접 판단하지 않는다. 서버는 Published Artifact 기준의 파일별 Manifest를 제공하고, 클라이언트가 로컬 파일 존재 여부와 SHA-256을 검사해 복구 필요 여부를 판단한다.

## 2. 최종 결론

B11 서버 기능 구현은 완료되었다.

- Full ZIP 업로드 방식은 유지한다.
- ZIP 업로드 시 파일별 Manifest 대상 파일을 추출해 별도 storage 파일로 저장한다.
- 파일별 Manifest metadata는 `update_artifact_files` 테이블에 저장한다.
- Artifact 교체 시 기존 Manifest DB rows와 물리 파일을 정리한다.
- 공개 Update Check 응답에 `files[]`를 추가한다.
- 동일 버전 `ALREADY_LATEST` 응답에서도 compatible Artifact가 있으면 `files[]`를 내려준다.
- 기존 클라이언트 호환성을 위해 기존 package 응답 필드는 유지한다.

최종 검증 결과:

```text
Release 빌드 성공, 경고 0
테스트 312/312 성공
```

## 3. 주요 변경 파일

### 3.1 DB / Migration

- `database/schema.sql`
- `database/migrations/20260704_add_update_artifact_files.sql`

추가 테이블:

```text
update_artifact_files
```

주요 컬럼:

```text
file_code
artifact_code
file_public_id
file_path
file_size
file_sha256
file_storage_key
file_download_path
is_required
file_status
created_at
```

운영 DB에는 자동 반영되지 않는다. 배포 전 아래 migration을 수동 적용해야 한다.

```text
database/migrations/20260704_add_update_artifact_files.sql
```

### 3.2 Entity / Enum / Repository

- `src/POSCAM.UpdateServer.Api/Models/Entities/UpdateArtifactFile.cs`
- `src/POSCAM.UpdateServer.Api/Models/Enums/ArtifactFileStatus.cs`
- `src/POSCAM.UpdateServer.Api/Repositories/IUpdateArtifactFileRepository.cs`
- `src/POSCAM.UpdateServer.Api/Repositories/UpdateArtifactFileRepository.cs`

Repository 기능:

- Artifact 기준 Active Manifest 조회
- Artifact 기준 Manifest 존재 여부 확인
- Manifest 다건 등록
- Artifact 기준 Manifest 삭제

### 3.3 Storage Manifest 생성

- `src/POSCAM.UpdateServer.Api/Storage/ArtifactFileManifestEntry.cs`
- `src/POSCAM.UpdateServer.Api/Storage/IArtifactFileManifestService.cs`
- `src/POSCAM.UpdateServer.Api/Storage/ArtifactFileManifestService.cs`

역할:

- ZIP 내부 Entry 순회
- Zip Slip / 절대경로 / 위험 경로 차단
- 디렉터리 Entry 제외
- Manifest 포함/제외 대상 판정
- 대상 파일별 SHA-256 계산
- 대상 파일별 storage 저장
- 실패 시 생성된 파일 정리
- DB 실패 시 외부에서 정리 가능하도록 `DeleteManifestFilesAsync` 제공

현재 포함 대상:

```text
*.exe
*.dll
ffmpeg.exe
mediamtx.exe
resources/*
resource/*
templates/*
assets/*
```

현재 제외 디렉터리:

```text
config
configs
log
logs
cache
temp
tmp
token
tokens
auth
localdb
database
data
```

### 3.4 Artifact Upload 연결

- `src/POSCAM.UpdateServer.Api/Services/ArtifactUploadService.cs`
- `src/POSCAM.UpdateServer.Api/Services/ArtifactUploadService.Mapping.cs`
- `src/POSCAM.UpdateServer.Api/Models/Dtos/Admin/Artifacts/ArtifactUploadDtos.cs`

변경된 업로드 흐름:

```text
1. ZIP staging 저장
2. ZIP 검증
3. ZIP 내부 파일별 Manifest 생성
4. Manifest 대상 파일이 없으면 InvalidPackage 처리
5. Full ZIP packages 이동
6. DB transaction 시작
7. Artifact 신규 등록 또는 교체
8. 기존 artifact_files 조회
9. 기존 artifact_files 삭제
10. 신규 artifact_files 등록
11. Commit
12. 기존 manifest 파일 storage 정리
13. 기존 full ZIP 정리
```

업로드 응답에 `manifestFileCount`가 추가되었다.

### 3.5 Update Check 연결

- `src/POSCAM.UpdateServer.Api/Models/Dtos/Updates/UpdateCheckResponse.cs`
- `src/POSCAM.UpdateServer.Api/Services/UpdateCheckService.cs`

`UpdateCheckResponse`에 `files[]`가 추가되었다.

`files[]` 항목:

```text
path
size
sha256
required
downloadUrl
```

동일 버전 응답 예시 정책:

```text
updateAvailable = false
reasonCode = ALREADY_LATEST
files[] = compatible Artifact의 Manifest
```

서버는 복구 필요 여부를 판단하지 않고, 클라이언트가 `files[]`를 기준으로 로컬 파일을 검사한다.

### 3.6 문서

- `docs/manifest-repair-update-policy.md`
- `docs/api-contracts.md`
- `codex/prompts/B11_IMPLEMENT_MANIFEST_REPAIR_UPDATE.md`
- `codex/reports/B11_CURRENT_CODE_ANALYSIS.md`
- `codex/reports/B11_COMPLETION_REPORT.md`
- `codex/WORK_STATUS.md`

## 4. 테스트 보강

추가/수정된 테스트 영역:

- Repository SQL 계약 테스트
- `UpdateArtifactFileRepositorySqlTests`
- `ArtifactFileManifestServiceTests`
- `ArtifactUploadServiceTests`
- `ArtifactUploadConcurrentEditTests`
- `ArtifactReplacementFailureTests`
- `UpdateCheckServiceTests`

검증 내용:

- `update_artifact_files` SQL 계약
- Manifest 대상 파일 포함/제외 판정
- 위험 ZIP Entry 거부
- 파일별 SHA-256 계산
- Manifest 파일 삭제
- 업로드 성공 시 Manifest DB 저장
- 업로드 실패 시 신규 Manifest 파일 정리
- Artifact 교체 시 기존 Manifest 정리
- Update Check `files[]` 응답
- 동일 버전 `ALREADY_LATEST`에서도 `files[]` 반환
- 위험한 Manifest storageKey가 있으면 `DatabaseError` 처리

## 5. 최종 검증 결과

사용자 로컬 환경에서 아래 명령으로 검증했다.

```powershell
dotnet build POSCAM.UpdateServer.sln -c Release
dotnet test POSCAM.UpdateServer.sln -c Release --no-build
```

결과:

```text
빌드: 성공
테스트: 312/312 성공
실패: 0
건너뜀: 0
```

테스트 중 `/health/ready` 503 로그는 DB 미연결 readiness 상태를 검증하는 기존 테스트 로그이며, 최종 테스트 실패가 없으므로 B11 실패로 보지 않는다.

## 6. 운영 배포 전 확인 사항

### 6.1 DB Migration 수동 적용

운영 DB 반영 전 반드시 다음 migration을 적용해야 한다.

```text
database/migrations/20260704_add_update_artifact_files.sql
```

테이블이 없으면 Artifact 업로드 또는 Update Check Manifest 조회가 실패할 수 있다.

### 6.2 기존 Artifact 처리

B11 적용 전 이미 업로드된 기존 Artifact에는 `update_artifact_files` rows가 없다.

정책 선택이 필요하다.

- 기존 Artifact는 새 버전 업로드 시부터 Manifest 생성
- 또는 기존 ZIP을 다시 업로드하여 Manifest 생성
- 또는 별도 backfill 도구를 만들어 기존 ZIP에서 Manifest 생성

현재 B11 구현 범위에는 기존 Artifact backfill 자동화는 포함하지 않았다.

### 6.3 클라이언트 반영 포인트

신규 클라이언트는 Update Check 응답의 `files[]`를 사용해야 한다.

클라이언트 판단 흐름:

```text
1. Update Check 호출
2. 기존 방식대로 updateAvailable=true이면 full ZIP 업데이트 가능
3. updateAvailable=false여도 files[]가 있으면 로컬 파일 검사
4. files[].path 기준 로컬 파일 존재 확인
5. 파일이 없거나 SHA-256 불일치 시 복구 필요로 판단
6. 필요한 파일만 files[].downloadUrl로 다운로드
7. 실행 중인 EXE/DLL은 직접 덮어쓰지 않고 updater 단계에서 교체
```

### 6.4 Nginx / Static 파일 제공

`files[].downloadUrl`은 기존 `PublicBaseUrl + /packages/{storageKey}` 구조를 사용한다.

따라서 `/packages` 정적 파일 제공 설정이 기존 ZIP뿐 아니라 `files/{filePublicId}` 경로도 제공해야 한다.

## 7. 의도적으로 제외한 범위

이번 B11에서는 아래를 구현하지 않았다.

- 클라이언트 로컬 파일 상태를 서버에 전송하는 API
- 서버 측 `NeedRepair` 최종 판단
- 기존 Artifact ZIP의 자동 backfill
- 파일별 다운로드 API Controller
- 파일별 다운로드 감사 로그
- 파일별 다운로드 권한 검증
- 파일별 Manifest 포함/제외 정책의 제품별 설정화
- 실행 중 파일 교체 updater 구현

## 8. 완료 기준 충족 여부

| 항목 | 결과 |
|---|---|
| DB schema 추가 | 완료 |
| 수동 migration 추가 | 완료 |
| Entity / Enum 추가 | 완료 |
| Repository 추가 | 완료 |
| Storage Manifest 생성 | 완료 |
| Artifact Upload 연결 | 완료 |
| Update Check files[] 연결 | 완료 |
| API 계약 문서 갱신 | 완료 |
| 빌드 | Release 성공, 경고 0 |
| 테스트 | 312/312 성공 |

## 9. 최종 상태

B11은 서버 기준 구현과 검증이 완료되었다.

다음 작업은 클라이언트 updater 쪽에서 `files[]`를 이용한 로컬 파일 검사 및 필요한 파일만 복구하는 흐름을 구현하는 것이다.
