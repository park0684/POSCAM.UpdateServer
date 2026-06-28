# B02 도메인·버전 구현 보고

## 구현 범위

- Product, Release, Artifact 상태 Enum
- Product/Channel/OS/Architecture/Package/Audit 고정 코드
- 숫자 기반 `UpdateVersion` 값 객체
- 버전 파싱 오류 모델
- 릴리스 강제 업데이트 정책 검증
- 업데이트 가능 여부·강제 여부 판정
- 릴리스 상태 전이 검증
- update_products, update_releases, update_artifacts, update_audit_logs 대응 Entity
- 버전·정책·상태·아키텍처 단위 테스트

## 핵심 정책

- 3자리 또는 4자리 숫자 버전만 허용
- 각 구성요소 0~65535
- 선행 0, 접두어, 접미어, 공백, prerelease 문자열 거부
- `1.2.0`과 `1.2.0.0` 동일
- Revision 0은 세 자리 문자열로 정규화
- 숫자 구성요소 비교로 `1.10.0 > 1.9.0`
- 전체 강제와 기준 버전 미만 강제는 동시 사용 금지
- 강제 기준 버전은 릴리스 버전보다 높을 수 없음
- 강제 기준과 같은 현재 버전은 강제하지 않음
- 현재 버전이 릴리스보다 높으면 다운그레이드하지 않음
- 상태 전이는 Draft → Published → Disabled만 허용
- Artifact exact architecture 우선순위 2, any 우선순위 1, 비호환 0

## 범위 밖

- Repository 및 SQL
- Controller와 API DTO
- 실제 Update Check Service
- AuthServer HttpClient
- 파일 Storage 처리

## 검증 상태

- 정적 코드 점검 완료
- Release 빌드와 전체 테스트는 사용자 로컬 환경에서 확인 필요
