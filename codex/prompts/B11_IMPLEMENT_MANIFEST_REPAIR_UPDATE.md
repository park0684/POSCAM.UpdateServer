# B11 - Manifest 파일별 복구 업데이트

## 1. 작업 목적

Manifest 기반 파일별 복구 업데이트를 구현한다.

현재 업데이트 구조는 로컬 버전이 서버 버전보다 낮은 경우 전체 패키지 다운로드를 전제로 한다. 이번 단계에서는 서버 버전과 로컬 버전이 동일하더라도 필수 파일 누락 또는 SHA-256 불일치가 있으면 `NeedRepair`로 판단하고, 필요한 파일만 개별 다운로드할 수 있도록 서버·클라이언트 계약을 확장한다.

## 2. 반드시 먼저 읽을 문서

- `AGENTS.md`
- `README.md`
- `docs/architecture.md`
- `docs/domain-policy.md`
- `docs/manifest-repair-update-policy.md`
- `database/schema.sql`
- `codex/WORK_STATUS.md`
- 기존 B04, B07, B08 완료 결과

문서와 현재 코드가 충돌하면 임의 구현하지 말고 `Blocked`로 보고한다.

## 3. 작업 전 현재 상태 확인

다음 상태를 먼저 확인하고 보고한다.

- 현재 브랜치
- `git status --short`
- Release 모델/테이블 구조
- Artifact 모델/테이블 구조
- Upload Service/Controller 구조
- Update Check Request/Response 구조
- Storage Options 구조
- 패키지 저장 경로
- Published Artifact 처리 정책

사용자 확인 전 기능 코드를 수정하지 않는다.

## 4. 수정 허용 범위

이번 단계에서 필요한 경우에만 다음 파일군을 수정한다.

- DB migration 또는 schema 문서
- Artifact File Manifest Entity/DTO/Repository
- Storage Service
- ZIP Validator
- SHA-256 Stream 처리
- Upload Service/Controller
- Update Check Service/Controller
- Public response DTO
- 감사 로그 타입
- 테스트
- 작업 상태 문서

목록 밖 파일이 반드시 필요하면 먼저 이유와 경로를 보고한다.

## 5. 금지 사항

- 사용자 확인 전 기능 코드 수정 금지
- Published ZIP 파일 덮어쓰기 금지
- Published Artifact 핵심정보 변경 금지
- AuthServer DB 직접 조회 금지
- TokenSecret 출력 금지
- 업로드 ZIP 전체를 불필요하게 byte[]로 메모리에 적재 금지
- 원본 ZIP Entry 경로를 그대로 신뢰 금지
- 절대경로, `../`, Zip Slip 가능 경로 허용 금지
- 사용자 설정, 토큰, 로그, 캐시 파일을 복구 대상에 포함 금지
- 다운로드 파일 SHA 검증 전 적용 금지
- 실행 중인 EXE/DLL 직접 덮어쓰기 금지
- 빌드 또는 테스트 실패 상태에서 다음 단계 이동 금지

## 6. 구현 요구사항

### 6.1 DB/모델

- Artifact별 파일 Manifest를 저장할 수 있어야 한다.
- 파일 상대경로, 크기, SHA-256, 필수 여부, 파일 상태, 파일별 StorageKey 또는 PublicId를 저장한다.
- 동일 Artifact 내 동일 파일 경로는 중복될 수 없다.
- Published Artifact에 대해 Manifest를 생성하더라도 ZIP 원본은 변경하지 않는다.

권장 테이블명:

```text
update_artifact_files
```

### 6.2 ZIP 업로드

Draft Artifact ZIP 업로드 시 다음을 수행한다.

- ZIP 자체 크기와 SHA-256 계산
- ZIP Entry 유효성 검증
- 절대경로/Zip Slip/비정상 Entry 차단
- 파일별 상대경로 정규화
- 파일별 크기와 SHA-256 계산
- Manifest 포함/제외 대상 판단
- 포함 대상 파일별 저장소 생성
- DB 등록 실패 시 staging 및 생성 파일 정리

### 6.3 기존 Published Artifact Manifest 생성

기존 Published Artifact에 Manifest가 없는 경우 다음 방식으로 처리한다.

- ZIP 원본은 변경하지 않는다.
- ZIP을 분석해서 Manifest와 파일별 저장소만 생성한다.
- 감사 로그에 `GENERATE_MANIFEST`를 남긴다.
- 실패 시 Manifest 생성 전 상태로 정리한다.

### 6.4 Update Check

Update Check 응답은 다음 판단에 필요한 정보를 제공해야 한다.

- 최신 버전
- 업데이트 상태
- 전체 패키지 URL과 SHA-256
- 파일별 Manifest 또는 Manifest URL
- 파일별 다운로드 URL

