# PC CAM 32비트·64비트 업데이트 제품 분리

## 1. 작업 목적

현재 UpdateServer는 PC CAM 32비트와 64비트를 하나의 제품 코드 `PCCAM`으로 관리한다.

현재 버전 계열은 다음과 같이 서로 다르다.

```text
PC CAM 32비트: 3.0.4
PC CAM 64비트: 3.2.0
```

릴리스의 중복 기준은 `제품 코드 + 채널 + 버전`이며, 아키텍처는 릴리스가 아니라 Artifact에 속한다. 따라서 향후 32비트가 `3.2.0`에 도달하면 이미 존재하는 64비트용 `PCCAM / stable / 3.2.0`과 충돌하여 별도 릴리스를 생성할 수 없다.

이 문제를 해결하기 위해 PC CAM 업데이트 제품을 다음과 같이 분리한다.

```text
PCCAM_X86 : PC CAM 32비트
PCCAM_X64 : PC CAM 64비트
```

기존 `PCCAM`은 이미 설치된 프로그램의 전환을 위해 일정 기간 유지한다.

---

## 2. 목표 구조

```text
PCCAM       : 기존 설치 프로그램 전환용 레거시 제품
PCCAM_X86   : PC CAM 32비트 전용 제품
PCCAM_X64   : PC CAM 64비트 전용 제품
CAMVIEWER   : CamViewer
UPDATER     : 공용 UpdateClient
```

제품별 릴리스는 독립적으로 관리한다.

```text
PCCAM_X86 / stable / 3.2.0
PCCAM_X64 / stable / 3.2.0
```

동일 버전이어도 제품 코드가 다르므로 각각 별도 릴리스로 등록할 수 있어야 한다.

---

## 3. 작업 대상

### 3.1 UpdateServer 제품 코드

다음 제품 코드를 유효한 제품으로 인정하도록 수정한다.

```text
PCCAM
PCCAM_X86
PCCAM_X64
CAMVIEWER
UPDATER
```

수정 대상:

- `src/POSCAM.UpdateServer.Api/Models/Domain/UpdateDomainCodes.cs`
- 제품 코드 검증을 직접 사용하는 테스트
- 제품 코드가 문서나 예제에 하드코딩된 부분

`PCCAM`은 레거시 전환용으로 유지하며 즉시 제거하지 않는다.

### 3.2 데이터베이스 제품 등록

`update_products`에 다음 제품을 추가한다.

```text
PCCAM_X86 / POSCAM PC CAM 32비트
PCCAM_X64 / POSCAM PC CAM 64비트
```

수정 대상:

- 신규 DB 마이그레이션 파일
- `database/seed/001_update_products.sql`
- 필요 시 `database/schema.sql` 문서성 데이터

다음 테이블에는 신규 컬럼을 추가하지 않는다.

```text
update_releases
update_artifacts
update_artifact_files
```

제품 코드 분리만으로 기존 릴리스 중복 구조를 유지할 수 있기 때문이다.

### 3.3 POSCAM.UpdateClient 제품 식별

공용 UpdateClient가 신규 제품 코드를 정상 처리하도록 수정한다.

수정 대상:

- `src/POSCAM.UpdateClient/Models/UpdateProductIdentity.cs`
- 제품 식별을 사용하는 옵션 검증
- 관련 단위 테스트와 프로세스 통합 테스트

허용 조합:

```text
PCCAM_X86 + x86
PCCAM_X64 + x64
PCCAM + x86
PCCAM + x64
CAMVIEWER + x86 또는 x64
UPDATER + x86 또는 x64
```

차단 조합:

```text
PCCAM_X86 + x64
PCCAM_X64 + x86
```

레거시 `PCCAM` 조합은 전환 기간에만 유지한다.

### 3.4 PC CAM 32비트 프로젝트

UpdateClient 실행 시 다음 값을 전달하도록 수정한다.

```text
ProductCode  = PCCAM_X86
Architecture = x86
```

수정 대상은 PC CAM 32비트 저장소에서 별도 진행한다.

