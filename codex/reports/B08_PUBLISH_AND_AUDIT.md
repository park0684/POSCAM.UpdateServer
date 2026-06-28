# B08 게시·중지·감사 구현 보고

## 작업 결과

- 상태: InProgress
- 구현: 완료
- 로컬 Release 빌드·전체 테스트: 확인 필요

## 변경 파일

### 게시·배포 중지·긴급 격리

- `Models/Dtos/Admin/Lifecycle/ReleaseLifecycleDtos.cs`: 게시·중지·격리 응답 DTO
- `Services/IReleaseLifecycleService.cs`: 상태 작업 계약
- `Services/ReleaseLifecycleService.cs`: Publish·Disable·긴급 Quarantine 업무 흐름
- `Services/ReleaseLifecycleService.Helpers.cs`: Actor 감사 스냅샷·응답·오류 처리
- `Controllers/AdminReleaseLifecycleController.cs`: Publish·Disable API
- `Controllers/AdminArtifactLifecycleController.cs`: 긴급 Artifact 격리 API

### 게시 전 무결성 검증·격리 Storage

- `Storage/ArtifactStorageFailureType.cs`: `PackageIntegrityError` 분류
- `Storage/QuarantinedArtifactFile.cs`: 격리·복구 상태 모델
- `Storage/IArtifactStorageService.cs`: 저장 Artifact 검증·격리·복구 계약
- `Storage/ArtifactStorageService.Lifecycle.cs`: 존재·크기·SHA-256·ZIP 재검증과 격리·복구

### 감사 조회

- `Models/Queries/AuditSearchCriteria.cs`: 감사 목록 필터
- `Models/Dtos/Admin/Audits/AuditDtos.cs`: 감사 조회 요청·응답 DTO
- `Repositories/IAuditManagementQueryRepository.cs`: 감사 조회 계약
- `Repositories/AuditManagementQueryRepository.cs`: 전체 감사 목록·릴리스 이력 SQL
- `Services/IAuditQueryService.cs`: 감사 조회 서비스 계약
- `Services/AuditQueryService.cs`: 필터 검증·UTC·페이징
- `Controllers/AdminAuditsController.cs`: 전체 감사 목록·릴리스 이력 API

### Repository·DI·문서

- `Repositories/IArtifactManagementQueryRepository.cs`: ArtifactCode 잠금 조회 추가
- `Repositories/ArtifactManagementQueryRepository.cs`: ArtifactCode `FOR UPDATE`
- `Program.cs`: Lifecycle·Audit 서비스와 Repository DI
- `docs/api-contracts.md`: B08 API·상태 작업·감사 필터
- `docs/storage-policy.md`: 일반 Disable과 긴급 Quarantine 파일 정책

### 테스트

- 게시 성공과 PublishedAt UTC
- 활성 Artifact 없음 차단
- 파일 없음·크기·SHA-256·ZIP 무결성 실패
- Published·Disabled 재게시 차단
- 일반 Disable 시 package 파일 유지
- Draft·Disabled 잘못된 상태 전이 차단
- Artifact 긴급 격리와 Published Release 동시 중지
- DB 실패 시 격리 파일 원위치 복구
- 이미 없는 파일의 긴급 배포 차단
- Draft Artifact 긴급 격리 차단
- Storage 오류 매핑
- 감사 필터·UTC·페이징
- 릴리스와 연결 Artifact 이력 조회
- 상태 전이·Artifact 잠금·감사 조회 SQL 계약
- 모든 B08 Controller의 관리자 보호 경로

## API

```text
POST /api/v1/admin/releases/{releaseCode}/publish
POST /api/v1/admin/releases/{releaseCode}/disable
POST /api/v1/admin/artifacts/{artifactCode}/quarantine
GET  /api/v1/admin/audit-logs
GET  /api/v1/admin/releases/{releaseCode}/audit-logs
```

모든 API는 `/api/v1/admin` 경로 아래에 있으며 B05 관리자 권한 Middleware를 통과한다.

## 게시 흐름

