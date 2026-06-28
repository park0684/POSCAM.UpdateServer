# B07 Artifact 업로드 구현 보고

## 작업 결과

- 상태: InProgress
- 구현: 완료
- 로컬 Release 빌드·전체 테스트: 확인 필요

## 변경 파일

### Storage

- `Storage/IArtifactStorageService.cs`: staging·최종 이동·실패 정리 계약
- `Storage/ArtifactStorageService.cs`: Stream 저장, 실제 크기·SHA-256 계산, packages 이동, 삭제·격리
- `Storage/IZipPackageValidator.cs`: ZIP 검증 계약
- `Storage/ZipPackageValidator.cs`: ZIP 구조·Entry Stream·Zip Slip·절대경로·항목 수·Expanded Bytes 검증
- `Storage/StagedArtifactFile.cs`: staging 결과
- `Storage/ArtifactStorageDestination.cs`: Public ID·서버 파일명·Storage Key
- `Storage/ArtifactStorageException.cs`: 안전한 Storage 오류 분류

### Upload API

- `Models/Dtos/Admin/Artifacts/ArtifactUploadDtos.cs`: multipart 요청·응답
- `Services/IArtifactUploadService.cs`: 업로드 업무 계약
- `Services/ArtifactUploadService.cs`: 신규·교체·트랜잭션·실패 정리
- `Services/ArtifactUploadService.Validation.cs`: 입력·상태·오류 코드 검증
- `Services/ArtifactUploadService.Mapping.cs`: Entity·응답·감사 로그
- `Controllers/AdminArtifactsController.cs`: `POST /api/v1/admin/releases/{releaseCode}/artifacts`

### DB·DI·예외 처리

- `Repositories/IArtifactManagementQueryRepository.cs`: Artifact 잠금 조회 계약
- `Repositories/ArtifactManagementQueryRepository.cs`: Target 기준 `FOR UPDATE`
- `Program.cs`: Storage·업로드 DI, Kestrel·multipart 크기 제한
- `GlobalExceptionHandlingMiddleware.cs`: 요청 본문 한도 초과를 `413 / 8031`로 유지

### 테스트

- 정상 ZIP과 SHA-256
- 손상 ZIP
- Zip Slip·절대경로·Windows Drive·UNC·NTFS ADS
- ZIP Entry 수 제한
- Expanded Bytes 제한
- Storage Root 탈출 차단
- 실제 Stream 크기 초과와 staging 정리
- 신규 Artifact 생성
- Draft Artifact 교체
- Published·Disabled 업로드 차단
- 업로드 중 상태·릴리스 식별값 변경 차단
- DB Unique 충돌 후 새 파일 정리
- 교체 실패 시 기존 파일 유지
- 관리자 보호 경로
- Artifact Target `FOR UPDATE`
- 413 전역 오류 매핑

## 업로드 처리 흐름

```text
관리자 인증
→ 요청·Draft 사전 확인
→ 서버 Public ID·파일명·Storage Key 생성
→ .staging Stream 저장
→ 실제 크기·SHA-256 계산
→ ZIP 전체 Entry Stream 검증
→ packages 원자적 이동
→ DB 트랜잭션 시작
→ Release FOR UPDATE 및 상태·식별값 재확인
→ Artifact Target FOR UPDATE
→ 신규 INSERT 또는 Draft 교체 UPDATE
→ UPLOAD / REPLACE_DRAFT_ARTIFACT 감사 로그
→ Commit
→ 교체인 경우 기존 파일 정리
```

## 오류 계약

| 상황 | HTTP | ErrorCode |
|---|---:|---:|
| 잘못된 OS | 400 | 8004 |
| 잘못된 Architecture | 400 | 8005 |
| 잘못된 Package Type | 400 | 9001 |
| Release 없음 | 404 | 8010 |
| Draft 아님·동시 변경 | 409 | 8012 |
| Artifact Unique 충돌 | 409 | 8022 |
| 파일 크기 초과 | 413 | 8031 |
| 손상·위험 ZIP | 415 | 8030 |
| Storage 오류 | 500 | 8032 |

## 안전성 정책

- 업로드 전체를 `byte[]`로 적재하지 않는다.
- 128KB 임대 Buffer로 Stream 처리한다.
- 원본 파일명은 경로에 사용하지 않는다.
- 원본 이름은 `.zip` 접미사 확인에만 사용한다.
- 서버가 Public ID·파일명·Storage Key를 생성한다.
- 실제 파일 크기와 SHA-256은 서버가 계산한다.
- ZIP을 서버에서 추출하거나 실행하지 않는다.
- Published·Disabled 릴리스는 업로드·교체할 수 없다.
- DB 반영 실패 시 새 파일을 삭제하거나 quarantine으로 이동한다.
- 교체 시 이전 파일은 DB Commit 이후에만 정리한다.
- 물리 경로는 API 응답과 일반 로그에 노출하지 않는다.

## 검증 결과

- 실행 명령:
  - `dotnet restore POSCAM.UpdateServer.sln`
  - `dotnet build POSCAM.UpdateServer.sln -c Release`
  - `dotnet test POSCAM.UpdateServer.sln -c Release --no-build`
- Build: 로컬 확인 필요
- Test: 로컬 확인 필요
- 예상 전체 테스트 수: 약 215개

## 남은 문제

- 컴파일 오류: 로컬 검증 필요
- 실제 동작 오류: 실제 MariaDB·Nginx 연동은 후속 통합 검증 필요
- 불필요한 중복: 정적 검토 완료
- 다음 단계 선행조건: B07 Release 빌드·전체 테스트 성공

## 정책 이탈 여부

- 없음
