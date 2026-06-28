# 운영 배포 체크리스트

## 배포 전
- [ ] 실제 Secret Git 미포함
- [ ] poscam_update DB와 전용 계정
- [ ] Migration 수동 검토
- [ ] DB와 package 백업
- [ ] 명시적 이미지 태그
- [ ] 양쪽 내부 서비스 키 32자 이상
- [ ] poscam-internal 네트워크
- [ ] Nginx가 컨테이너에서 보이는 단일 프록시 IP 확인
- [ ] `UPDATE_TRUSTED_PROXY_IP`에 확인한 IP만 설정
- [ ] AdminWeb CORS Origin 확인

## 이미지
- [ ] `docker build -t poscam-update-server:<version> .` 성공
- [ ] 이미지 태그에 `latest` 미사용
- [ ] 컨테이너 실행 사용자가 root가 아님
- [ ] read-only root filesystem과 `/tmp` tmpfs 확인

## 설치
- [ ] V001 적용
- [ ] Seed 적용
- [ ] `/var/poscam/update-storage/packages` 생성
- [ ] `/var/poscam/update-storage/.staging` 생성
- [ ] `/var/poscam/update-storage/.quarantine` 생성
- [ ] Host 저장소를 컨테이너 `app` 사용자 UID/GID가 쓰고 Nginx가 packages를 읽을 수 있게 설정
- [ ] 컨테이너 실행
- [ ] `/health/live` 200
- [ ] `/health/ready` 200
- [ ] AuthServer 중단이 `/health/ready`에 영향을 주지 않음
- [ ] Nginx 문법 검사
- [ ] IIS·DNS·인증서
- [ ] `/api/internal` 외부 404
- [ ] 허용 Origin CORS preflight 성공
- [ ] 미허용 Origin에 CORS 허용 Header 없음
- [ ] Update Check 60회/60초 제한과 429 응답 확인
- [ ] Update Check
- [ ] 테스트 업로드·게시·다운로드·SHA-256·Range

## 로그·보안
- [ ] 응답 `X-Request-ID`와 API·감사 로그 Request ID 연결
- [ ] 로그에 Authorization·Cookie·내부 서비스 키 없음
- [ ] 외부 전달 IP가 신뢰 프록시를 통해서만 반영됨
- [ ] Swagger가 운영 환경에서 공개되지 않음
- [ ] 자동 Migration이 실행되지 않음

## 장애 확인
- [ ] AuthServer 중단 시 공개 확인 정상
- [ ] AuthServer 중단 시 관리자 API 503
- [ ] DB 중단 시 live 200, ready 503
- [ ] Storage 쓰기 불가 시 live 200, ready 503
