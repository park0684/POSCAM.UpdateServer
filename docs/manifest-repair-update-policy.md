# Manifest 파일별 복구 업데이트 정책

## 1. 목적

이 문서는 POSCAM.UpdateServer의 Manifest 기반 파일별 복구 업데이트 기준을 정의한다.

기존 버전 비교 방식은 로컬 버전이 서버 버전보다 낮은 경우에만 업데이트를 수행한다. 이 구조에서는 서버 버전과 로컬 버전이 동일하지만 일부 파일이 누락되거나 손상된 경우 복구할 수 없다.

본 정책은 동일 버전에서도 파일 존재 여부와 SHA-256 무결성을 검사하여 필요한 파일만 개별 다운로드하고 복구할 수 있도록 한다.

## 2. 핵심 방향

- Full ZIP 업로드 구조는 유지한다.
- 서버는 ZIP 업로드 시 파일별 Manifest를 생성한다.
- Manifest에는 파일 상대경로, 파일 크기, SHA-256, 필수 여부, 파일별 다운로드 식별자를 저장한다.
- 클라이언트는 서버 Manifest와 로컬 파일 상태를 비교한다.
- 동일 버전이라도 필수 파일이 없거나 SHA-256이 다르면 `NeedRepair`로 판단한다.
- `NeedRepair`에서는 전체 ZIP이 아니라 필요한 파일만 개별 다운로드한다.
- 전체 ZIP은 최초 설치, 낮은 버전 업데이트, 긴급 전체 복구용으로 유지한다.

## 3. 업데이트 판단 기준

| 조건 | 결과 | 설명 |
|---|---|---|
| 서버 버전 > 로컬 버전 | `NeedUpdate` | 버전 업그레이드 필요 |
| 서버 버전 = 로컬 버전, 필수 파일 누락 | `NeedRepair` | 동일 버전 파일 복구 필요 |
| 서버 버전 = 로컬 버전, 필수 파일 SHA 불일치 | `NeedRepair` | 동일 버전 파일 손상 복구 필요 |
| 서버 버전 = 로컬 버전, 필수 파일 정상 | `NoUpdate` | 최신 상태 |
| 서버 정책상 강제 업데이트 | `ForceUpdate` | 버전/파일 상태와 무관하게 업데이트 필요 |
| 서버 정책상 차단 버전 | `Blocked` | 실행 또는 업데이트 차단 |

## 4. Manifest 포함 대상

Manifest에는 배포 패키지에 포함된 실행·기능 파일 중 서버가 복구 대상으로 관리해야 하는 파일을 포함한다.

### 4.1 기본 포함 대상

- `*.exe`
- `*.dll`
- `providers/*.dll`
- `ffmpeg.exe`
- `mediamtx.exe`
- 필수 리소스 파일
- 기본 템플릿 파일
- 프로그램 실행에 필요한 고정 설정 샘플 파일

### 4.2 기본 제외 대상

다음 파일은 사용자 환경에 따라 달라지므로 Manifest 검사 및 복구 대상에서 제외한다.

- 사용자 설정 파일
- 인증 토큰 파일
- 로그 파일
- 캐시 파일
- 임시 파일
- 로컬 DB 파일
- 장비별 또는 매장별로 생성되는 파일
- 실행 중 생성·수정되는 상태 파일

## 5. ZIP 업로드 처리 기준

관리자가 Draft Release에 ZIP을 업로드하면 서버는 다음 순서로 처리한다.

1. 업로드 파일을 staging 경로에 저장한다.
2. ZIP 파일 자체의 SHA-256과 크기를 계산한다.
3. ZIP 형식과 Entry를 검증한다.
4. 절대경로, `../`, Zip Slip 가능 경로를 거부한다.
5. 허용되지 않은 Entry 또는 비정상 Entry를 거부한다.
6. ZIP 내부 파일별 상대경로를 정규화한다.
7. 각 파일의 크기와 SHA-256을 계산한다.
8. Manifest 대상 여부를 판단한다.
9. 대상 파일을 파일별 storage 경로에 저장한다.
10. Full ZIP과 파일별 Manifest를 DB에 등록한다.
11. DB 등록 실패 시 staging 및 생성 파일을 정리한다.

## 6. 파일별 저장 기준

파일별 다운로드를 위해 ZIP 내부 파일을 개별 저장한다.

권장 저장 구조:

```text
/app/update-storage/packages/{product}/{version}/{artifactPublicId}/full.zip
/app/update-storage/packages/{product}/{version}/{artifactPublicId}/files/{filePublicId}
```

