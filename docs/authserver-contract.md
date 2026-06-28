# AuthServer 계약

설정:
```text
AuthServer__BaseUrl=http://poscam-auth-api:8080
AuthServer__InternalServiceKey={secret}
```

요청:
```http
POST /api/internal/update-management/authorize
Authorization: Bearer {accountToken}
X-POSCAM-Service-Key: {secret}
```

성공 Data: `userCode`, `userName`, `userRole`.

매핑:
- 401 → 401
- 403 → 403
- 연결 실패·Timeout·비정상 응답 → 503/9003

UpdateServer는 토큰을 분해하거나 payload를 읽지 않는다.
