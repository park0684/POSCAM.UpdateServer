# B04 공개 Update Check 구현 보고

## 구현 범위

- `POST /api/v1/updates/check`
- 익명 호출을 명시하는 `AllowAnonymous`
- Update Check Request·Response DTO
- 입력 검증과 오류 코드 변환
- Active Product 확인
- 호환되는 가장 높은 Published Release와 Artifact 조회
- Published 존재 여부를 이용한 업데이트 없음 사유 구분
- 일반·전체 강제·기준 버전 미만 강제 판정
- PublicBaseUrl과 Storage Key를 이용한 Package URL 생성
- 응답 캐시 금지
- 잘못된 JSON도 `ApiResponse<T>` 형식으로 반환
- Service·Controller·SQL 계약 단위 테스트

## 요청 검증

다음 값은 자동 보정하지 않고 정확히 일치해야 한다.

- Product: `PCCAM`, `CAMVIEWER`, `UPDATER`
- Version: 3자리 또는 4자리 숫자 버전
- OS: `windows`
- Client Architecture: `x86`, `x64`
- Channel: `stable`, `beta`, `internal`

잘못된 값은 HTTP 400과 각 전용 ErrorCode를 반환한다.

## 정상 reasonCode

- `UPDATE_AVAILABLE`
- `MANDATORY_RELEASE`
- `FORCE_UPDATE_BELOW_VERSION`
- `ALREADY_LATEST`
- `CLIENT_VERSION_AHEAD`
- `NO_AVAILABLE_RELEASE`
- `NO_COMPATIBLE_ARTIFACT`

업데이트가 없더라도 HTTP 200, Success=true를 사용한다.

## Package URL

형식:

```text
{PublicBaseUrl}/packages/{StorageKey}
```

- Storage Key의 각 경로 구간을 URL Encoding한다.
- 절대 경로, Windows 경로 구분자, `.`과 `..` 구간은 거부한다.
- 응답의 Architecture는 실제 선택된 Artifact의 `x86`, `x64`, `any`를 반환한다.

## 캐시

Update Check 응답에는 다음을 적용한다.

```text
Cache-Control: no-store
Pragma: no-cache
Expires: 0
```

정적 Package의 immutable cache 정책은 Nginx에서 별도로 적용한다.

## AuthServer 독립성

공개 Update Check는 다음에 의존하지 않는다.

- AccountToken
- AuthServer 내부 API
- UpdateManage 권한
- 제품 라이선스

## B03 최소 보완

`NO_AVAILABLE_RELEASE`와 `NO_COMPATIBLE_ARTIFACT`를 구분하기 위해 Release Repository에 `HasPublishedReleaseAsync`를 추가했다.

## 검증 상태

- B02+B03: 사용자 로컬 Release 빌드 성공, 94/94 테스트 성공
- B04: 정적 검토 완료
- B04 Release 빌드와 전체 테스트는 사용자 로컬 확인 필요
