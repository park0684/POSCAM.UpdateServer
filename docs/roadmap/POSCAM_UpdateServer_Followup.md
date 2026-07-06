# POSCAM UpdateServer Follow-up Roadmap

## Completed

- Manifest based artifact file generation
- update_artifact_files DB storage
- files[] response in update check
- files[] response in ALREADY_LATEST state
- packageUrl download check
- files[].downloadUrl download check
- PowerShell repair simulation
- Dockerfile based build
- deployment document

## Next

- Rotate internal service key before production deployment
- Apply update_artifact_files migration to production DB
- Deploy AuthServer with correct secrets
- Deploy UpdateServer with correct secrets
- Configure AdminWeb UpdateApiSettings for production
- Verify health endpoint
- Verify update check endpoint
- Verify packageUrl and files[].downloadUrl

## Client updater later

- Implement update check client
- Implement manifest repair planner
- Implement file downloader
- Verify size and SHA-256 before apply
- Replace only missing or corrupted files
- Keep full ZIP update path for version upgrades
