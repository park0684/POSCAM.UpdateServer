# API 계약

모든 JSON은 `ApiResponse<T>`를 사용한다.

## 공개 Update Check

```http
POST /api/v1/updates/check
Content-Type: application/json
```

```json
{
  "productCode": "PCCAM",
  "currentVersion": "1.0.0",
  "os": "windows",
  "architecture": "x86",
  "channel": "stable"
}
```

업데이트 응답 Data:
- updateAvailable
- mandatory
- reasonCode
- productCode
- currentVersion
- latestVersion
- forceUpdateBelowVersion
- channel
- os
- architecture
- packageType
- packageUrl
- fileName
- fileSize
- sha256
- releaseNotes
- publishedAt

## 관리자 경로

```text
GET    /api/v1/admin/products/active
GET    /api/v1/admin/releases
POST   /api/v1/admin/releases
GET    /api/v1/admin/releases/{releaseCode}
PUT    /api/v1/admin/releases/{releaseCode}
DELETE /api/v1/admin/releases/{releaseCode}
POST   /api/v1/admin/releases/{releaseCode}/artifacts
POST   /api/v1/admin/releases/{releaseCode}/publish
POST   /api/v1/admin/releases/{releaseCode}/disable
POST   /api/v1/admin/artifacts/{artifactCode}/quarantine
GET    /api/v1/admin/audit-logs
GET    /api/v1/admin/releases/{releaseCode}/audit-logs
```

Artifact multipart 필드: `os`, `architecture`, `packageType`, `file`.

### 상태 작업

- Publish: Draft → Published. 게시 직전 활성 Artifact의 파일 존재, 크기, SHA-256, ZIP을 재검증한다.
- Disable: Published → Disabled. 일반 배포 중지는 Artifact 상태와 package 파일을 변경하지 않는다.
- Quarantine: Published 또는 Disabled Release의 Artifact를 Disabled로 바꾸고 package 파일을 `.quarantine`으로 이동한다. Published Release는 함께 Disabled로 전환한다.
- Published → Draft와 Disabled → Published는 허용하지 않는다.

### 감사 필터

`GET /api/v1/admin/audit-logs` Query:

- `action`
- `targetType`
- `targetCode`
- `actorUserCode`
- `requestId`
- `fromUtc`
- `toUtc`
- `page`
- `pageSize`

`GET /api/v1/admin/releases/{releaseCode}/audit-logs`는 해당 Release 작업과 현재 연결된 Artifact 작업을 함께 반환한다. `action`, `page`, `pageSize`를 지원한다.

페이징:

```json
{"items":[],"page":1,"pageSize":20,"totalCount":0,"totalPages":0}
```
