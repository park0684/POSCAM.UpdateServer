# 클라이언트 업데이트 실패 시 호스트 연속 실행 정책

## 1. 목적

업데이트 기능의 장애가 PC CAM 또는 CamViewer의 기존 정상 기능을 중단시키지 않도록 호스트 프로그램의 exit code 처리 원칙을 정의한다.

이 문서는 기존 `docs/client-manifest-repair-update-policy.md`와 `codex/prompts/B12_IMPLEMENT_CLIENT_MANIFEST_REPAIR_UPDATE.md`에 기록된 **업데이트 확인 실패 시 호스트 시작 차단** 규칙을 대체한다.

## 2. 기본 원칙

업데이트는 기존 프로그램 실행보다 우선하지 않는다.

```text
UpdateServer 접속 실패
Update Check 실패
응답 검증 실패
업데이트 다운로드 실패
UpdateClient 실행 파일 또는 종속 DLL 누락
apply 프로세스 시작 실패
```

위 상황에서는 설치된 기존 파일을 변경하지 않았으므로 호스트 프로그램은 오류를 기록하고 현재 설치된 버전으로 계속 실행한다.

## 3. 호스트 프로그램 종료 조건

호스트 프로그램은 아래 조건을 모두 만족할 때만 종료한다.

```text
1. startup-check가 exit code 10을 반환함
2. repair-plan.json이 존재함
3. apply 프로세스가 정상적으로 시작됨
```

`apply` 프로세스가 정상적으로 시작된 이후에만 실행 중 파일 교체를 위해 호스트가 종료된다.

## 4. startup-check exit code 해석

```text
0  = 업데이트 작업 없음 → 현재 프로그램 계속 실행
10 = 적용 필요 → apply 시작 성공 시에만 현재 프로그램 종료
20 = 검증 실패 → 오류 로그 후 현재 프로그램 계속 실행
30 = Update Check 실패 → 오류 로그 후 현재 프로그램 계속 실행
40 = 다운로드 실패 → 오류 로그 후 현재 프로그램 계속 실행
그 밖의 값 = 오류 로그 후 현재 프로그램 계속 실행
```

`50`은 `apply` 또는 `apply-worker` 단계의 적용 실패 코드이므로 일반적인 startup-check 결과로 사용하지 않는다.

## 5. apply 시작 실패

startup-check가 exit code 10을 반환했더라도 다음 상황이면 호스트 프로그램을 종료하지 않는다.

```text
적용 계획 파일 없음
UpdateClient apply 프로세스 시작 실패
프로세스 생성 예외
```

이 경우 현재 설치된 버전으로 계속 실행하고 다음 시작 시 다시 업데이트를 시도한다.

## 6. 적용 단계와의 구분

이 정책은 **호스트 프로그램이 아직 실행 중이고 설치 파일이 변경되기 전 단계**에 적용한다.

apply가 정상적으로 시작되어 호스트가 종료된 이후의 교체 실패는 UpdateClient의 백업·rollback 정책으로 처리한다. 해당 단계의 복구 및 재실행 정책은 별도로 검증한다.

## 7. 제품별 고정 식별값

```text
PcCam 32비트 : PCCAM / x86
PcCam 64비트 : PCCAM / x64
CamViewer     : CAMVIEWER / x64
```

제품과 아키텍처 식별 실패도 업데이트 기능 실패로 기록하되, 기존 호스트 프로그램의 실행 자체를 차단하지 않는다.
