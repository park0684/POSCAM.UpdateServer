# B11 현재 코드 분석 보고서

## 1. 분석 기준

- Repository: `park0684/POSCAM.UpdateServer`
- Branch: `feature/initial-update-server`
- 단계: B11 - Manifest 파일별 복구 업데이트
- 목적: 기능 코드 수정 전, 현재 구현 구조와 B11 적용 범위를 확정한다.

## 2. 현재 상태 요약

`feature/initial-update-server` 브랜치에는 B00~B10 기반 구현이 완료되어 있다.

확인된 주요 기반:

- DB 계층과 Dapper Repository 구조 존재
- 공개 `POST /api/v1/updates/check` 존재
- Release 관리 API 존재
- Artifact 업로드 API 존재
- ZIP staging 저장, SHA-256 계산, ZIP 검증, packages 이동 구조 존재
- 게시 직전 Artifact 파일 존재/크기/SHA-256/ZIP 재검증 구조 존재
- 감사 로그 구조 존재
- Rate Limit, CORS, Health Check, Docker 기반 운영 설정 존재

따라서 B11은 신규 서버 전체 구현이 아니라 기존 B07/B08 구조를 확장하는 작업이다.

## 3. B11 정책과 현재 코드의 핵심 차이

현재 구조는 `update_artifacts`에 Full ZIP 단위 메타데이터만 저장한다.

현재 저장 대상:

- Artifact Code
- Release Code
- Public ID
- OS
- Architecture
- Package Type
- File Name
- Storage Key
- File Size
- SHA-256
- Status

B11에서 추가로 필요한 구조:

- ZIP 내부 파일별 상대경로
- 파일별 크기
- 파일별 SHA-256
- 파일별 Public ID 또는 Storage Key
- 파일별 다운로드 URL
- Manifest 포함/제외 여부
- 파일 상태

현재 `update_artifact_files` 테이블은 없다.

## 4. 서버 NeedRepair 판정 기준

서버는 클라이언트 로컬 설치 폴더의 파일 존재 여부와 SHA-256을 직접 확인할 수 없다.

따라서 B11에서 서버가 최종 `NeedRepair`를 확정하지 않는다.

권장 정책:

1. 서버는 최신 Compatible Release와 Artifact를 조회한다.
2. 서버는 기존 버전 비교 결과를 유지한다.
3. 서버는 응답에 파일별 Manifest를 함께 내려준다.
4. 클라이언트가 로컬 파일 존재 여부와 SHA-256을 검사한다.
5. 클라이언트가 누락/손상 파일이 있으면 내부적으로 `NeedRepair`로 판단한다.
6. 필요한 파일만 개별 다운로드한다.

즉, API 응답은 `NeedRepair` 판단에 필요한 데이터를 제공하고, 최종 복구 판단은 클라이언트가 수행한다.

## 5. API 응답 확장 방향

기존 `UpdateCheckResponse`는 Full ZIP 기준 필드만 가지고 있다.

기존 주요 필드:

- `updateAvailable`
- `mandatory`
- `reasonCode`
- `productCode`
- `currentVersion`
- `latestVersion`
- `packageType`
- `packageUrl`
- `fileName`
- `fileSize`
- `sha256`

B11에서는 기존 필드를 유지하고 아래 필드를 추가하는 것이 안전하다.

```csharp
public IReadOnlyList<UpdateArtifactFileResponse> Files { get; init; } = [];
```

권장 신규 DTO:

```csharp
public sealed class UpdateArtifactFileResponse
{
    public string Path { get; init; } = string.Empty;
    public long Size { get; init; }
    public string Sha256 { get; init; } = string.Empty;
    public bool Required { get; init; }
    public string DownloadUrl { get; init; } = string.Empty;
}
```

호환성 기준:

- 기존 클라이언트는 `files`를 무시해도 기존 full update 동작을 유지한다.
- 신규 클라이언트는 `files`를 사용해 동일 버전 복구를 수행한다.
- `currentVersion == latestVersion` 이더라도 `files`는 내려줄 수 있다.
- 이 경우 `updateAvailable=false`, `reasonCode=ALREADY_LATEST`는 유지한다.
- 클라이언트가 `files` 검사 후 필요 시 `NeedRepair`로 전환한다.

## 6. DB 추가 필요 항목

신규 테이블이 필요하다.

