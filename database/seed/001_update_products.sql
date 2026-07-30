USE poscam_update;

INSERT INTO update_products
(prd_code, prd_name, prd_description, prd_status, prd_idate, prd_udate)
VALUES
('PCCAM', 'POSCAM PC CAM', '기존 PC CAM 설치 프로그램의 신규 제품 코드 전환용 레거시 제품', 1, UTC_TIMESTAMP(), NULL),
('PCCAM_X86', 'POSCAM PC CAM 32비트', 'Windows POS 화면 캡처 및 NVR 송출 프로그램 32비트', 1, UTC_TIMESTAMP(), NULL),
('PCCAM_X64', 'POSCAM PC CAM 64비트', 'Windows POS 화면 캡처 및 NVR 송출 프로그램 64비트', 1, UTC_TIMESTAMP(), NULL),
('CAMVIEWER', 'POSCAM CamViewer', '거래 시각 기반 CCTV 및 POS 화면 재생 프로그램', 1, UTC_TIMESTAMP(), NULL),
('UPDATER', 'POSCAM Updater', 'POSCAM Windows 클라이언트 업데이트 적용 프로그램', 1, UTC_TIMESTAMP(), NULL)
ON DUPLICATE KEY UPDATE
prd_name=VALUES(prd_name),
prd_description=VALUES(prd_description),
prd_status=VALUES(prd_status),
prd_udate=UTC_TIMESTAMP();
