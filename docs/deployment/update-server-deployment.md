# POSCAM UpdateServer Deployment

## Purpose

UpdateServer provides update check, release management, artifact upload, and manifest based file repair update for POSCAM products.

## Required secrets

AuthServer secrets:
- /run/secrets/AuthPolicy__InternalServiceKey
- /run/secrets/AuthPolicy__TokenSecret
- /run/secrets/ConnectionStrings__DefaultConnection

UpdateServer secrets:
- /run/secrets/AuthServer__InternalServiceKey
- /run/secrets/ConnectionStrings__DefaultConnection

AuthPolicy__InternalServiceKey on AuthServer and AuthServer__InternalServiceKey on UpdateServer must have the same value.
Rotate the internal service key before production deployment.

## DB migration

The update_artifact_files table is required for manifest based file repair update.

Required migration:
- database/migrations/20260704_add_update_artifact_files.sql

Production DB check:
- SHOW TABLES LIKE 'update_artifact_files';
- DESCRIBE update_artifact_files;

## Docker build

Run from repository root:

docker build -t poscam-update-server:2026.07.05 -f ./src/POSCAM.UpdateServer.Api/Dockerfile .

## Production run example

Linux server example:

docker run -d --name poscam-update-server --network poscam-internal -p 5002:8080 -v /data/poscam/update-storage:/app/update-storage -v /opt/poscam/secrets/internal_service_key.txt:/run/secrets/AuthServer__InternalServiceKey:ro -v /opt/poscam/secrets/update_connection_string.txt:/run/secrets/ConnectionStrings__DefaultConnection:ro -e ASPNETCORE_ENVIRONMENT=Production -e DOTNET_ENVIRONMENT=Production -e ASPNETCORE_HTTP_PORTS=8080 -e AuthServer__BaseUrl=http://poscam-auth-api:8080 -e UpdateStorage__RootPath=/app/update-storage -e UpdateStorage__PublicBaseUrl=https://update.poscam.co.kr poscam-update-server:2026.07.05

## Health check

Inside production server:

curl -i http://127.0.0.1:5002/health/ready

Expected:
- status: Healthy
- database: Healthy
- storage: Healthy

## Update check

Public endpoint:

POST https://update.poscam.co.kr/api/v1/updates/check

Request fields:
- productCode=PCCAM
- currentVersion=0.0.0
- os=windows
- architecture=x86
- channel=stable

Expected:
- JSON response
- success=true
- packageUrl exists when update is available
- files exists when compatible artifact exists

## Download URL check

packageUrl and files[].downloadUrl must return HTTP 200 OK.

## Post deployment checklist

- AdminWeb login works
- AdminWeb release management page works
- Artifact ZIP upload works
- Upload response has manifestFileCount greater than 0
- update_artifact_files rows are created
- Release publish works
- Update check response is valid
- packageUrl returns 200 OK
- files[].downloadUrl returns 200 OK

## Notes

- Do not copy local test release data to production DB.
- Rotate internal service key before production deployment.
- UpdateStorage__PublicBaseUrl must be a public URL reachable by clients.
- /packages path should allow GET and HEAD only.