```sql
CREATE TABLE update_artifact_files
(
    file_code BIGINT NOT NULL AUTO_INCREMENT PRIMARY KEY,
    artifact_code BIGINT NOT NULL,

    file_public_id CHAR(32) NOT NULL,
    file_path VARCHAR(500) NOT NULL,
    file_size BIGINT NOT NULL,
    file_sha256 CHAR(64) NOT NULL,

    file_storage_key VARCHAR(700) NOT NULL,
    file_download_path VARCHAR(700) NOT NULL,

    is_required TINYINT NOT NULL DEFAULT 1,
    file_status TINYINT NOT NULL DEFAULT 1,
    created_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,

    CONSTRAINT fk_update_artifact_files_artifact
        FOREIGN KEY (artifact_code) REFERENCES update_artifacts (art_code)
        ON DELETE RESTRICT ON UPDATE RESTRICT,

    UNIQUE KEY uq_update_artifact_files_public_id (file_public_id),
    UNIQUE KEY uq_update_artifact_files_storage_key (file_storage_key),
    UNIQUE KEY uq_update_artifact_files_path (artifact_code, file_path),
    INDEX ix_update_artifact_files_artifact (artifact_code),
    INDEX ix_update_artifact_files_status (artifact_code, file_status)
);
```

주의:

- 운영 Migration은 자동 실행하지 않는다.
- `database/schema.sql`에는 신규 설치 기준 스키마를 반영한다.
- 운영 반영용 SQL은 별도 수동 적용 파일로 분리하는 것이 안전하다.

## 7. Entity/Repository 추가 필요 항목

신규 Entity:

- `src/POSCAM.UpdateServer.Api/Models/Entities/UpdateArtifactFile.cs`

신규 Repository Interface:

- `src/POSCAM.UpdateServer.Api/Repositories/IUpdateArtifactFileRepository.cs`

신규 Repository 구현:

- `src/POSCAM.UpdateServer.Api/Repositories/UpdateArtifactFileRepository.cs`

필요 메서드:

```csharp
Task<IReadOnlyList<UpdateArtifactFile>> GetActiveByArtifactAsync(long artifactCode, ...);
Task CreateManyAsync(IReadOnlyList<UpdateArtifactFile> files, ...);
Task DeleteByArtifactAsync(long artifactCode, ...);
Task<bool> ExistsByArtifactAsync(long artifactCode, ...);
```

Draft Artifact 교체 시 기존 Artifact row를 Replace하는 현재 구조를 유지한다면, 같은 `artifact_code`에 연결된 기존 `update_artifact_files`는 삭제 후 재생성해야 한다.

## 8. Storage 확장 필요 항목

현재 Storage는 다음 책임을 수행한다.

- upload stream을 staging에 저장
- 저장 중 ZIP 전체 SHA-256 계산
- ZIP 구조 검증
- packages로 full ZIP 이동
- 게시 직전 full ZIP 재검증
- 긴급 격리

B11에서 추가할 책임:

- ZIP 내부 파일별 Manifest 생성
- 포함/제외 대상 판단
- 파일별 SHA-256 계산
- 파일별 storage key 생성
- 파일별 물리 파일 저장
- DB 실패 시 생성된 파일 정리 또는 격리

권장 방식:

- 기존 `ZipPackageValidator`는 검증 책임을 유지한다.
- 새 서비스로 `IZipManifestBuilder` 또는 `IArtifactFileManifestService`를 추가한다.
- 기존 Validator에 과도한 책임을 추가하지 않는다.

권장 신규 파일:

- `src/POSCAM.UpdateServer.Api/Storage/ArtifactFileStorageDestination.cs`
- `src/POSCAM.UpdateServer.Api/Storage/ArtifactFileManifestEntry.cs`
- `src/POSCAM.UpdateServer.Api/Storage/IArtifactFileManifestService.cs`
- `src/POSCAM.UpdateServer.Api/Storage/ArtifactFileManifestService.cs`

## 9. 파일 포함/제외 정책

B11 정책 문서 기준으로 아래는 Manifest 포함 대상이다.

- `*.exe`
- `*.dll`
- `providers/*.dll`
- `ffmpeg.exe`
- `mediamtx.exe`
- 필수 리소스 파일
- 기본 템플릿 파일
- 프로그램 실행에 필요한 고정 설정 샘플 파일

기본 제외 대상:

- 사용자 설정 파일
- 인증 토큰 파일
- 로그 파일
- 캐시 파일
- 임시 파일
- 로컬 DB 파일
- 장비별 또는 매장별 생성 파일
- 실행 중 생성/수정되는 상태 파일

권장 구현:

- 1차는 확장자/파일명/디렉터리 기반 고정 규칙으로 시작한다.
- 추후 제품별 manifest include/exclude 정책이 필요하면 옵션화한다.
- 제외 대상은 서버가 파일별 저장소에도 저장하지 않는다.

## 10. ArtifactUploadService 수정 범위

현재 업로드 흐름:

