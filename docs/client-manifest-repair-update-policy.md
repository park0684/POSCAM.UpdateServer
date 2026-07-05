# 클라이언트 Manifest 파일 복구 업데이트 정책

## 1. 목적

이 문서는 UpdateServer B11에서 제공하는 `files[]` Manifest 응답을 클라이언트가 어떻게 해석하고 처리할지 정의한다.

최종 구조는 `PcCam.exe`가 먼저 실행되고, 본 기능을 시작하기 전에 `POSCAM.UpdateClient.exe`를 호출해 업데이트 및 파일 무결성 검증을 받는 방식이다.

서버는 클라이언트 PC의 실제 파일 상태를 알 수 없으므로 복구 필요 여부를 직접 판단하지 않는다. 클라이언트는 Update Check 응답의 `files[]`를 기준으로 로컬 파일 존재 여부, 파일 크기, SHA-256을 검사하고 필요한 파일만 다운로드하여 복구한다.

## 2. 실행 관계

### 2.1 PcCam.exe가 실행 진입점이다

사용자, 시작 프로그램, 작업 스케줄러, 자동 실행은 기존처럼 `PcCam.exe`를 실행한다.

```text
PcCam.exe 실행
→ 본 기능 시작 전 POSCAM.UpdateClient.exe 호출
→ 업데이트/파일 검증 결과 확인
→ 정상일 때만 캡처/송출 시작
```

`POSCAM.UpdateClient.exe`는 독립 런처가 아니다. `PcCam.exe`가 호출하는 무인 검증/복구 보조 프로세스다.

### 2.2 PcCam.exe의 책임

```text
1. 최소 초기화
2. UpdateClient startup-check 호출
3. 검증 결과 수신
4. 정상 결과면 본 기능 시작
5. 업데이트/복구 적용 필요 결과면 본 기능을 시작하지 않음
6. UpdateClient apply 호출 후 자기 자신 종료
```

PcCam.exe는 자기 자신이나 실행 중인 DLL을 직접 덮어쓰지 않는다.

### 2.3 UpdateClient의 책임

```text
1. Update Check 호출
2. updateAvailable 확인
3. files[] 기준 로컬 파일 검사
4. 필요한 파일 다운로드
5. 다운로드 파일 size/SHA-256 검증
6. 적용 계획 파일 생성
7. PcCam 종료 대기
8. 기존 파일 백업
9. 새 파일 교체
10. 교체 후 검증
11. 실패 시 rollback
12. PcCam.exe 재실행
13. 로그 기록
```

## 3. 무인 실행 정책

무인점포 환경을 전제로 하므로 사용자 확인 UI는 사용하지 않는다.

금지 항목:

```text
업데이트 하시겠습니까? 확인창
복구하시겠습니까? 확인창
확인/취소 MessageBox
시스템 트레이 알림
수동 입력 대기
콘솔창 표시
```

`POSCAM.UpdateClient.exe`는 UI 없는 silent 프로세스로 동작한다. 결과는 exit code, 작업 파일, 로그로만 전달한다.

권장 프로젝트 형태:

```text
TargetFramework = net48
PlatformTarget = x86
OutputType = WinExe
```

## 4. UpdateClient 명령 구조

### 4.1 startup-check

PcCam.exe가 본 기능 시작 전에 호출한다.

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

예상 호출:

```powershell
POSCAM.UpdateClient.exe startup-check --app PcCam.exe --install-dir "C:\POSCAM\PCCAM"
```

### 4.2 apply

업데이트/복구 적용이 필요할 때 PcCam.exe가 호출한 뒤 자기 자신을 종료한다.

역할:

```text
PcCam.exe 프로세스 종료 대기
기존 파일 백업
새 파일 교체
교체 후 SHA-256 재검증
실패 시 rollback
PcCam.exe 재실행
```

예상 호출:

```powershell
POSCAM.UpdateClient.exe apply --plan "_update\state\repair-plan.json" --wait-process-id 1234 --restart "PcCam.exe"
```

## 5. exit code 정책

PcCam.exe는 UpdateClient의 exit code로 다음 행동을 결정한다.

```text
0  = 검증 성공, 업데이트/복구 필요 없음, PcCam 계속 실행
10 = 업데이트 또는 복구 적용 필요, PcCam은 apply 실행 후 종료
20 = 검증 실패, PcCam 본 기능 시작 금지
30 = Update Check 실패, PcCam 본 기능 시작 금지
40 = 다운로드 실패, PcCam 본 기능 시작 금지
50 = 적용 실패, PcCam 재실행 금지
```

운영 정책상 검증이 완료되지 않았거나 파일 무결성이 보장되지 않으면 PcCam 본 기능은 시작하지 않는다.

## 6. Full ZIP 업데이트와 파일 복구 우선순위

### 6.1 updateAvailable=true

```text
Full ZIP 업데이트 우선
files[] 부분 복구는 적용하지 않음
UpdateClient가 Full ZIP 다운로드/검증 후 적용 계획 생성
PcCam은 본 기능을 시작하지 않고 apply 단계로 전환
```

### 6.2 updateAvailable=false + files[] 있음

```text
files[] 기준 로컬 파일 검사
복구 대상이 없으면 exit code 0
복구 대상이 있으면 필요한 파일만 다운로드/검증 후 exit code 10
```

