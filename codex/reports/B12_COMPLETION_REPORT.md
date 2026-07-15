# B12 클라이언트 Manifest 복구 업데이트 완료 보고서

## 1. 작업 목적

B12의 목적은 `POSCAM.UpdateClient.exe`가 UpdateServer의 공개 Update Check 응답을 사용해 다음 작업을 무인으로 수행하도록 구현하는 것이다.

```text
Update Check
→ Full Package 업데이트 판단
→ files[] Manifest 기반 로컬 파일 검사
→ 필요한 파일 다운로드 및 검증
→ 적용 계획 저장
→ 대상 프로그램 종료 대기
→ 백업·교체·재검증
→ 실패 시 rollback
→ 성공 시 대상 프로그램 재실행
```

UpdateClient는 별도 UI나 트레이를 제공하지 않는 `net48 / x86 / WinExe` 보조 프로세스다. x86 UpdateClient 하나를 PcCam x86, PcCam x64, CamViewer x64에서 공용으로 사용한다.

## 2. 최종 결론

B12의 UpdateClient 구현과 사용자 로컬 검증이 완료되었다.

최종 검증 결과:

```text
Release 빌드 성공
빌드 경고 0
빌드 오류 0
테스트 450/450 성공
실패 0
건너뜀 0
```

테스트 중 출력되는 `/health/ready` 503, Update Check 429, 관리자 API 401은 각각 실패 상태를 검증하는 기존 서버 테스트 로그다. 전체 테스트가 통과했으므로 B12 실패로 판단하지 않는다.

## 3. 명령 및 exit code

### 3.1 startup-check

호스트 프로그램이 본 기능 시작 전에 실행한다.

필수 식별 인자:

```text
--product-code
--architecture
--install-dir
--app
```

지원 제품 코드:

```text
PCCAM
CAMVIEWER
UPDATER
```

지원 클라이언트 아키텍처:

```text
x86
x64
```

서버 Artifact 호환용 `any`는 실제 설치 프로그램 식별값으로 허용하지 않는다.

### 3.2 apply / apply-worker

`apply`와 내부 `apply-worker`도 `productCode`와 `architecture`를 필수로 전달한다. 적용 계획에 기록된 값과 실행 인자가 다르면 파일을 변경하기 전에 적용을 중단한다.

### 3.3 exit code

```text
0  = 검증 성공, 적용할 작업 없음
10 = 업데이트 또는 복구 적용 필요
20 = 검증 실패
30 = Update Check 실패
40 = 다운로드 실패
50 = 적용 실패
```

## 4. 제품·아키텍처 식별 정책

업데이트 대상은 다음 조합으로 식별한다.

```text
productCode + os + architecture + channel
```

현재 프로그램별 고정 프로필:

```text
PcCam 32비트 : PCCAM / windows / x86 / stable
PcCam 64비트 : PCCAM / windows / x64 / stable
CamViewer     : CAMVIEWER / windows / x64 / stable
```

호스트 프로그램은 OS 비트 수를 추측해 전달하지 않는다. 64비트 Windows에서도 PcCam 32비트가 실행될 수 있으므로 각 프로젝트에 고정된 빌드 아키텍처를 명시적으로 전달한다.

## 5. 적용 계획

고정 경로:

```text
{installRoot}\_update\state\repair-plan.json
```

적용 계획에는 다음 식별정보가 포함된다.

```text
productCode
architecture
installDirectory
applicationFileName
mode
jobId
package 정보
파일 대상 목록
```

`startup-check`, `apply`, `apply-worker` 전 구간에서 제품과 아키텍처를 유지하고 재검증한다.

## 6. 파일별 복구

`updateAvailable=false`이고 `files[]`가 있으면 다음 순서로 처리한다.

```text
경로 안전성 검사
→ 파일 존재 확인
→ 크기 비교
→ SHA-256 비교
→ Missing / SizeMismatch / HashMismatch 대상 산출
→ 필요한 파일만 작업 폴더에 다운로드
→ 크기·SHA-256 재검증
→ 적용 계획 저장
```

다운로드 위치:

```text
{installRoot}\_update\downloads\{jobId}\files\{relativePath}
```

원본 파일은 다운로드 단계에서 변경하지 않는다.

## 7. Full Package 적용

`updateAvailable=true`이면 파일별 복구보다 Full Package가 우선한다.

처리 흐름:

