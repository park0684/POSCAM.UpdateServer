# 아키텍처

```text
Windows Client
  ├─ POST /api/v1/updates/check → UpdateServer
  └─ GET /packages/...          → Nginx

AdminWeb Server → UpdateServer JSON 관리자 API
Admin Browser   → UpdateServer multipart ZIP 직접 업로드

UpdateServer
  ├─ AuthServer 내부 권한 API
  ├─ poscam_update DB
  └─ /app/update-storage
```

권장 구조:
```text
src/POSCAM.UpdateServer.Api/
├─ Controllers
├─ Models
├─ Services
├─ Repositories
├─ Infrastructure
├─ Options
└─ Program.cs

tests/POSCAM.UpdateServer.Tests/
```

초기에는 Web API와 테스트 두 프로젝트로 시작하고 과도한 다중 프로젝트 분할을 피한다.