```text
관리자 인증
→ Release FOR UPDATE
→ Draft 상태 확인
→ 활성 Artifact 조회
→ 각 Artifact 파일 존재 확인
→ 실제 파일 크기 확인
→ SHA-256 재계산·대조
→ ZIP 전체 Entry 재검증
→ Draft → Published
→ PublishedAt UTC 저장
→ PUBLISH 감사 로그
→ Commit
```

게시 전 무결성 검증 실패 시 상태를 변경하지 않고 다음 응답을 반환한다.

```text
HTTP 409
ErrorCode 8033 PackageIntegrityError
```

활성 Artifact가 없는 Draft는 `409 / 8021 NoCompatibleArtifact`로 게시를 차단한다.

## 일반 배포 중지

```text
Published → Disabled
```

일반 배포 중지는 다음 항목을 변경하지 않는다.

- Artifact 상태
- package 파일
- Public ID
- Storage Key
- 기존 다운로드 URL의 물리 파일

새 Update Check에서는 Disabled Release가 제외되지만 기존 파일은 보존한다. `DISABLE` 감사 로그를 기록한다.

## 긴급 Artifact 격리

```text
Release FOR UPDATE
→ Artifact FOR UPDATE
→ packages 파일을 .quarantine으로 이동
→ Artifact Disabled
→ Published Release이면 Release도 Disabled
→ Artifact DISABLE 감사
→ Release DISABLE 감사
→ Commit
```

감사 After JSON에는 다음 원인을 저장한다.

- Artifact: `EMERGENCY_QUARANTINE`
- Release: `EMERGENCY_ARTIFACT_QUARANTINE`

파일이 이미 없으면 `storageState=MISSING`으로 기록하고 DB의 배포 대상에서 제외한다.

DB 상태 반영이 Commit 전에 실패하면 격리 파일을 원래 packages 위치로 복구한다. 복구 실패는 Critical 로그로 남기며 토큰·서비스 키·물리 경로를 외부 응답에 노출하지 않는다.

## 상태 전이 정책

- Draft → Published: 허용
- Published → Disabled: 허용
- Published → Draft: 금지
- Disabled → Published: 금지
- Draft → Disabled: 금지
- Published 핵심 정보·Artifact 교체: 금지

## 감사 조회

전체 감사 목록 필터:

- `action`
- `targetType`
- `targetCode`
- `actorUserCode`
- `requestId`
- `fromUtc`
- `toUtc`
- `page`
- `pageSize`

릴리스별 감사 이력은 다음을 함께 반환한다.

- 해당 Release를 Target으로 저장한 감사 로그
- 현재 해당 Release에 연결된 Artifact를 Target으로 저장한 감사 로그

정렬은 `CreatedAt DESC, AuditLogCode DESC`이며 감사 로그 수정·삭제 API는 제공하지 않는다.

## 오류 계약

| 상황 | HTTP | ErrorCode |
|---|---:|---:|
| Release 없음 | 404 | 8010 |
| Artifact 없음 | 404 | 8020 |
| 잘못된 상태 전이 | 409 | 8012 |
| 활성 Artifact 없음 | 409 | 8021 |
| 게시 전 무결성 실패 | 409 | 8033 |
| 긴급 격리 Storage 실패 | 500 | 8032 |
| 감사 조회 입력 오류 | 400 | 9001 |

## 검증 결과

- 실행 명령:
  - `dotnet restore POSCAM.UpdateServer.sln`
  - `dotnet build POSCAM.UpdateServer.sln -c Release`
  - `dotnet test POSCAM.UpdateServer.sln -c Release --no-build`
- Build: 로컬 확인 필요
- Test: 로컬 확인 필요
- 예상 전체 테스트 수: 약 257개

## 남은 문제

- 컴파일 오류: 로컬 검증 필요
- 실제 동작 오류: 실제 MariaDB·Nginx·Docker volume 통합 검증은 후속 단계 필요
- 불필요한 중복: 정적 검토 완료
- 다음 단계 선행조건: B08 Release 빌드·전체 테스트 성공

## 정책 이탈 여부

- 없음
