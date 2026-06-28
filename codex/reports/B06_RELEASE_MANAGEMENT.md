# B06 릴리스 관리 구현 보고

## 구현 범위

- `GET /api/v1/admin/products/active`
- `GET /api/v1/admin/releases`
- `GET /api/v1/admin/releases/{releaseCode}`
- `POST /api/v1/admin/releases`
- `PUT /api/v1/admin/releases/{releaseCode}`
- `DELETE /api/v1/admin/releases/{releaseCode}`
- 제품·채널·상태·검색어 필터
- Page/PageSize 기반 페이징
- Draft 생성·수정·삭제
- CREATE·UPDATE·DELETE_DRAFT 감사 로그
- Release 목록·상세 DTO와 Artifact 요약
- 관리 조회 Repository와 `FOR UPDATE` 잠금 조회
- 서비스·SQL·관리자 Route 단위 테스트

## 관리 경로 보호

모든 API는 다음 Prefix 아래에 있다.

```text
/api/v1/admin/*
```

따라서 B05 `UpdateManagementAuthorizationMiddleware`가 매 요청마다 AuthServer 권한 확인을 수행한다. 관리자 Controller와 Action에는 `AllowAnonymous`를 사용하지 않는다.

## Draft 생성·수정 정책

- Product: `PCCAM`, `CAMVIEWER`, `UPDATER`
- Channel: `stable`, `beta`, `internal`
- Version: 확정된 3자리·4자리 숫자 버전 형식
- `1.2.0.0`은 `1.2.0`으로 정규화
- Active Product에만 생성·수정 가능
- 전체 강제와 기준 버전 미만 강제를 동시에 사용할 수 없음
- 강제 기준 버전은 릴리스 버전보다 높을 수 없음
- Product+Channel+숫자 버전 중복은 HTTP 409 / 8011

## 상태 정책

- Draft만 수정·삭제 가능
- Published·Disabled 수정 금지
- Published·Disabled 삭제 금지
- 상태 충돌은 HTTP 409 / 8012
- 수정·삭제는 `SELECT ... FOR UPDATE`로 상태 경쟁을 차단

## 트랜잭션과 감사

CREATE·UPDATE·DELETE_DRAFT는 다음 작업을 하나의 DB 트랜잭션으로 처리한다.

```text
Release 작업
+ Audit Log 저장
+ Commit
```

감사 Actor는 요청 본문이 아니라 B05 AuthServer 성공 결과의 Scoped Actor를 사용한다.

감사 항목:

- Actor UserCode·UserName
- Before/After JSON
- IP
- User-Agent
- Request ID

실패 시 Release 작업과 감사 로그를 함께 Rollback한다.

## Draft 삭제와 Artifact

DB FK가 `ON DELETE RESTRICT`이므로 Artifact가 연결된 Draft는 현재 HTTP 409 / 8012로 차단한다. B07에서 실제 파일과 Artifact 메타데이터 수명주기가 구현된 뒤 안전한 정리 정책과 함께 확장한다.

## 목록 조회

필터:

- ProductCode
- Channel
- Status
- Keyword: Version, ReleaseNotes, InternalMemo, CreatedByUserName

정렬:

```text
Major DESC
Minor DESC
Build DESC
Revision DESC
ReleaseCode DESC
```

문자열 버전 정렬은 사용하지 않는다.

## 금지 범위 준수

B06에서는 다음 기능을 구현하지 않았다.

- Artifact 업로드·교체
- Release 게시
- Release 배포 중지
- Product CRUD
- Published 핵심 정보 수정

## 검증 상태

- B05: 사용자 로컬 Release 빌드 성공, 149/149 테스트 성공
- B06: 정적 검토 완료
- B06 Release 빌드와 전체 테스트는 사용자 로컬 확인 필요