### 6.3 files[] 없음

```text
부분 복구 수행하지 않음
기존 Update Check 결과만 사용
```

## 7. 로컬 파일 검사 정책

### 7.1 기준 경로

`files[].path`는 설치 루트 기준 상대 경로로 해석한다.

예:

```text
설치 루트: C:\POSCAM\PCCAM
path: resources/model.dat
실제 검사 경로: C:\POSCAM\PCCAM\resources\model.dat
```

### 7.2 경로 안전성 검증

클라이언트는 서버 응답이라도 반드시 경로 안전성 검사를 수행해야 한다.

거부 대상:

```text
빈 path
절대 경로
드라이브 문자 포함 경로
.. 포함 경로
NUL 문자 포함
\ 또는 / 기준으로 비정상 segment 포함 경로
설치 루트 밖으로 벗어나는 경로
```

안전하지 않은 path가 있으면 해당 응답은 신뢰하지 않고 복구를 중단한다.

### 7.3 검사 순서

```text
1. path 안전성 확인
2. 로컬 실제 경로 계산
3. 파일 존재 여부 확인
4. 파일 크기 비교
5. SHA-256 비교
6. 불일치 시 RepairTarget으로 등록
```

복구 대상 조건:

```text
파일 없음
파일 크기 다름
SHA-256 다름
```

파일 크기가 다르면 SHA-256 계산을 생략하고 바로 복구 대상으로 등록할 수 있다. 파일 크기가 같으면 SHA-256을 계산해 최종 확인한다.

## 8. 다운로드 정책

복구 파일은 바로 원본 경로에 저장하지 않는다.

권장 임시 경로:

```text
{설치루트}\_update\downloads\{작업ID}\{safeRelativePath}
```

다운로드 후 반드시 다시 검증한다.

```text
다운로드 파일 크기 == files[].size
다운로드 파일 SHA-256 == files[].sha256
```

검증 실패 시 원본 파일을 건드리지 않고 실패 처리한다.

하나라도 다운로드 또는 검증에 실패하면 해당 복구 작업 전체를 실패 처리한다.

## 9. 파일 교체 정책

실행 중 파일은 직접 덮어쓰지 않는다.

대상 예:

```text
*.exe
*.dll
ffmpeg.exe
mediamtx.exe
```

교체 순서:

```text
1. PcCam.exe가 apply 호출
2. PcCam.exe 종료
3. UpdateClient가 지정 시간 동안 PcCam.exe 종료 대기
4. 종료되지 않으면 적용 실패 처리
5. 교체 대상 기존 파일 백업
6. 다운로드 파일을 원본 위치로 이동 또는 복사
7. 교체 후 SHA-256 재검증
8. 전체 성공 시 PcCam.exe 재실행
9. 실패 시 백업 파일로 rollback
10. rollback 실패 시 심각 오류 로그 기록
```

초기 버전에서는 강제 종료를 사용하지 않는다.

## 10. 로그 정책

클라이언트는 최소한 아래 로그를 기록한다.

```text
UpdateCheck 요청/응답 요약
files[] 개수
검사 대상 파일 수
복구 대상 파일 수
각 복구 대상 path
다운로드 성공/실패
검증 성공/실패
교체 성공/실패
rollback 수행 여부
PcCam 재실행 여부
최종 exit code
```

로그에는 다운로드 URL 전체를 남기지 않는다. 필요하면 path 또는 storage key 일부만 남긴다.

## 11. 실패 처리 정책

### 11.1 Update Check 실패

```text
PcCam 본 기능 시작 금지
로그 기록
exit code 30
```

### 11.2 다운로드 실패

```text
원본 파일 변경 없음
실패 로그 기록
exit code 40
```

### 11.3 검증 실패

```text
다운로드 파일 삭제 또는 격리
원본 파일 변경 없음
실패 로그 기록
exit code 20 또는 40
```

### 11.4 교체 실패

```text
가능하면 백업으로 rollback
rollback 성공/실패 로그 기록
PcCam 재실행 금지
exit code 50
```

## 12. 보안 정책

클라이언트는 다음을 반드시 지켜야 한다.

```text
downloadUrl이 http/https인지 확인
path 안전성 검사
설치 루트 밖 파일 접근 금지
다운로드 후 SHA-256 검증
검증 전 원본 덮어쓰기 금지
상대 경로 그대로 파일 시스템에 join 금지
```

## 13. 구현 단위 제안

권장 클래스 구성:

```text
UpdateCheckClient
UpdateCheckResponse / UpdateManifestFile
StartupCheckCommand
ApplyRepairCommand
ManifestRepairPlanner
LocalFileHashService
RepairDownloadService
RepairApplyService
RepairBackupService
RepairPlanStore
RepairLogService
```

## 14. 다음 단계

```text
1. UpdateClient 명령을 startup-check/apply로 변경
2. exit code 상수 정의
3. UpdateCheck files[] DTO 추가
4. ManifestRepairPlanner 구현
5. SHA-256 검사 구현
6. 복구 대상 산출 테스트 작성
7. 다운로드/검증 구현
8. apply 백업/교체/rollback 구현
9. PcCam.exe 시작 흐름에서 UpdateClient 호출 연결
```
