# B12 클라이언트 Manifest 파일 복구 업데이트 구현 프롬프트

## 1. 작업 개요

UpdateServer B11에서 공개 Update Check 응답에 `files[]` Manifest가 추가되었다. B12는 클라이언트 Updater가 이 `files[]`를 이용해 로컬 파일을 검사하고, 누락 또는 손상된 파일만 복구할 수 있도록 클라이언트 쪽 구현을 준비하는 작업이다.

이 프롬프트는 실제 클라이언트 저장소에서 구현할 때 사용한다.

## 2. 전제

서버는 다음 정책을 따른다.

- Full ZIP 업데이트 응답 필드는 기존대로 유지한다.
- `files[]`는 compatible Artifact 기준 파일별 Manifest이다.
- 동일 버전 `ALREADY_LATEST`에서도 `files[]`가 내려올 수 있다.
- 서버는 `NeedRepair`를 판단하지 않는다.
- 클라이언트가 로컬 파일 상태를 검사해 복구 필요 여부를 판단한다.

참조 문서:

```text
docs/client-manifest-repair-update-policy.md
docs/manifest-repair-update-policy.md
docs/api-contracts.md
codex/reports/B11_COMPLETION_REPORT.md
```

## 3. 구현 목표

클라이언트는 다음 흐름을 구현해야 한다.

```text
1. Update Check 호출
2. updateAvailable=true이면 기존 Full ZIP 업데이트 흐름 우선
3. updateAvailable=false여도 files[]가 있으면 로컬 파일 검사
4. files[].path 기준 로컬 파일 존재 확인
5. 파일이 없거나 크기/SHA-256이 다르면 복구 대상으로 등록
6. 복구 대상 파일만 files[].downloadUrl로 다운로드
7. 다운로드 파일 크기/SHA-256 재검증
8. 별도 Updater 프로세스에서 백업 후 교체
9. 교체 후 재검증
10. 실패 시 rollback
```

## 4. 구현 우선순위

### 4.1 1단계: DTO 추가

Update Check 응답 DTO에 `files[]`를 추가한다.

```csharp
public sealed class UpdateManifestFile
{
    public string Path { get; set; } = "";
    public long Size { get; set; }
    public string Sha256 { get; set; } = "";
    public bool Required { get; set; }
    public string DownloadUrl { get; set; } = "";
}
```

기존 응답 DTO에는 아래 필드를 추가한다.

```csharp
public List<UpdateManifestFile> Files { get; set; } = new();
```

기존 서버와의 호환을 위해 `files`가 없어도 null 예외가 나지 않도록 처리한다.

### 4.2 2단계: ManifestRepairPlanner 구현

역할:

- `files[]` 유효성 검사
- path 안전성 검사
- 설치 루트 기준 실제 경로 계산
- 파일 존재 여부 확인
- size 비교
- SHA-256 비교
- 복구 대상 목록 생성

권장 결과 모델:

```csharp
public sealed class RepairPlan
{
    public bool HasRepairTargets { get; init; }
    public IReadOnlyList<RepairTarget> Targets { get; init; } = Array.Empty<RepairTarget>();
}

public sealed class RepairTarget
{
    public string RelativePath { get; init; } = "";
    public string LocalPath { get; init; } = "";
    public long ExpectedSize { get; init; }
    public string ExpectedSha256 { get; init; } = "";
    public string DownloadUrl { get; init; } = "";
    public string Reason { get; init; } = "";
}
```

복구 사유:

```text
Missing
SizeMismatch
HashMismatch
```

### 4.3 3단계: 경로 안전성 검사

거부 조건:

```text
빈 path
절대 경로
드라이브 문자 포함
.. segment 포함
설치 루트 밖 경로
NUL 문자 포함
```

상대 경로는 `/`와 `\`를 모두 처리하되, 최종적으로 OS 경로로 안전하게 변환한다.

### 4.4 4단계: SHA-256 검사

파일 크기가 다르면 SHA-256 계산을 생략할 수 있다.

파일 크기가 같으면 SHA-256을 계산해 서버 값과 비교한다.

SHA-256 문자열은 대소문자 구분 없이 비교한다.

### 4.5 5단계: 다운로드/검증

다운로드 파일은 원본 위치에 직접 저장하지 않는다.

임시 경로 예:

```text
{설치루트}\_update\downloads\{작업ID}\{safeRelativePath}
```

다운로드 후 검증:

```text
파일 크기 == expected size
SHA-256 == expected sha256
```

검증 실패 시 원본 파일은 변경하지 않는다.

### 4.6 6단계: 별도 Updater 프로세스

실행 중인 EXE/DLL은 메인 프로그램이 직접 덮어쓰지 않는다.

권장 실행 파일:

```text
POSCAM.UpdateClient.exe
```

Updater 역할:

```text
메인 프로그램 종료 대기
기존 파일 백업
다운로드 파일 교체
교체 후 SHA-256 검증
실패 시 rollback
메인 프로그램 재실행
로그 기록
```

초기 버전에서는 메인 프로세스 강제 종료를 사용하지 않는다.

## 5. Full ZIP 업데이트와 파일 복구 우선순위

```text
updateAvailable=true
→ 기존 Full ZIP 업데이트 우선
→ 부분 복구는 수행하지 않음

updateAvailable=false + files[] 있음
→ 로컬 파일 검사
→ 복구 대상이 있으면 부분 복구

files[] 없음
→ 부분 복구 수행하지 않음
```

## 6. 테스트 항목

필수 테스트:

```text
files[]가 없으면 복구 대상 없음
files[]가 비어 있으면 복구 대상 없음
파일이 없으면 Missing 대상
파일 크기가 다르면 SizeMismatch 대상
파일 크기는 같지만 SHA-256이 다르면 HashMismatch 대상
파일 크기/SHA-256이 같으면 복구 대상 아님
위험 path는 예외 또는 실패 결과
동일 버전 ALREADY_LATEST에서도 files[] 검사 수행
updateAvailable=true이면 Full ZIP 우선
다운로드 후 SHA-256 불일치 시 원본 미변경
교체 실패 시 rollback 수행
```

## 7. 구현 시 주의사항

- 서버 응답이라고 해도 path를 신뢰하지 않는다.
- 원본 파일을 다운로드 파일로 바로 덮어쓰지 않는다.
- 검증 실패 파일은 적용하지 않는다.
- 일부만 교체된 상태를 정상으로 간주하지 않는다.
- 로그에는 민감 URL 전체를 남기지 않는다.
- 설치 경로가 보호 경로이면 관리자 권한 처리를 고려한다.

## 8. 이번 프롬프트에서 하지 말 것

- 서버 API 변경
- UpdateServer DB schema 변경
- B11 서버 코드 수정
- 클라이언트 로컬 파일 상태를 서버로 보내는 API 추가
- 서버에서 NeedRepair 판단 추가
- 기존 Artifact backfill 구현

## 9. 완료 기준

```text
DTO 추가 완료
ManifestRepairPlanner 구현 완료
로컬 파일 검사 테스트 완료
다운로드/검증 테스트 완료
Updater 프로세스 교체 정책 반영
빌드 성공
테스트 성공
```
