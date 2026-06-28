# 저장소 정책

```text
/app/update-storage/
├─ packages/
├─ .staging/
└─ .quarantine/
```

- Draft만 업로드
- 최대 1GB
- 서버가 파일명·Public ID·Storage Key 생성
- staging 기록 중 크기·SHA-256 계산
- ZIP 열기, Zip Slip, Entry 수, Expanded Bytes 검증
- 검증 후 packages 이동
- DB 실패 시 파일 제거 또는 quarantine
- 게시 직전 파일 존재·크기·SHA-256·ZIP 재검증
- Published 파일 불변