1. 요청 검증
2. Release Draft 확인
3. Storage destination 생성
4. 업로드 ZIP staging 저장
5. ZIP 검증
6. full ZIP packages 이동
7. DB transaction 시작
8. Release lock
9. 기존 Artifact lock
10. Artifact create 또는 replace
11. 감사 로그
12. commit
13. 기존 파일 정리

B11 수정 후 흐름:

1. 요청 검증
2. Release Draft 확인
3. Full ZIP destination 생성
4. 업로드 ZIP staging 저장
5. ZIP 검증
6. ZIP 내부 파일별 Manifest 생성 및 파일별 storage 저장
7. full ZIP packages 이동
8. DB transaction 시작
9. Release lock
10. 기존 Artifact lock
11. Artifact create 또는 replace
12. 기존 artifact files 삭제
13. 신규 artifact files 등록
14. 감사 로그
15. commit
16. 기존 full ZIP 및 기존 file storage 정리
17. 실패 시 신규 full ZIP 및 신규 file storage 정리

주의:

- DB transaction 동안 대용량 ZIP 읽기/추출을 하지 않는다.
- 파일별 저장은 DB transaction 이전에 수행하되, DB 실패 시 정리한다.
- 기존 Draft Artifact 교체 시 기존 file storage 정리가 필요하다.

## 11. UpdateCheckService 수정 범위

현재 `UpdateCheckService`는 product, currentVersion, os, architecture, channel을 검증하고, compatible full artifact 1건을 조회해 full package URL을 생성한다.

B11 수정 필요:

- `IUpdateArtifactFileRepository` 주입
- Compatible artifact 조회 후 active artifact files 조회
- 각 file storage key를 public download URL로 변환
- `UpdateCheckResponse.Files`에 매핑

주의:

- 파일별 URL 생성은 기존 `TryBuildPackageUrl`과 동일한 검증 기준을 사용한다.
- `PackageUrl`은 full ZIP URL로 유지한다.
- `Files`가 없는 기존 Published Artifact는 빈 배열을 내려주거나, 별도 manifest 생성 API가 구현되기 전까지 B11 대상에서 제외한다.

## 12. 감사 로그 수정 범위

기존 AuditActions:

- `CREATE`
- `UPDATE`
- `UPLOAD`
- `REPLACE_DRAFT_ARTIFACT`
- `PUBLISH`
- `DISABLE`
- `DELETE_DRAFT`

B11 추가 필요:

- `GENERATE_MANIFEST`

용도:

- 기존 Published ZIP을 변경하지 않고, ZIP을 분석해 manifest와 파일별 storage를 생성한 경우 기록한다.

주의:

- Draft 업로드 시 manifest 생성은 기존 `UPLOAD` 또는 `REPLACE_DRAFT_ARTIFACT` 감사의 afterData에 파일 수 정도만 포함해도 된다.
- Published artifact의 manifest 생성은 별도 감사 액션으로 남긴다.

## 13. API/Controller 수정 범위

초기 구현에서는 파일별 다운로드를 Nginx 정적 제공으로 유지한다.

따라서 신규 다운로드 Controller는 필수는 아니다.

초기 권장 URL:

```text
/packages/{product}/{channel}/{version}/{artifactPublicId}/files/{filePublicId}
```

단, 실제 저장 경로와 URL에는 원본 ZIP 내부 경로를 직접 노출하지 않는다.

향후 감사/권한/통계가 필요하면 다음 API를 별도 추가한다.

```text
GET /api/v1/updates/files/{filePublicId}/download
```

## 14. 테스트 추가 필요 항목

Repository 테스트:

- `UpdateArtifactFileRepositorySqlTests`
- 동일 artifact 내 file_path 중복 제약 확인
- public id/storage key unique 확인
- active files 조회 SQL 확인

Storage 테스트:

- ZIP 내부 파일별 SHA-256 계산
- Zip Slip Entry 거부
- 절대경로 Entry 거부
- directory entry 제외
- config/log/cache/token 제외
- exe/dll/providers/ffmpeg/mediamtx 포함
- 파일별 storage key가 안전한 segment만 가지는지 확인

Service 테스트:

- Artifact 업로드 시 full ZIP + artifact files 등록
- Draft Artifact 교체 시 기존 files 삭제 후 신규 files 등록
- DB 실패 시 신규 file storage 정리
- 기존 UpdateCheck 응답 호환성 유지
- 동일 버전 최신 상태에서도 files[]가 포함되는지 확인

Controller 테스트:

- `/api/v1/updates/check` 응답에 files[] 포함
- 기존 필드 `packageUrl`, `sha256` 유지

## 15. 수정 필요 파일 목록

### DB/문서

