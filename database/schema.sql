CREATE DATABASE IF NOT EXISTS poscam_update
    CHARACTER SET utf8mb4
    COLLATE utf8mb4_unicode_ci;

USE poscam_update;

CREATE TABLE IF NOT EXISTS update_products
(
    prd_code VARCHAR(30) NOT NULL,
    prd_name VARCHAR(100) NOT NULL,
    prd_description VARCHAR(500) NULL,
    prd_status TINYINT NOT NULL,
    prd_idate DATETIME NOT NULL,
    prd_udate DATETIME NULL,
    CONSTRAINT pk_update_products PRIMARY KEY (prd_code),
    INDEX ix_update_products_status (prd_status)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

CREATE TABLE IF NOT EXISTS update_releases
(
    rel_code BIGINT NOT NULL AUTO_INCREMENT,
    rel_product_code VARCHAR(30) NOT NULL,
    rel_version VARCHAR(30) NOT NULL,
    rel_version_major INT NOT NULL,
    rel_version_minor INT NOT NULL,
    rel_version_build INT NOT NULL,
    rel_version_revision INT NOT NULL,
    rel_channel VARCHAR(20) NOT NULL,
    rel_force_update_below_version VARCHAR(30) NULL,
    rel_is_mandatory TINYINT NOT NULL,
    rel_release_notes TEXT NULL,
    rel_internal_memo TEXT NULL,
    rel_status TINYINT NOT NULL,
    rel_published_at DATETIME NULL,
    rel_created_by_user_code INT NULL,
    rel_created_by_user_name VARCHAR(100) NULL,
    rel_idate DATETIME NOT NULL,
    rel_udate DATETIME NULL,
    CONSTRAINT pk_update_releases PRIMARY KEY (rel_code),
    CONSTRAINT fk_update_releases_product
        FOREIGN KEY (rel_product_code) REFERENCES update_products (prd_code)
        ON DELETE RESTRICT ON UPDATE RESTRICT,
    CONSTRAINT uq_update_releases_version UNIQUE
    (
        rel_product_code, rel_channel,
        rel_version_major, rel_version_minor,
        rel_version_build, rel_version_revision
    ),
    INDEX ix_update_releases_lookup
    (
        rel_product_code, rel_channel, rel_status,
        rel_version_major, rel_version_minor,
        rel_version_build, rel_version_revision
    ),
    INDEX ix_update_releases_published_at (rel_published_at)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

CREATE TABLE IF NOT EXISTS update_artifacts
(
    art_code BIGINT NOT NULL AUTO_INCREMENT,
    art_release_code BIGINT NOT NULL,
    art_public_id CHAR(32) NOT NULL,
    art_os VARCHAR(20) NOT NULL,
    art_architecture VARCHAR(10) NOT NULL,
    art_package_type VARCHAR(20) NOT NULL,
    art_file_name VARCHAR(255) NOT NULL,
    art_storage_key VARCHAR(500) NOT NULL,
    art_content_type VARCHAR(100) NOT NULL,
    art_file_size BIGINT NOT NULL,
    art_sha256 CHAR(64) NOT NULL,
    art_signature LONGTEXT NULL,
    art_status TINYINT NOT NULL,
    art_idate DATETIME NOT NULL,
    art_udate DATETIME NULL,
    CONSTRAINT pk_update_artifacts PRIMARY KEY (art_code),
    CONSTRAINT fk_update_artifacts_release
        FOREIGN KEY (art_release_code) REFERENCES update_releases (rel_code)
        ON DELETE RESTRICT ON UPDATE RESTRICT,
    CONSTRAINT uq_update_artifacts_public_id UNIQUE (art_public_id),
    CONSTRAINT uq_update_artifacts_storage_key UNIQUE (art_storage_key),
    CONSTRAINT uq_update_artifacts_target UNIQUE
    (
        art_release_code, art_os, art_architecture, art_package_type
    ),
    INDEX ix_update_artifacts_lookup
    (
        art_release_code, art_os, art_status, art_package_type, art_architecture
    )
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

CREATE TABLE IF NOT EXISTS update_audit_logs
(
    ual_code BIGINT NOT NULL AUTO_INCREMENT,
    ual_action VARCHAR(30) NOT NULL,
    ual_target_type VARCHAR(30) NOT NULL,
    ual_target_code VARCHAR(100) NOT NULL,
    ual_actor_user_code INT NULL,
    ual_actor_user_name VARCHAR(100) NULL,
    ual_before_data LONGTEXT NULL,
    ual_after_data LONGTEXT NULL,
    ual_ip_address VARCHAR(45) NULL,
    ual_user_agent VARCHAR(500) NULL,
    ual_request_id VARCHAR(100) NULL,
    ual_idate DATETIME NOT NULL,
    CONSTRAINT pk_update_audit_logs PRIMARY KEY (ual_code),
    INDEX ix_update_audit_logs_target
        (ual_target_type, ual_target_code, ual_idate),
    INDEX ix_update_audit_logs_actor
        (ual_actor_user_code, ual_idate),
    INDEX ix_update_audit_logs_action
        (ual_action, ual_idate),
    INDEX ix_update_audit_logs_request_id
        (ual_request_id)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
