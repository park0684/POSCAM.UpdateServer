# 배포 정책

- 컨테이너: poscam-update-api
- 내부 8080
- 호스트 127.0.0.1:5002
- 네트워크 poscam-internal
- 도메인 https://update.poscam.co.kr

```text
IIS → Ubuntu Nginx
  ├─ /api/*      → 127.0.0.1:5002
  └─ /packages/* → /var/poscam/update-storage/packages
```

`/api/internal/*` 외부 차단.

Health:
- `/health/live`: 프로세스
- `/health/ready`: DB와 Storage
- AuthServer는 Ready 조건에서 제외

이미지에 latest를 사용하지 않는다.
