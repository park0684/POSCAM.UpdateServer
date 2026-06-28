# 보안 정책

- AccountToken 직접 파싱·검증 금지.
- TokenSecret과 AuthServer DB 공유 금지.
- 관리자 권한 확인 실패 시 기본 거부.
- Secret과 Connection String을 Git·로그에 기록하지 않는다.
- 서버가 파일명과 경로를 생성한다.
- Root Path 탈출, Zip Slip, 손상 ZIP을 차단한다.
- Published package 불변.
- Update Check는 익명이지만 Rate Limit 적용.
- CORS는 AdminWeb Origin만 허용.
- `/api/internal/*` 외부 차단.
