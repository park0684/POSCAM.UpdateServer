# POSCAM.UpdateServer Codex 규칙

## 작업 원칙
1. 현재 파일 전체를 읽고 수정한다.
2. `DECISIONS.md`와 `docs`를 최우선으로 따른다.
3. 단계 범위 밖 기능을 선행 구현하지 않는다.
4. 빌드·테스트 실패 상태에서 다음 단계로 이동하지 않는다.
5. 완료 단계 설계를 후속 단계에서 임의 변경하지 않는다.
6. 운영 Migration, Secret 변경, commit, push, deploy를 수행하지 않는다.

## 절대 금지
- AccountToken 직접 파싱 또는 검증
- AuthServer TokenSecret 설정
- `poscam_auth` DB 조회
- AuthServer 프로젝트 또는 DLL 참조
- ZIP 바이너리 DB 저장
- Published Release·Artifact 수정 API
- 자동 DB Migration
- 실제 운영 경로 테스트
- 토큰·서비스 키·비밀번호·Connection String 로그

## 서비스 경계
- 공개 Update Check는 익명.
- 관리자 API는 요청마다 AuthServer 권한 확인.
- package 다운로드는 Nginx 담당.
- AuthServer 장애 시 공개 확인과 기존 다운로드는 계속 가능.
