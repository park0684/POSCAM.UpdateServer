# B12 클라이언트 Manifest 파일 복구 업데이트 구현 프롬프트

## 1. 작업 개요

UpdateServer B11에서 공개 Update Check 응답에 `files[]` Manifest가 추가되었다. B12는 `PcCam.exe`가 시작 시 `POSCAM.UpdateClient.exe`를 호출해 업데이트 및 파일 무결성을 검증하고, 필요한 경우 무인으로 다운로드/교체 후 `PcCam.exe`를 재실행할 수 있도록 클라이언트 쪽 구현을 준비하는 작업이다.

## 2. 전제

서버는 다음 정책을 따른다.

- Full ZIP 업데이트 응답 필드는 기존대로 유지한다.
- `files[]`는 compatible Artifact 기준 파일별 Manifest이다.
- 동일 버전 `ALREADY_LATEST`에서도 `files[]`가 내려올 수 있다.
- 서버는 `NeedRepair`를 판단하지 않는다.
- 클라이언트가 로컬 파일 상태를 검사해 복구 필요 여부를 판단한다.

클라이언트 실행 정책은 다음과 같다.

- `PcCam.exe`가 먼저 실행된다.
- `PcCam.exe`는 본 기능 시작 전에 `POSCAM.UpdateClient.exe startup-check`를 호출한다.
- 업데이트/복구가 필요 없으면 `PcCam.exe`는 그대로 본 기능을 시작한다.
- 업데이트/복구가 필요하면 `PcCam.exe`는 `POSCAM.UpdateClient.exe apply`를 호출한 뒤 종료한다.
- `UpdateClient`가 파일 교체 후 `PcCam.exe`를 재실행한다.
- 사용자 확인창, 시스템 트레이 알림, 콘솔창 표시 없이 동작한다.
- 결과는 exit code, 작업 파일, 로그로만 전달한다.

참조 문서:

```text
docs/client-manifest-repair-update-policy.md
docs/manifest-repair-update-policy.md
docs/api-contracts.md
codex/reports/B11_COMPLETION_REPORT.md
```

## 3. 프로젝트 형태

Windows 7 32비트 호환을 전제로 한다.

```xml
<TargetFramework>net48</TargetFramework>
<PlatformTarget>x86</PlatformTarget>
<Prefer32Bit>true</Prefer32Bit>
<OutputType>WinExe</OutputType>
```

`OutputType=WinExe`를 사용해 콘솔창이 뜨지 않도록 한다. UI는 만들지 않는다.

## 4. 명령 구조

### 4.1 startup-check

`PcCam.exe`가 본 기능 시작 전에 호출한다.

역할:

```text
Update Check 호출
Full ZIP 업데이트 필요 여부 확인
files[] 기반 로컬 파일 검사
필요한 파일 다운로드
다운로드 파일 검증
적용 계획 파일 생성
exit code 반환
```

### 4.2 apply

업데이트/복구 적용이 필요할 때 `PcCam.exe`가 호출한 뒤 자기 자신을 종료한다.

역할:

```text
PcCam.exe 종료 대기
기존 파일 백업
새 파일 교체
교체 후 SHA-256 재검증
실패 시 rollback
PcCam.exe 재실행
로그 기록
```

## 5. exit code 정책

```text
0  = 검증 성공, 업데이트/복구 필요 없음, PcCam 계속 실행
10 = 업데이트 또는 복구 적용 필요, PcCam은 apply 실행 후 종료
20 = 검증 실패, PcCam 본 기능 시작 금지
30 = Update Check 실패, PcCam 본 기능 시작 금지
40 = 다운로드 실패, PcCam 본 기능 시작 금지
50 = 적용 실패, PcCam 재실행 금지
```

## 6. 구현 우선순위

### 6.1 1단계: 명령/exit code 골격 수정

현재 placeholder 명령을 아래 구조로 바꾼다.

```text
startup-check
apply
```

exit code 상수를 정의한다.

