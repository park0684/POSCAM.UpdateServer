# 클라이언트 Manifest 파일 복구 업데이트 정책

## 1. 목적

이 문서는 UpdateServer B11에서 제공하는 `files[]` Manifest 응답을 클라이언트 Updater가 어떻게 해석하고 처리할지 정의한다.

서버는 클라이언트 PC의 실제 파일 상태를 알 수 없으므로 복구 필요 여부를 직접 판단하지 않는다. 클라이언트는 Update Check 응답의 `files[]`를 기준으로 로컬 파일 존재 여부, 파일 크기, SHA-256을 검사하고 필요한 파일만 다운로드하여 복구한다.

## 2. 적용 대상

우선 적용 대상은 POSCAM 계열 Windows 클라이언트이다.

```text
PCCAM
POSCAM.CamViewer
향후 POSCAM.UpdateClient
```

정책상 파일 검사와 다운로드 판단 로직은 공통화할 수 있다. 단, 실행 중인 EXE/DLL 교체는 제품별 실행 구조에 따라 별도 Updater 프로세스가 담당하는 것을 원칙으로 한다.

## 3. 기본 원칙

### 3.1 Full ZIP 업데이트 우선

`updateAvailable=true`이면 기존 Full ZIP 업데이트 흐름을 우선한다.

```text
updateAvailable=true
→ packageUrl 기반 Full ZIP 업데이트
→ files[]는 검증 또는 보조 정보로만 사용 가능
```

이유는 버전이 실제로 올라가는 경우에는 전체 패키지 단위 교체가 더 명확하고, 누락 파일만 부분 복구하는 것보다 일관성이 높기 때문이다.

### 3.2 동일 버전 복구는 files[] 기준

`updateAvailable=false`이더라도 `files[]`가 있으면 클라이언트는 로컬 파일 검사를 수행할 수 있다.

```text
updateAvailable=false
reasonCode=ALREADY_LATEST
files[] 있음
→ 로컬 파일 검사
→ 누락/손상 파일이 있으면 부분 복구
```

이 흐름은 서버의 업데이트 판정과 별개다. 서버 기준으로는 최신 버전이지만, 클라이언트 로컬 파일이 손상되었을 수 있기 때문이다.

### 3.3 서버는 NeedRepair를 판단하지 않는다

`NeedRepair`는 클라이언트 내부 판단값이다.

서버 응답만으로는 아래를 알 수 없다.

```text
로컬 파일 존재 여부
로컬 파일 크기
로컬 파일 SHA-256
실행 중 파일 잠금 여부
```

따라서 서버는 Manifest를 제공하고, 클라이언트가 실제 복구 대상을 산출한다.

## 4. Update Check 응답 해석

### 4.1 기존 필드

기존 Full ZIP 업데이트 판단 필드는 유지한다.

```text
updateAvailable
mandatory
reasonCode
packageUrl
fileName
fileSize
sha256
```

### 4.2 files[] 필드

B11 이후 응답에는 다음 구조의 `files[]`가 포함될 수 있다.

```text
path
size
sha256
required
downloadUrl
```

필드 의미:

| 필드 | 의미 |
|---|---|
| path | 프로그램 설치 경로 기준 상대 파일 경로 |
| size | 서버 기준 파일 크기 |
| sha256 | 서버 기준 SHA-256 |
| required | 필수 파일 여부 |
| downloadUrl | 해당 파일 단독 다운로드 URL |

## 5. 로컬 파일 검사 정책

### 5.1 기준 경로

`files[].path`는 설치 루트 기준 상대 경로로 해석한다.

예:

```text
설치 루트: C:\POSCAM\PCCAM
path: resources/model.dat
실제 검사 경로: C:\POSCAM\PCCAM\resources\model.dat
```

### 5.2 경로 안전성 검증

클라이언트는 서버 응답이라도 반드시 경로 안전성 검사를 수행해야 한다.

거부 대상:

```text
빈 path
절대 경로
드라이브 문자 포함 경로
.. 포함 경로
\ 또는 / 기준으로 비정상 segment 포함 경로
설치 루트 밖으로 벗어나는 경로
```

안전하지 않은 path가 있으면 해당 응답은 신뢰하지 않고 복구를 중단한다.

### 5.3 검사 순서

각 파일은 다음 순서로 검사한다.

```text
1. path 안전성 확인
2. 로컬 실제 경로 계산
3. 파일 존재 여부 확인
4. 파일 크기 비교
5. SHA-256 비교
6. 불일치 시 RepairTarget으로 등록
```

### 5.4 복구 대상 판단

복구 대상 조건:

```text
파일 없음
파일 크기 다름
SHA-256 다름
```

파일 크기가 다르면 SHA-256 계산을 생략하고 바로 복구 대상으로 등록할 수 있다. 파일 크기가 같으면 SHA-256을 계산해 최종 확인한다.

## 6. 다운로드 정책

### 6.1 임시 다운로드 경로

복구 파일은 바로 원본 경로에 저장하지 않는다.

권장 임시 경로:

```text
{설치루트}\_update\downloads\{작업ID}\{safeRelativePath}
```

예:

```text
C:\POSCAM\PCCAM\_update\downloads\20260704_001\PCCAM.exe
```

### 6.2 다운로드 후 검증

다운로드 후 반드시 다시 검증한다.

```text
다운로드 파일 크기 == files[].size
다운로드 파일 SHA-256 == files[].sha256
```

