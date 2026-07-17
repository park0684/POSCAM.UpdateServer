# B03 Dapper DB 계층 구현 보고

## 구현 범위

- `IDbContext`, `DapperContext`
- Dapper underscore 매핑 활성화
- `ConnectionStrings:DefaultConnection` 사용
- MySqlConnector 연결 생성 및 비동기 Open
- DB 오류 분류와 안전한 `UpdateDatabaseException`
- 외부 트랜잭션 재사용이 가능한 Repository 공통 기반
- Product, Release, Artifact, Audit Repository
- Published Release와 호환 Artifact를 한 번에 조회하는 Projection
- Repository DI 등록
- DB 설정·오류 분류·SQL 계약 단위 테스트

## Repository 정책

- 모든 SQL은 `poscam_update`의 비한정 테이블명만 사용한다.
- AuthServer DB SQL을 사용하지 않는다.
- 조회 컬럼은 Entity 속성명으로 명시적 Alias를 사용한다.
- 모든 쓰기 시각은 `UTC_TIMESTAMP()`를 사용한다.
- 릴리스 버전은 Major, Minor, Build, Revision 숫자 컬럼으로 정렬한다.
- 동일 버전에서는 exact architecture를 `any`보다 우선한다.
- 모든 조건값은 Dapper Parameter로 전달한다.
- 실제 ZIP 데이터는 DB에 저장하지 않는다.
- Migration을 실행하거나 자동 적용하지 않는다.

## DB 오류 구분

- 1062: Duplicate
- 1451, 1452: ForeignKeyViolation
- 연결·인증·DB 선택 오류: ConnectionFailed
- 기타 공급자 오류: Unknown

Connection String, SQL 상세, 비밀번호는 외부 오류 메시지에 포함하지 않는다.

## 트랜잭션

Repository 메서드는 선택적으로 `IDbTransaction`을 받는다.

- 트랜잭션이 있으면 해당 연결을 재사용한다.
- 트랜잭션이 없으면 Repository가 연결을 열고 해제한다.
- 향후 릴리스·Artifact·감사 로그를 하나의 업무 트랜잭션으로 묶을 수 있다.

## 검증 상태

- 정적 코드 및 SQL 정책 점검 완료
- 실제 MariaDB 연결 테스트는 운영 Migration 이후 별도 수행
- Release 빌드와 전체 단위 테스트는 사용자 로컬 환경에서 확인 필요