### 6.2 2단계: DTO 추가

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

기존 서버와의 호환을 위해 `files`가 없어도 null 예외가 나지 않도록 처리한다.

### 6.3 3단계: ManifestRepairPlanner 구현

역할:

```text
files[] 유효성 검사
path 안전성 검사
설치 루트 기준 실제 경로 계산
파일 존재 여부 확인
size 비교
SHA-256 비교
복구 대상 목록 생성
```

권장 결과 모델:

```csharp
public sealed class RepairPlan
{
    public bool HasRepairTargets { get; set; }
    public List<RepairTarget> Targets { get; set; } = new List<RepairTarget>();
}

public sealed class RepairTarget
{
    public string RelativePath { get; set; } = "";
    public string LocalPath { get; set; } = "";
    public long ExpectedSize { get; set; }
    public string ExpectedSha256 { get; set; } = "";
    public string DownloadUrl { get; set; } = "";
    public string Reason { get; set; } = "";
}
```

복구 사유:

```text
Missing
SizeMismatch
HashMismatch
```

### 6.4 4단계: 경로 안전성 검사

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

### 6.5 5단계: SHA-256 검사

파일 크기가 다르면 SHA-256 계산을 생략할 수 있다.

파일 크기가 같으면 SHA-256을 계산해 서버 값과 비교한다.

SHA-256 문자열은 대소문자 구분 없이 비교한다.

### 6.6 6단계: 다운로드/검증

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

### 6.7 7단계: apply 교체/rollback

실행 중인 EXE/DLL은 메인 프로그램이 직접 덮어쓰지 않는다.

`apply` 명령에서 수행한다.

```text
PcCam.exe 종료 대기
기존 파일 백업
다운로드 파일 교체
교체 후 SHA-256 검증
실패 시 rollback
성공 시 PcCam.exe 재실행
```

초기 버전에서는 메인 프로세스 강제 종료를 사용하지 않는다.

## 7. Full ZIP 업데이트와 파일 복구 우선순위

```text
updateAvailable=true
→ 기존 Full ZIP 업데이트 우선
→ 부분 복구는 수행하지 않음
→ 다운로드/검증 후 apply 대상 생성

updateAvailable=false + files[] 있음
→ 로컬 파일 검사
→ 복구 대상이 있으면 필요한 파일만 다운로드/검증
→ apply 대상 생성

files[] 없음
→ 부분 복구 수행하지 않음
```

## 8. 테스트 항목

필수 테스트:

```text
startup-check 명령 인식
apply 명령 인식
알 수 없는 명령은 실패 exit code
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

## 9. 구현 시 주의사항

- 서버 응답이라고 해도 path를 신뢰하지 않는다.
- 원본 파일을 다운로드 파일로 바로 덮어쓰지 않는다.
- 검증 실패 파일은 적용하지 않는다.
- 일부만 교체된 상태를 정상으로 간주하지 않는다.
- 로그에는 민감 URL 전체를 남기지 않는다.
- MessageBox를 사용하지 않는다.
- NotifyIcon을 사용하지 않는다.
- Console 창이 뜨면 안 된다.
- 설치 경로가 보호 경로이면 관리자 권한 처리를 고려한다.

## 10. 이번 프롬프트에서 하지 말 것

- 서버 API 변경
- UpdateServer DB schema 변경
- B11 서버 코드 수정
- 클라이언트 로컬 파일 상태를 서버로 보내는 API 추가
- 서버에서 NeedRepair 판단 추가
- 기존 Artifact backfill 구현
- 시스템 트레이 알림 구현
- 사용자 확인 UI 구현

## 11. 완료 기준

```text
net48/x86/WinExe 유지
startup-check/apply 명령 구조 반영
exit code 정책 반영
DTO 추가 완료
ManifestRepairPlanner 구현 완료
로컬 파일 검사 테스트 완료
다운로드/검증 테스트 완료
apply 교체/rollback 정책 반영
빌드 성공
테스트 성공
```
