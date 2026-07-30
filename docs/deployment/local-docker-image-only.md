# Local Docker image-only deployment

This deployment path replaces only the local UpdateServer container image.
It does not inspect or modify update product data and does not execute SQL.

## Run

```powershell
Set-Location D:\_work\POSCAM.UpdateServer

git checkout master
git pull

powershell -ExecutionPolicy Bypass `
  -File .\tools\Deploy-LocalDockerImageOnly.ps1
```

## Scope

- Build `poscam-update-server:local`
- Preserve the existing `poscam-update-server-local` container under a timestamped backup name
- Start the new container on `127.0.0.1:8083`
- Verify live/ready health and update-storage write access
- Restore the previous container automatically when verification fails

## Excluded

- Database schema changes
- Product master seed or update
- `PCCAM`, `PCCAM_X86`, or `PCCAM_X64` validation
- Production container or production database access

The backup container is retained by default. Remove it only after local verification is complete.