- 제품 코드 상수
- UpdateClient 실행 인자 생성부
- 업데이트 확인 시작부
- 설정 파일에 제품 코드가 있다면 해당 설정
- 배포 패키지에 포함되는 UpdateClient 바이너리

### 3.5 PC CAM 64비트 프로젝트

UpdateClient 실행 시 다음 값을 전달하도록 수정한다.

```text
ProductCode  = PCCAM_X64
Architecture = x64
```

수정 대상은 PC CAM 64비트 저장소에서 별도 진행한다.

### 3.6 설치 Manifest와 상태 파일

전환 이후 설치 Manifest에는 신규 제품 코드와 아키텍처가 저장되어야 한다.

```text
32비트: PCCAM_X86 + x86
64비트: PCCAM_X64 + x64
```

기존 Manifest에 `PCCAM`이 저장되어 있어도 전환 업데이트 적용 후 신규 제품 코드로 갱신되어야 한다.

### 3.7 테스트

UpdateServer 테스트:

- `PCCAM_X86`, `PCCAM_X64` 제품 코드 허용
- 기존 `PCCAM` 제품 코드 유지
- `PCCAM_X86`과 `PCCAM_X64`에 동일 버전 릴리스 등록 가능
- 각 제품이 자신의 릴리스만 조회
- 잘못된 제품 코드 및 아키텍처 거부

UpdateClient 테스트:

- `PCCAM_X86 + x86` 성공
- `PCCAM_X64 + x64` 성공
- `PCCAM_X86 + x64` 실패
- `PCCAM_X64 + x86` 실패
- `PCCAM + x86/x64` 전환 기간 허용
- `CAMVIEWER`, `UPDATER` 기존 동작 유지

프로세스 통합 테스트:

- 신규 제품 코드로 업데이트 확인
- 파일 다운로드 및 Manifest 저장
- 적용 후 재실행
- Rollback
- `--skip-update-once`
- 동일 버전 누락 파일 복구

---

## 4. 기존 설치 프로그램 전환

기존 설치 프로그램은 `PCCAM`으로 업데이트를 조회하므로 신규 제품을 추가하는 것만으로 자동 전환되지 않는다.

### 4.1 전환 순서

1. 서버와 UpdateClient가 `PCCAM`, `PCCAM_X86`, `PCCAM_X64`를 모두 지원한다.
2. 기존 `PCCAM` 제품에 마지막 전환 릴리스를 등록한다.
3. 전환 릴리스에는 신규 UpdateClient와 신규 제품 코드를 전달하는 PC CAM 실행 파일을 포함한다.
4. 업데이트 적용 후 재실행부터 `PCCAM_X86` 또는 `PCCAM_X64`로 조회한다.
5. 신규 제품별 기준 릴리스를 등록한다.
6. 모든 설치처 전환을 확인한 뒤 레거시 `PCCAM`의 신규 배포를 중단한다.

### 4.2 전환 전후

```text
전환 전: PCCAM + x86
전환 후: PCCAM_X86 + x86
```

```text
전환 전: PCCAM + x64
전환 후: PCCAM_X64 + x64
```

기존 릴리스 이력은 레거시 제품에 그대로 유지하고 신규 제품으로 임의 이동하지 않는다.

---

## 5. 제한사항

### 5.1 기존 `PCCAM` 즉시 제거 금지

기존 설치 프로그램의 전환이 완료되기 전까지 `PCCAM` 제품을 삭제하거나 비활성화하지 않는다.

### 5.2 기존 Published 릴리스 직접 변경 금지

기존 Published 릴리스의 제품 코드, Artifact 또는 아키텍처를 직접 변경하지 않는다. 신규 변경은 Draft 생성, Artifact 업로드, 검증, Publish 절차를 따른다.

### 5.3 기존 릴리스 이력 임의 이동 금지

기존 `PCCAM` 릴리스를 `PCCAM_X86` 또는 `PCCAM_X64`로 일괄 변경하지 않는다. Artifact, Manifest, 감사 로그, 다운로드 경로 및 게시 이력을 보존한다.

