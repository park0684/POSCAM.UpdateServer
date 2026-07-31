# PCCAM x64 3.2.2 Full Package 전환

## 1. 목적

PCCAM x64 3.2.1에 배포된 구형 `POSCAM.UpdateClient.exe`는 Manifest 파일 복구 대상에 자기 자신이 포함되면 FileRepair 적용을 시도한다.

실행 중인 UpdateClient는 자기 자신을 직접 교체할 수 없으므로 다음 흐름으로 실패한다.

```text
files[] 비교
→ POSCAM.UpdateClient.exe 불일치 감지
→ FileRepair 다운로드 완료
→ apply 사전 검증
→ 실행 중인 UpdateClient 직접 교체 차단
→ ExitCode=50
```

3.2.2에는 이 문제를 수정한 새 UpdateClient가 포함되지만, 구형 클라이언트가 새 UpdateClient를 받기 전에 같은 실패가 발생할 수 있다.

따라서 3.2.2 전환 기간에는 Update Check 응답의 패키지 정보는 유지하고 `files[]`만 비워, 구형 UpdateClient도 기존 Full Package worker 경로를 선택하도록 한다.

## 2. 운영 설정

운영 UpdateServer 컨테이너에 다음 환경변수를 추가한다.

```text
UpdateStorage__ForceFullPackageReleaseKeys__0=PCCAM_X64:3.2.2
```

Docker Compose 예시:

```yaml
environment:
  UpdateStorage__ForceFullPackageReleaseKeys__0: PCCAM_X64:3.2.2
```

직접 `docker run`을 사용하는 경우:

```bash
-e 'UpdateStorage__ForceFullPackageReleaseKeys__0=PCCAM_X64:3.2.2'
```

기본 `appsettings.json` 값은 빈 배열이다.

```json
{
  "UpdateStorage": {
    "ForceFullPackageReleaseKeys": []
  }
}
```

설정을 넣지 않으면 기존 Manifest 파일 복구 동작이 유지된다.

## 3. Update Check 기대 결과

### 3.1 3.2.1 클라이언트

요청:

```json
{
  "productCode": "PCCAM_X64",
  "currentVersion": "3.2.1.0",
  "os": "windows",
  "architecture": "x64",
  "channel": "stable"
}
```

3.2.2가 Published 상태이고 전환 설정이 활성화되면:

```text
updateAvailable=true
latestVersion=3.2.2
packageUrl=3.2.2 Full ZIP URL
files=[]
```

구형 UpdateClient는 `files[]`가 비어 있으므로 Full Package worker를 사용한다.

### 3.2 3.2.2 클라이언트

```text
updateAvailable=false
reasonCode=ALREADY_LATEST
latestVersion=3.2.2
files=[]
```

같은 버전 반복 적용은 발생하지 않는다.

## 4. 배포 순서

```text
1. UpdateServer 변경 빌드 및 테스트
2. 운영 UpdateServer 새 이미지 배포
3. ForceFullPackageReleaseKeys 환경변수 적용 확인
4. PCCAM x64 3.2.2 Full ZIP 생성 및 무결성 검증
5. 3.2.2 Draft 릴리스와 Artifact 등록
6. 게시 전 Update Check 테스트
7. 3.2.2 Published 전환
8. 3.2.1 설치처에서 Full Package 업데이트 검증
9. 3.2.2 재시작·인증·송출 검증
```

3.2.2를 먼저 게시하고 서버 전환 설정을 나중에 적용하면 구형 UpdateClient가 다시 FileRepair 실패를 일으킬 수 있으므로 순서를 바꾸지 않는다.

## 5. 전환 설정 제거 시점

전환 설정이 활성화된 동안 3.2.2의 동일 버전 Manifest 파일 복구는 비활성화된다.

주요 설치처가 3.2.2의 수정된 UpdateClient로 전환된 뒤 다음 환경변수를 제거하고 UpdateServer를 재배포한다.

```text
UpdateStorage__ForceFullPackageReleaseKeys__0
```

설정 제거 후 수정된 3.2.2 UpdateClient는 다시 정상적으로 `files[]`를 받아 개별 파일 복구를 수행한다. UpdateClient 자체가 불일치하면 새 로직에 따라 Full Package worker로 전환한다.

## 6. 확인 항목

전환 활성 상태:

```text
3.2.1 → 3.2.2 요청에서 files=[]
packageUrl/fileSize/sha256 유지
Full Package 다운로드 성공
worker가 POSCAM.UpdateClient.exe를 포함해 교체
재실행 후 PcCam.exe FileVersion=3.2.2.0
```

전환 제거 후:

```text
3.2.2 동일 버전 요청에서 files[] 반환
일반 DLL 누락은 FileRepair
POSCAM.UpdateClient.exe 불일치는 Full Package worker
```

## 7. 제외 사항

이 설정은 Artifact, DB의 Manifest 파일 정보 또는 실제 저장 파일을 삭제하지 않는다. Update Check 응답에서 지정된 제품·버전의 `files[]` 노출만 일시적으로 억제한다.
