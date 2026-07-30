USE poscam_update;

-- PC CAM 32비트와 64비트는 서로 다른 버전 계열을 사용하므로
-- 릴리스 이력을 독립 관리할 수 있도록 제품 코드를 분리한다.
-- 기존 PCCAM은 설치 프로그램 전환이 완료될 때까지 유지한다.
INSERT INTO update_products
(
    prd_code,
    prd_name,
    prd_description,
    prd_status,
    prd_idate,
    prd_udate
)
VALUES
(
    'PCCAM_X86',
    'POSCAM PC CAM 32비트',
    'Windows POS 화면 캡처 및 NVR 송출 프로그램 32비트',
    1,
    UTC_TIMESTAMP(),
    NULL
),
(
    'PCCAM_X64',
    'POSCAM PC CAM 64비트',
    'Windows POS 화면 캡처 및 NVR 송출 프로그램 64비트',
    1,
    UTC_TIMESTAMP(),
    NULL
)
ON DUPLICATE KEY UPDATE
    prd_name = VALUES(prd_name),
    prd_description = VALUES(prd_description),
    prd_status = VALUES(prd_status),
    prd_udate = UTC_TIMESTAMP();

-- 레거시 제품은 전환 릴리스를 제공할 수 있도록 활성 상태를 유지한다.
UPDATE update_products
SET prd_name = 'POSCAM PC CAM',
    prd_description = '기존 PC CAM 설치 프로그램의 신규 제품 코드 전환용 레거시 제품',
    prd_status = 1,
    prd_udate = UTC_TIMESTAMP()
WHERE prd_code = 'PCCAM';