업데이트 상태는 최소 다음 값을 지원한다.

- `NoUpdate`
- `NeedUpdate`
- `NeedRepair`
- `ForceUpdate`
- `Blocked`

서버가 클라이언트 로컬 파일을 직접 볼 수 없으므로 `NeedRepair` 최종 판단은 클라이언트가 Manifest 비교 후 결정할 수 있다. 서버 응답은 클라이언트가 이 판단을 수행할 수 있을 만큼 충분한 Manifest 정보를 제공해야 한다.

### 6.5 파일별 다운로드

- 실제 서버 파일 경로를 노출하지 않는다.
- 파일별 PublicId 또는 StorageKey를 사용한다.
- 정적 파일 제공 또는 API 다운로드 중 현재 구조에 맞는 방식을 선택한다.
- 다운로드 대상 파일의 SHA-256은 Manifest와 일치해야 한다.

### 6.6 클라이언트 계약

클라이언트는 다음 흐름을 따른다.

1. Update Check 응답 수신
2. Manifest 기준 로컬 파일 검사
3. 누락/손상 파일 목록 생성
4. 필요한 파일만 다운로드
5. 다운로드 파일 SHA-256 검증
6. 기존 파일 백업
7. 파일 교체
8. 교체 후 SHA-256 재검증
9. 실패 시 롤백

## 7. 단계별 체크리스트

### 7.1 기준 문서 단계

- [ ] `docs/manifest-repair-update-policy.md` 존재
- [ ] `codex/prompts/B11_IMPLEMENT_MANIFEST_REPAIR_UPDATE.md` 존재
- [ ] `codex/WORK_STATUS.md`에 B11 추가
- [ ] 기능 코드 변경 없음

### 7.2 현재 코드 분석 단계

- [ ] Release 구조 확인
- [ ] Artifact 구조 확인
- [ ] Upload 구조 확인
- [ ] Update Check 구조 확인
- [ ] Storage 구조 확인
- [ ] Audit 구조 확인
- [ ] 수정 필요 파일 목록 작성
- [ ] 사용자 확인 완료

### 7.3 DB/모델 단계

- [ ] Manifest 저장 구조 확정
- [ ] Migration 작성
- [ ] Entity/DTO 작성
- [ ] Repository 작성
- [ ] 중복 파일 경로 제약 확인
- [ ] 테스트 작성

### 7.4 업로드/저장 단계

- [ ] ZIP Entry 검증
- [ ] 파일별 SHA-256 계산
- [ ] 포함/제외 대상 판단
- [ ] 파일별 저장소 생성
- [ ] DB 실패 시 정리
- [ ] 테스트 작성

### 7.5 Update Check 단계

- [ ] Manifest 응답 추가
- [ ] 파일별 다운로드 URL 추가
- [ ] 기존 응답 호환성 확인
- [ ] 테스트 작성

### 7.6 파일별 다운로드 단계

- [ ] 다운로드 경로 구현
- [ ] 경로 노출 방지
- [ ] SHA 기준 검증 가능
- [ ] 테스트 작성

### 7.7 최종 검증 단계

- [ ] 동일 버전 + 정상 파일: `NoUpdate`
- [ ] 동일 버전 + 파일 누락: `NeedRepair`
- [ ] 동일 버전 + 파일 손상: `NeedRepair`
- [ ] 낮은 로컬 버전: `NeedUpdate`
- [ ] 강제 업데이트: `ForceUpdate`
- [ ] 다운로드 파일 SHA 불일치: 적용 중단
- [ ] 사용자 설정/로그 파일 제외
- [ ] Zip Slip 업로드 거부
- [ ] Published ZIP 원본 변경 없음

## 8. 빌드 및 테스트

다음 명령이 성공해야 한다.

```powershell
dotnet build POSCAM.UpdateServer.sln -c Release
```

```powershell
dotnet test POSCAM.UpdateServer.sln -c Release --no-build
```

실패 상태에서 다음 단계로 이동하지 않는다.

## 9. 완료 조건

- 정책 문서와 구현이 일치한다.
- Manifest DB/모델/Repository가 구현되어 있다.
- ZIP 업로드 시 파일별 Manifest가 생성된다.
- 필요한 파일만 개별 다운로드할 수 있다.
- Published ZIP 원본이 변경되지 않는다.
- 기존 Update Check 기능이 깨지지 않는다.
- 동일 버전 파일 누락/손상 복구 시나리오가 테스트된다.
- 빌드와 테스트가 성공한다.

## 10. 완료 보고 형식

`codex/COMPLETION_REPORT_TEMPLATE.md` 형식을 사용하고 `codex/WORK_STATUS.md`의 B11 단계만 갱신한다.