- `database/schema.sql`
- `database/migrations/2026xxxx_add_update_artifact_files.sql` 신규 권장
- `docs/api-contracts.md`
- `docs/storage-policy.md`
- `codex/WORK_STATUS.md`

### Domain/Enums/DTO

- `src/POSCAM.UpdateServer.Api/Models/Domain/UpdateDomainCodes.cs`
- `src/POSCAM.UpdateServer.Api/Models/Dtos/Updates/UpdateCheckResponse.cs`
- `src/POSCAM.UpdateServer.Api/Models/Dtos/Admin/Artifacts/ArtifactUploadDtos.cs` 선택
- `src/POSCAM.UpdateServer.Api/Models/Entities/UpdateArtifactFile.cs` 신규
- `src/POSCAM.UpdateServer.Api/Models/Enums/ArtifactFileStatus.cs` 신규 선택

### Repositories

- `src/POSCAM.UpdateServer.Api/Repositories/IUpdateArtifactFileRepository.cs` 신규
- `src/POSCAM.UpdateServer.Api/Repositories/UpdateArtifactFileRepository.cs` 신규
- `src/POSCAM.UpdateServer.Api/Repositories/UpdateReleaseRepository.cs` 선택

### Storage

- `src/POSCAM.UpdateServer.Api/Storage/IArtifactStorageService.cs`
- `src/POSCAM.UpdateServer.Api/Storage/ArtifactStorageService.cs`
- `src/POSCAM.UpdateServer.Api/Storage/ArtifactStorageService.Lifecycle.cs` 선택
- `src/POSCAM.UpdateServer.Api/Storage/IZipPackageValidator.cs` 선택
- `src/POSCAM.UpdateServer.Api/Storage/ZipPackageValidator.cs` 선택
- `src/POSCAM.UpdateServer.Api/Storage/IArtifactFileManifestService.cs` 신규 권장
- `src/POSCAM.UpdateServer.Api/Storage/ArtifactFileManifestService.cs` 신규 권장
- `src/POSCAM.UpdateServer.Api/Storage/ArtifactFileManifestEntry.cs` 신규 권장
- `src/POSCAM.UpdateServer.Api/Storage/StoredArtifactFile.cs` 신규 권장

### Services

- `src/POSCAM.UpdateServer.Api/Services/ArtifactUploadService.cs`
- `src/POSCAM.UpdateServer.Api/Services/ArtifactUploadService.Mapping.cs`
- `src/POSCAM.UpdateServer.Api/Services/UpdateCheckService.cs`
- `src/POSCAM.UpdateServer.Api/Services/ReleaseLifecycleService.cs` 선택

### DI

- `src/POSCAM.UpdateServer.Api/Program.cs`

### Tests

- `tests/POSCAM.UpdateServer.Tests/Repositories/UpdateArtifactFileRepositorySqlTests.cs` 신규
- `tests/POSCAM.UpdateServer.Tests/Storage/ArtifactFileManifestServiceTests.cs` 신규
- `tests/POSCAM.UpdateServer.Tests/Services/ArtifactUploadServiceTests.cs`
- `tests/POSCAM.UpdateServer.Tests/Services/UpdateCheckServiceTests.cs`
- `tests/POSCAM.UpdateServer.Tests/TestDoubles/FakeArtifactFileRepository.cs` 신규
- `tests/POSCAM.UpdateServer.Tests/TestDoubles/FakeArtifactStorageService.cs`

## 16. 구현 순서 제안

1. DB/Entity/Repository 추가
2. 파일별 Manifest 생성 정책과 테스트 추가
3. Storage에 파일별 저장/정리 기능 추가
4. ArtifactUploadService에 Manifest 생성 및 DB 저장 연결
5. UpdateCheckResponse에 files[] 추가
6. UpdateCheckService에서 artifact files 조회 및 URL 매핑
7. API 계약 문서 갱신
8. 테스트 보강
9. 빌드/테스트 검증
10. WORK_STATUS B11 완료 여부 갱신

## 17. 현재 단계 결론

B11은 진행 가능하다.

단, 기능 코드 수정 전 다음 정책을 확정해야 한다.

1. 서버는 `NeedRepair` 최종 판정을 하지 않고 manifest를 제공한다.
2. 클라이언트가 로컬 파일 검사 후 `NeedRepair`를 판단한다.
3. 기존 UpdateCheck 응답 필드는 유지하고 `files[]`만 추가한다.
4. 초기 파일별 다운로드는 Nginx 정적 제공을 유지한다.
5. API 다운로드는 향후 감사/통계/권한 요구가 생기면 별도 단계로 전환한다.

기능 코드 변경은 아직 수행하지 않았다.