```text
ZIP 다운로드
→ 패키지 크기·SHA-256 검증
→ 안전한 staging 추출
→ ZIP 경로 traversal·절대경로·중복 경로·symlink 차단
→ root application 파일 존재 확인
→ 별도 worker runtime 생성 및 실행
→ 원본 UpdateClient 종료 대기
→ 설치 파일 백업·교체·재검증
→ 실패 시 역순 rollback
→ 성공 시 대상 프로그램 재실행
```

실행 중인 UpdateClient 자체를 교체할 수 있도록 worker는 아래 작업별 경로에서 실행한다.

```text
{installRoot}\_update\workers\{jobId}
```

## 8. 백업 및 rollback

백업 경로:

```text
{installRoot}\_update\backups\{jobId}
```

적용 중 하나라도 실패하거나 재실행이 실패하면 적용된 파일을 역순으로 복구한다. 신규 생성 파일은 삭제하고 기존 파일은 백업본으로 복원한다. rollback까지 실패하면 적용 실패로 종료하고 계획 파일을 유지한다.

## 9. 운영 로그

로그 경로:

```text
{installRoot}\_update\logs\updateclient-yyyyMMdd.log
```

로그 기록 실패가 업데이트 exit code를 변경하지 않도록 로그 예외는 업데이트 흐름과 분리한다. 다운로드 URL 전체, 토큰, 비밀번호, Connection String과 같은 민감정보는 기록하지 않는다.

## 10. 주요 구현 영역

```text
Models/StartupCheckOptions.cs
Models/ApplyOptions.cs
Models/UpdateApplyPlan.cs
Models/UpdateProductIdentity.cs
Services/StartupCheckService.cs
Services/StartupUpdatePreparationService.cs
Services/ManifestRepairPlanner.cs
Services/UpdateFileDownloadService.cs
Services/FileRepairApplyService.cs
Services/FullPackageStagingService.cs
Services/FullPackageApplyService.cs
Services/UpdateApplyService.cs
Services/UpdateApplyWorkerService.cs
Services/UpdateWorkerLauncherService.cs
Services/UpdateClientLog.cs
UpdateClientApplication.cs
```

## 11. 최종 검증

사용자 로컬 환경에서 다음 명령으로 검증했다.

```powershell
dotnet build POSCAM.UpdateServer.sln -c Release
dotnet test POSCAM.UpdateServer.sln -c Release --no-build
```

결과:

```text
POSCAM.UpdateClient: 성공
POSCAM.UpdateClient.Tests: 성공
POSCAM.UpdateServer.Api: 성공
POSCAM.UpdateServer.Tests: 성공
전체 테스트: 450/450 성공
경고: 0
오류: 0
```

## 12. B12 범위 밖 후속 작업

B12는 공용 UpdateClient 구현까지 완료한 단계다. 실제 제품 저장소 연결은 별도 후속 단계로 진행한다.

```text
1. PcCam 32비트 저장소 연동
   productCode=PCCAM
   architecture=x86

2. PcCam 64비트 저장소 연동
   productCode=PCCAM
   architecture=x64

3. CamViewer 저장소 연동
   productCode=CAMVIEWER
   architecture=x64
```

각 저장소에서 확인할 사항:

```text
본 기능 시작 전 startup-check 호출 위치
exit code 0/10/20/30/40 처리
apply 실행 후 호스트 프로세스 종료 순서
배포 폴더에 UpdateClient와 Newtonsoft.Json 포함
실제 설치 경로 권한
실제 프로세스 종료·교체·재실행 통합 테스트
```

운영 DB migration, 실제 Artifact 업로드, 운영 배포는 B12 범위에 포함하지 않는다.

## 13. 완료 기준 충족 여부

| 항목 | 결과 |
|---|---|
| net48 / x86 / WinExe | 완료 |
| startup-check / apply 명령 | 완료 |
| exit code 정책 | 완료 |
| Update Check DTO / files[] | 완료 |
| ManifestRepairPlanner | 완료 |
| 안전 경로 검사 | 완료 |
| 다운로드·크기·SHA-256 검증 | 완료 |
| FileRepair 적용·rollback | 완료 |
| Full Package staging·worker 적용 | 완료 |
| 제품·아키텍처 식별 및 재검증 | 완료 |
| 무인 운영 로그 | 완료 |
| Release 빌드 | 성공, 경고 0 |
| 전체 테스트 | 450/450 성공 |

## 14. 최종 상태

B12 UpdateClient 구현은 완료되었다.

다음 개발 단계는 UpdateClient 코드를 추가로 확장하는 것이 아니라, 각 제품 저장소의 현재 시작 흐름을 분석하고 프로젝트별 고정 프로필로 안전하게 연동하는 작업이다.
