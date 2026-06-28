# 도메인 정책

## Product
- PCCAM, CAMVIEWER, UPDATER
- Active=1, Disabled=9
- 물리 삭제 금지

## Release
- Draft=0, Published=1, Disabled=9
- 상태 전이: Draft → Published → Disabled
- Draft: 수정·Artifact 업로드·교체·삭제
- Published: 핵심정보·Artifact 불변, Disable만 가능
- Disabled: 조회·감사만 가능, 재게시 금지

## Artifact
- OS: windows
- Architecture: x86, x64, any
- PackageType: full
- Active=1, Disabled=9
- 동일 Release에서 exact architecture가 any보다 우선

## 감사
CREATE, UPDATE, UPLOAD, REPLACE_DRAFT_ARTIFACT, PUBLISH, DISABLE, DELETE_DRAFT를 기록하며 수정·삭제하지 않는다.
