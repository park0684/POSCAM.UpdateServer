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
GET    /api/v1/admin/audit-logs
GET    /api/v1/admin/releases/{releaseCode}/audit-logs
```

Artifact multipart 필드: `os`, `architecture`, `packageType`, `file`.

페이징:
```json
{"items":[],"page":1,"pageSize":20,"totalCount":0,"totalPages":0}
```
