USE poscam_update;

CREATE TABLE IF NOT EXISTS update_artifact_files
(
    file_code BIGINT NOT NULL AUTO_INCREMENT,
    artifact_code BIGINT NOT NULL,
    file_public_id CHAR(32) NOT NULL,
    file_path VARCHAR(500) NOT NULL,
    file_size BIGINT NOT NULL,
    file_sha256 CHAR(64) NOT NULL,
    file_storage_key VARCHAR(700) NOT NULL,
    file_download_path VARCHAR(700) NOT NULL,
    is_required TINYINT NOT NULL DEFAULT 1,
    file_status TINYINT NOT NULL DEFAULT 1,
    created_at DATETIME NOT NULL,
    CONSTRAINT pk_update_artifact_files PRIMARY KEY (file_code),
    CONSTRAINT fk_update_artifact_files_artifact
        FOREIGN KEY (artifact_code) REFERENCES update_artifacts (art_code)
        ON DELETE RESTRICT ON UPDATE RESTRICT,
    CONSTRAINT uq_update_artifact_files_public_id UNIQUE (file_public_id),
    CONSTRAINT uq_update_artifact_files_storage_key UNIQUE (file_storage_key),
    CONSTRAINT uq_update_artifact_files_path UNIQUE (artifact_code, file_path),
    INDEX ix_update_artifact_files_artifact (artifact_code),
    INDEX ix_update_artifact_files_status (artifact_code, file_status)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
