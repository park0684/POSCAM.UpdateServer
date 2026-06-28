# 운영 배포 체크리스트

## 배포 전
- [ ] 실제 Secret Git 미포함
- [ ] poscam_update DB와 전용 계정
- [ ] Migration 수동 검토
- [ ] DB와 package 백업
- [ ] 명시적 이미지 태그
- [ ] 양쪽 내부 서비스 키
- [ ] poscam-internal 네트워크

## 설치
- [ ] V001 적용
- [ ] Seed 적용
- [ ] packages/.staging/.quarantine 생성
- [ ] 파일 권한 설정
- [ ] 컨테이너 실행
- [ ] live/ready
- [ ] Nginx 문법
- [ ] IIS·DNS·인증서
- [ ] /api/internal 외부 404
- [ ] Update Check
- [ ] 테스트 업로드·게시·다운로드·SHA-256·Range

## 장애 확인
- [ ] AuthServer 중단 시 공개 확인 정상
- [ ] AuthServer 중단 시 관리자 API 503
- [ ] 로그에 토큰·Secret 없음
