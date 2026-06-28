# 테스트 정책

## 버전
- 1.10.0 > 1.9.0
- 1.2.0 == 1.2.0.0
- Revision 정규화
- 잘못된 형식·범위

## 강제
- 전체 강제
- 기준 미만 강제
- 경계 동일은 선택
- 업데이트 없음 mandatory=false
- 다운그레이드 없음

## 상태
- Draft→Published
- Published→Disabled
- 역전이 금지
- Published 수정 금지

## Storage
- 정상·손상 ZIP
- Zip Slip·절대경로
- Entry·Expanded 제한
- Root Path 탈출
- SHA-256 일치·불일치

## AuthServer Client
- 성공, 401, 403, Timeout, 연결 실패, 비정상 JSON

실제 운영 DB와 경로를 사용하지 않는다.