검증 실패 시 원본 파일을 건드리지 않고 실패 처리한다.

### 6.3 부분 실패 정책

하나라도 다운로드 또는 검증에 실패하면 해당 복구 작업 전체를 실패 처리한다.

이유:

```text
일부 파일만 교체되면 실행 파일과 DLL 버전 불일치가 발생할 수 있다.
부분 복구 성공 상태를 정상으로 간주하면 장애 원인 추적이 어려워진다.
```

## 7. 파일 교체 정책

### 7.1 실행 중 파일은 직접 덮어쓰지 않는다

다음 파일은 메인 프로그램이 실행 중일 때 잠겨 있을 수 있다.

```text
*.exe
*.dll
ffmpeg.exe
mediamtx.exe
```

따라서 메인 프로그램 내부에서 직접 덮어쓰지 않는다.

### 7.2 별도 Updater 프로세스 원칙

파일 교체는 별도 Updater 프로세스가 담당한다.

권장 구성:

```text
POSCAM.UpdateClient.exe
```

역할:

```text
메인 프로그램 종료 대기
다운로드 파일 검증
기존 파일 백업
새 파일 교체
교체 후 검증
메인 프로그램 재실행
실패 시 rollback
로그 기록
```

### 7.3 교체 전 백업

교체 대상 파일은 먼저 백업한다.

권장 백업 경로:

```text
{설치루트}\_update\backup\{작업ID}\{safeRelativePath}
```

### 7.4 교체 순서

권장 순서:

```text
1. 메인 프로그램 종료 요청
2. 지정 시간 동안 프로세스 종료 대기
3. 종료되지 않으면 복구 중단
4. 교체 대상 기존 파일 백업
5. 다운로드 파일을 원본 위치로 이동 또는 복사
6. 교체 후 SHA-256 재검증
7. 전체 성공 시 백업 보관 또는 정리
8. 실패 시 백업 파일로 rollback
9. 메인 프로그램 재실행
```

강제 종료는 초기 버전에서는 사용하지 않는다. 사용자가 저장 중인 상태나 녹화/송출 중인 상태를 손상시킬 수 있기 때문이다.

## 8. Full ZIP 업데이트와 파일 복구의 우선순위

### 8.1 updateAvailable=true

```text
Full ZIP 업데이트 우선
files[] 부분 복구는 수행하지 않음
```

### 8.2 updateAvailable=false + files[] 있음

```text
로컬 파일 검사
복구 대상이 없으면 정상 종료
복구 대상이 있으면 부분 복구 진행
```

### 8.3 files[] 없음

```text
부분 복구 수행하지 않음
기존 Update Check 결과만 사용
```

## 9. 로그 정책

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
최종 결과
```

로그에는 다운로드 URL 전체를 남기지 않는 것이 좋다. 필요하면 storage key 일부 또는 path만 남긴다.

## 10. 사용자 메시지 정책

### 10.1 복구 대상 없음

```text
프로그램 파일이 정상입니다.
```

### 10.2 복구 필요

```text
프로그램 구성 파일 일부를 복구해야 합니다.
복구 중에는 프로그램이 잠시 종료될 수 있습니다.
```

### 10.3 실패

```text
프로그램 파일 복구에 실패했습니다.
잠시 후 다시 시도하거나 관리자에게 문의해 주세요.
```

### 10.4 관리자 권한 필요

설치 위치가 `Program Files` 등 보호 경로인 경우 관리자 권한이 필요할 수 있다.

```text
파일 복구를 위해 관리자 권한이 필요합니다.
```

## 11. 실패 처리 정책

### 11.1 다운로드 실패

```text
원본 파일 변경 없음
실패 로그 기록
사용자에게 재시도 안내
```

### 11.2 검증 실패

```text
다운로드 파일 삭제 또는 격리
원본 파일 변경 없음
실패 로그 기록
```

### 11.3 교체 실패

```text
가능하면 백업으로 rollback
rollback 성공/실패 로그 기록
메인 프로그램 재실행 여부는 상태에 따라 판단
```

### 11.4 rollback 실패

```text
심각 오류로 기록
사용자에게 재설치 또는 관리자 문의 안내
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
ManifestRepairPlanner
LocalFileHashService
RepairDownloadService
RepairApplyService
RepairBackupService
UpdaterProcessLauncher
RepairLogService
```

### 13.1 ManifestRepairPlanner

역할:

```text
files[] 검증
로컬 파일 존재/크기/SHA-256 검사
RepairTarget 목록 생성
```

### 13.2 RepairDownloadService

역할:

```text
downloadUrl 다운로드
임시 경로 저장
크기/SHA-256 검증
```

### 13.3 RepairApplyService

역할:

```text
백업
교체
교체 후 검증
rollback
```

### 13.4 UpdaterProcessLauncher

역할:

```text
메인 프로그램에서 별도 Updater 실행
복구 작업 파일 전달
메인 프로그램 종료
```

## 14. 다음 단계

다음 작업은 실제 클라이언트 저장소에서 아래 순서로 진행한다.

```text
1. UpdateCheck files[] DTO 추가
2. ManifestRepairPlanner 구현
3. SHA-256 검사 구현
4. 복구 대상 산출 테스트 작성
5. 다운로드/검증 구현
6. 별도 Updater 프로세스 설계
7. 실행 중 파일 교체/rollback 구현
8. PCCAM 또는 CamViewer 실행 흐름에 연결
```
