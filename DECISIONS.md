# 최종 확정 정책

이 문서는 가장 높은 우선순위를 가진다.

## 1. 관리자 인증
- UpdateServer는 AccountToken을 직접 검증하지 않는다.
- `POST /api/internal/update-management/authorize`를 호출한다.
- Bearer 토큰과 `X-POSCAM-Service-Key`를 전달한다.
- System 자동 허용, Admin은 `UpdateManage=12`, PartnerUser 거부.
- AuthServer 장애 시 관리자 API 503.
- 공개 Update Check와 기존 package 다운로드는 계속 동작.

## 2. 권한
- `AdminPermissionType.UpdateManage = 12`
- 표시명: `업데이트 관리`
- 설명: `프로그램 릴리스 등록, 패키지 업로드, 게시 및 배포 중지`
- 조회, 등록, 업로드, 게시, 중지, 감사 조회를 하나의 권한으로 관리.

## 3. API
```json
{"success":true,"message":"OK","errorCode":0,"data":{}}
```
- 의미 있는 HTTP 상태를 사용한다.
- UpdateServer 업무 오류는 8000번대.
- 인증 5000번대, 권한 7000번대, 공통 9000번대.
- 업데이트 없음은 200/Success=true이며 `reasonCode` 사용.

## 4. 공개 확인·다운로드
- `POST /api/v1/updates/check` 익명.
- 기본 Rate Limit: IP당 60회/60초.
- `/packages/*`는 Nginx 익명 정적 제공.
- `art_public_id` 사용, 디렉터리 목록 금지.
- Published URL 불변, package immutable cache, Update Check no-store.
- 파일 크기와 SHA-256 검증.

## 5. 버전
- `Major.Minor.Patch` 또는 `Major.Minor.Patch.Revision`
- 구성요소 0~65535.
- `1.2.0 == 1.2.0.0`, Revision 0은 세 자리로 정규화.
- 채널은 stable, beta, internal 정확 일치.
- 다운그레이드하지 않는다.
- `rel_force_update_below_version` 사용.
- `rel_is_mandatory`와 강제 기준은 동시 사용 금지.
- 호환 Artifact가 있는 가장 높은 Release 선택.

## 6. DB
- DB: `poscam_update`
- 테이블: products, releases, artifacts, audit_logs
- 실제 ZIP은 DB에 저장하지 않는다.
- AuthServer DB와 FK 없음.
- 상태 전이: Draft → Published → Disabled.
- Published 데이터와 파일은 불변.
- Repository SQL에서 `UTC_TIMESTAMP()` 사용.
- Migration 수동 적용.

## 7. 저장소
- 호스트 `/var/poscam/update-storage`
- 공개 `packages`, 비공개 `.staging`, `.quarantine`
- 최대 업로드 1GB.
- ZIP 구조, Zip Slip, Entry 수, Expanded Bytes 검증.
- SHA-256과 크기는 서버가 스트림 계산.
- 게시 직전 재검증.

## 8. AdminWeb
- 기존 AdminWeb에 추가.
- 기존 ApiClient는 AuthServer 전용, 신규 UpdateApiClient 추가.
- `GET /api/accounts/me/access` 추가.
- 1GB ZIP은 브라우저가 UpdateServer로 직접 업로드.
- XMLHttpRequest와 제한된 CORS 사용.

## 9. 배포
- UpdateServer 별도 Docker Compose.
- 외부 네트워크 `poscam-internal`.
- 컨테이너 8080, 호스트 `127.0.0.1:5002`.
- 도메인 `https://update.poscam.co.kr`.
- IIS → Ubuntu Nginx → API 또는 package.
- `/api/internal/*` 외부 차단.
- `latest` 태그 금지.
- live/ready 분리, Ready에 AuthServer 제외.

## 10. Codex
- A00~A05 → B00~B10 → C00~C05.
- 분석 단계 무수정.
- 단계별 허용 범위 준수.
- 빌드·테스트 실패 시 중단.
- WORK_STATUS 관리.
- 운영 Secret, Migration 실행, commit, push, deploy 금지.