외부 공개 URL은 실제 서버 파일 경로를 노출하지 않는다.

권장 다운로드 URL:

```text
/packages/{product}/{version}/{artifactPublicId}/files/{filePublicId}
```

또는 감사/권한/통계가 필요한 경우 API 방식으로 전환할 수 있다.

```text
/api/v1/updates/files/{filePublicId}/download
```

## 7. Published Artifact 처리 기준

Published Release와 Artifact는 불변으로 취급한다.

- Published ZIP 파일 자체를 교체하지 않는다.
- Published Artifact의 핵심정보를 수정하지 않는다.
- 기존 Published ZIP에 Manifest가 없는 경우 ZIP을 분석하여 Manifest와 파일별 저장소를 생성할 수 있다.
- 이때 ZIP 원본은 변경하지 않는다.
- Manifest 생성 작업은 감사 로그에 `GENERATE_MANIFEST`로 기록한다.

## 8. Update Check 응답 기준

Update Check 응답에는 클라이언트가 로컬 검사를 수행할 수 있는 Manifest 정보를 제공해야 한다.

권장 응답 필드:

```json
{
  "productCode": "PCCAM",
  "latestVersion": "1.0.5",
  "updateState": "NeedRepair",
  "mandatory": false,
  "packageUrl": "/packages/pccam/1.0.5/full.zip",
  "packageSha256": "...",
  "files": [
    {
      "path": "PCCAM.exe",
      "size": 1234567,
      "sha256": "...",
      "required": true,
      "downloadUrl": "/packages/pccam/1.0.5/files/abc123"
    }
  ]
}
```

파일 수가 많아 응답이 과도하게 커질 경우 `manifestUrl` 방식으로 분리할 수 있다.

## 9. 클라이언트 복구 처리 기준

클라이언트는 다음 순서로 복구를 수행한다.

1. Update Check 응답에서 Manifest를 수신한다.
2. 로컬 설치 경로 기준으로 Manifest 대상 파일을 찾는다.
3. 파일 누락 여부를 확인한다.
4. 존재하는 파일의 SHA-256을 계산한다.
5. 서버 Manifest와 비교한다.
6. 누락 또는 SHA 불일치 파일 목록을 만든다.
7. 필요한 파일만 개별 다운로드한다.
8. 다운로드 파일의 SHA-256을 검증한다.
9. 검증 성공 후 기존 파일을 백업한다.
10. 대상 파일을 교체한다.
11. 교체 후 다시 SHA-256을 검증한다.
12. 실패 시 기존 파일로 롤백한다.

## 10. 실행 중 파일 교체 기준

실행 중인 EXE/DLL은 직접 덮어쓰지 않는다.

- 업데이트 적용은 별도 Updater 프로세스에서 수행한다.
- 메인 프로그램은 종료 후 교체한다.
- 교체 대상 파일이 잠겨 있으면 적용을 중단하거나 재부팅/다음 실행 시 적용으로 예약한다.
- 실패 시 기존 파일을 보존한다.

## 11. 보안 기준

- ZIP Entry 경로는 반드시 정규화한다.
- 절대경로는 거부한다.
- `../` 경로는 거부한다.
- 빈 파일명 또는 디렉터리 Traversal 가능 경로는 거부한다.
- 실제 서버 저장 경로를 응답에 노출하지 않는다.
- 다운로드 파일은 SHA-256 검증 전 적용하지 않는다.
- Manifest와 실제 파일 SHA가 다르면 배포를 차단한다.

## 12. 실패 처리 기준

- 파일 다운로드 실패 시 해당 파일은 적용하지 않는다.
- SHA 검증 실패 시 해당 파일은 적용하지 않는다.
- 일부 파일 교체 실패 시 전체 복구 작업을 실패 처리한다.
- 기존 파일 백업이 없는 상태에서 원본 파일을 삭제하지 않는다.
- 실패 결과는 클라이언트 로그에 남긴다.
- 서버는 민감한 로컬 경로나 토큰을 로그에 남기지 않는다.

## 13. 완료 검증 기준

다음 테스트를 통과해야 한다.

- 동일 버전 + 정상 파일: `NoUpdate`
- 동일 버전 + 파일 누락: `NeedRepair`
- 동일 버전 + 파일 손상: `NeedRepair`
- 낮은 로컬 버전: `NeedUpdate`
- 강제 업데이트 Release: `ForceUpdate`
- 다운로드 파일 SHA 불일치: 적용 중단
- 사용자 설정·로그 파일: 검사 대상 제외
- Zip Slip ZIP 업로드: 거부
- Published ZIP 원본: 변경 없음