### 5.4 DB 릴리스 스키마 변경 금지

이번 작업에서는 릴리스와 Artifact 테이블에 아키텍처 컬럼을 추가하거나 중복 키 구조를 변경하지 않는다.

### 5.5 버전번호 강제 통일 금지

32비트와 64비트 버전을 맞추기 위해 실제 Assembly/File Version을 임의 변경하지 않는다. 각 프로그램의 실제 버전을 그대로 사용한다.

### 5.6 아키텍처 값 제거 금지

제품 코드가 분리되어도 업데이트 요청의 `architecture`를 유지한다. 제품 코드와 아키텍처를 이중 검증한다.

### 5.7 운영체제 비트 수로 제품 자동 판별 금지

64비트 Windows에서도 32비트 PC CAM이 실행될 수 있으므로 `Environment.Is64BitOperatingSystem`을 제품 코드 판단 기준으로 사용하지 않는다.

### 5.8 UpdateClient 이원화 금지

32비트용과 64비트용 UpdateClient 프로젝트를 별도로 만들지 않는다. 하나의 `POSCAM.UpdateClient.exe`를 공용으로 사용한다.

### 5.9 UpdateClient 핵심 엔진 재설계 금지

다음 로직은 이번 작업에서 변경하지 않는다.

```text
업데이트 API 호출
Full Package 및 증분 파일 다운로드
파일 크기와 SHA-256 검증
백업 및 파일 교체
관리 대상 파일 삭제
Rollback
프로세스 재실행
--skip-update-once
동일 버전 파일 복구
```

### 5.10 CamViewer 동작 변경 금지

`CAMVIEWER` 제품 코드와 업데이트 정책은 변경하지 않는다. 신규 UpdateClient 바이너리를 공유하더라도 기존 동작을 유지해야 한다.

### 5.11 운영 DB 직접 변경 금지

저장소에는 반복 실행 가능한 마이그레이션과 Seed를 추가하되 이번 코드 작업에서 운영 DB에 직접 적용하지 않는다.

---

## 6. 작업 제외 범위

- UpdateServer 전체 아키텍처 변경
- 업데이트 API 버전 변경
- 인증서버 및 라이선스 정책 변경
- PC CAM 32비트와 64비트 소스 통합
- PC CAM 기능 변경
- CamViewer 기능 수정
- 새로운 패키지 형식 또는 델타 패치 추가
- 업데이트 UI 전면 개편
- 운영 서버 및 운영 DB 배포

---

## 7. 구현 순서

1. 작업 기준 문서 확정
2. UpdateServer 제품 코드 추가
3. DB 마이그레이션 및 Seed 추가
4. UpdateClient 제품 코드 추가 및 제품-아키텍처 검증
5. 서버 및 UpdateClient 단위 테스트 보강
6. 프로세스 통합 테스트 보강
7. API 계약 및 배포 문서 갱신
8. 전체 Release 빌드와 테스트
9. PC CAM 32비트 저장소 연동
10. PC CAM 64비트 저장소 연동
11. 레거시 `PCCAM` 전환 릴리스 준비

---

## 8. 완료 조건

- `PCCAM_X86`, `PCCAM_X64`가 별도 제품으로 조회된다.
- 두 제품에 동일 버전 릴리스를 각각 등록할 수 있다.
- 32비트는 `PCCAM_X86`, 64비트는 `PCCAM_X64`만 조회한다.
- 제품과 아키텍처가 일치하지 않으면 UpdateClient가 요청 전에 차단한다.
- 기존 `PCCAM` 클라이언트는 전환 릴리스를 받을 수 있다.
- 전환 후 설치 Manifest에 신규 제품 코드가 저장된다.
- 기존 다운로드, 검증, 적용, Rollback 동작이 유지된다.
- `CAMVIEWER`, `UPDATER`의 기존 동작이 유지된다.
- 관련 단위 테스트와 프로세스 통합 테스트가 모두 성공한다.
