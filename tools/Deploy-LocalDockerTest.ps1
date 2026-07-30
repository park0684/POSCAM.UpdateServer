[CmdletBinding()]
param(
    [Parameter(Mandatory = $false)]
    [string]$ContainerName = "poscam-update-server-local",

    [Parameter(Mandatory = $false)]
    [string]$ImageName = "poscam-update-server:local",

    [Parameter(Mandatory = $false)]
    [string]$NetworkName = "poscam-internal",

    [Parameter(Mandatory = $false)]
    [int]$HostPort = 8083,

    [Parameter(Mandatory = $false)]
    [string]$AuthServerBaseUrl = "http://host.docker.internal:8081",

    [Parameter(Mandatory = $false)]
    [string]$UpdateStoragePublicBaseUrl = "http://127.0.0.1:8088",

    [Parameter(Mandatory = $false)]
    [string[]]$AdminWebOrigins = @(
        "http://127.0.0.1:8082",
        "http://localhost:8082"
    ),

    [Parameter(Mandatory = $false)]
    [string]$StorageHostPath = "D:\_data\poscam\update-storage",

    [Parameter(Mandatory = $false)]
    [string]$InternalServiceKeyPath = "D:\_work\poscam\secrets\internal_service_key.txt",

    [Parameter(Mandatory = $false)]
    [string]$ConnectionStringPath = "D:\_work\poscam\secrets\update_connection_string.txt",

    [Parameter(Mandatory = $false)]
    [string]$DatabaseContainer = "",

    [Parameter(Mandatory = $false)]
    [switch]$SkipMigration,

    [Parameter(Mandatory = $false)]
    [switch]$PullBaseImages,

    [Parameter(Mandatory = $false)]
    [switch]$RemoveBackupOnSuccess
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

function Write-Step {
    param([Parameter(Mandatory = $true)][string]$Message)

    Write-Host ""
    Write-Host "=== $Message ===" -ForegroundColor Cyan
}

function Invoke-Docker {
    param(
        [Parameter(Mandatory = $true)]
        [string[]]$Arguments,

        [Parameter(Mandatory = $false)]
        [switch]$Capture,

        [Parameter(Mandatory = $false)]
        [switch]$Sensitive
    )

    if ($Capture) {
        $output = & docker @Arguments 2>&1
        $exitCode = $LASTEXITCODE

        if ($exitCode -ne 0) {
            if ($Sensitive) {
                throw "Docker command failed. ExitCode=$exitCode"
            }

            throw "Docker command failed. ExitCode=$exitCode Arguments=$($Arguments -join ' ') Output=$($output -join [Environment]::NewLine)"
        }

        return (($output | ForEach-Object { $_.ToString() }) -join [Environment]::NewLine).Trim()
    }

    & docker @Arguments
    $exitCode = $LASTEXITCODE

    if ($exitCode -ne 0) {
        if ($Sensitive) {
            throw "Docker command failed. ExitCode=$exitCode"
        }

        throw "Docker command failed. ExitCode=$exitCode Arguments=$($Arguments -join ' ')"
    }
}

function Assert-File {
    param(
        [Parameter(Mandatory = $true)][string]$Path,
        [Parameter(Mandatory = $true)][string]$Description
    )

    if (-not (Test-Path -LiteralPath $Path -PathType Leaf)) {
        throw "$Description file was not found: $Path"
    }
}

function Assert-Directory {
    param(
        [Parameter(Mandatory = $true)][string]$Path,
        [Parameter(Mandatory = $true)][string]$Description
    )

    if (-not (Test-Path -LiteralPath $Path -PathType Container)) {
        throw "$Description directory was not found: $Path"
    }
}

function Get-ConnectionStringValue {
    param(
        [Parameter(Mandatory = $true)]
        [System.Data.Common.DbConnectionStringBuilder]$Builder,

        [Parameter(Mandatory = $true)]
        [string[]]$Keys,

        [Parameter(Mandatory = $false)]
        [string]$DefaultValue = ""
    )

    foreach ($key in $Keys) {
        if ($Builder.ContainsKey($key)) {
            return [string]$Builder[$key]
        }
    }

    return $DefaultValue
}

function Get-RunningContainerNames {
    $raw = Invoke-Docker -Arguments @("ps", "--format", "{{.Names}}") -Capture

    if ([string]::IsNullOrWhiteSpace($raw)) {
        return @()
    }

    return @($raw -split "`r?`n" | Where-Object { -not [string]::IsNullOrWhiteSpace($_) })
}

function Get-AllContainerNames {
    $raw = Invoke-Docker -Arguments @("ps", "-a", "--format", "{{.Names}}") -Capture

    if ([string]::IsNullOrWhiteSpace($raw)) {
        return @()
    }

    return @($raw -split "`r?`n" | Where-Object { -not [string]::IsNullOrWhiteSpace($_) })
}

function Resolve-DatabaseContainer {
    param(
        [Parameter(Mandatory = $true)][string]$ConfiguredContainer,
        [Parameter(Mandatory = $true)][string]$Server,
        [Parameter(Mandatory = $true)][int]$Port
    )

    $running = Get-RunningContainerNames

    if (-not [string]::IsNullOrWhiteSpace($ConfiguredContainer)) {
        if ($running -notcontains $ConfiguredContainer) {
            throw "Configured database container is not running: $ConfiguredContainer"
        }

        return $ConfiguredContainer
    }

    if ($running -contains $Server) {
        return $Server
    }

    if ($Port -eq 3307 -and $running -contains "poscam-db-new") {
        return "poscam-db-new"
    }

    if ($Port -eq 3306 -and $running -contains "poscam-db") {
        return "poscam-db"
    }

    if ($running -contains "poscam-db-new") {
        return "poscam-db-new"
    }

    if ($running -contains "poscam-db") {
        return "poscam-db"
    }

    throw "Unable to resolve the local MariaDB container. Running=$($running -join ', ') Server=$Server Port=$Port"
}

function Invoke-DatabaseScript {
    param(
        [Parameter(Mandatory = $true)][string]$DbContainer,
        [Parameter(Mandatory = $true)][string]$DbUser,
        [Parameter(Mandatory = $true)][string]$DbPassword,
        [Parameter(Mandatory = $true)][string]$DbName,
        [Parameter(Mandatory = $true)][string]$LocalScriptPath
    )

    $containerScriptPath = "/tmp/V002__split_pccam_products.sql"
    Invoke-Docker -Arguments @("cp", $LocalScriptPath, "${DbContainer}:$containerScriptPath")

    try {
        $shellCommand = @'
if command -v mariadb >/dev/null 2>&1; then
  mariadb --user="$DB_USER" --database="$DB_NAME" < /tmp/V002__split_pccam_products.sql
elif command -v mysql >/dev/null 2>&1; then
  mysql --user="$DB_USER" --database="$DB_NAME" < /tmp/V002__split_pccam_products.sql
else
  echo "MariaDB client was not found in the database container." >&2
  exit 127
fi
'@

        Invoke-Docker -Arguments @(
            "exec",
            "-e", "MYSQL_PWD=$DbPassword",
            "-e", "DB_USER=$DbUser",
            "-e", "DB_NAME=$DbName",
            $DbContainer,
            "sh", "-lc", $shellCommand
        ) -Sensitive
    }
    finally {
        try {
            Invoke-Docker -Arguments @("exec", $DbContainer, "rm", "-f", $containerScriptPath)
        }
        catch {
            Write-Warning "Failed to remove temporary migration file from $DbContainer. $($_.Exception.Message)"
        }
    }
}

function Get-ProductRows {
    param(
        [Parameter(Mandatory = $true)][string]$DbContainer,
        [Parameter(Mandatory = $true)][string]$DbUser,
        [Parameter(Mandatory = $true)][string]$DbPassword,
        [Parameter(Mandatory = $true)][string]$DbName
    )

    $query = "SELECT prd_code, prd_status FROM update_products WHERE prd_code IN ('PCCAM','PCCAM_X86','PCCAM_X64') ORDER BY prd_code;"
    $shellCommand = @'
if command -v mariadb >/dev/null 2>&1; then
  mariadb --user="$DB_USER" --database="$DB_NAME" --batch --skip-column-names --execute="$DB_QUERY"
elif command -v mysql >/dev/null 2>&1; then
  mysql --user="$DB_USER" --database="$DB_NAME" --batch --skip-column-names --execute="$DB_QUERY"
else
  echo "MariaDB client was not found in the database container." >&2
  exit 127
fi
'@

    return Invoke-Docker -Arguments @(
        "exec",
        "-e", "MYSQL_PWD=$DbPassword",
        "-e", "DB_USER=$DbUser",
        "-e", "DB_NAME=$DbName",
        "-e", "DB_QUERY=$query",
        $DbContainer,
        "sh", "-lc", $shellCommand
    ) -Capture -Sensitive
}

function Wait-ReadyHealth {
    param(
        [Parameter(Mandatory = $true)][string]$Uri,
        [Parameter(Mandatory = $false)][int]$Attempts = 30,
        [Parameter(Mandatory = $false)][int]$DelaySeconds = 2
    )

    $lastError = $null

    for ($attempt = 1; $attempt -le $Attempts; $attempt++) {
        try {
            $response = Invoke-RestMethod -Uri $Uri -Method Get -TimeoutSec 5
            return $response
        }
        catch {
            $lastError = $_
            Start-Sleep -Seconds $DelaySeconds
        }
    }

    throw "Ready health check failed: $Uri LastError=$($lastError.Exception.Message)"
}

function Invoke-UpdateCheck {
    param(
        [Parameter(Mandatory = $true)][string]$BaseUrl,
        [Parameter(Mandatory = $true)][string]$ProductCode,
        [Parameter(Mandatory = $true)][string]$Architecture
    )

    $request = @{
        productCode = $ProductCode
        currentVersion = "0.0.0"
        os = "windows"
        architecture = $Architecture
        channel = "stable"
    }

    $response = Invoke-RestMethod `
        -Uri "$BaseUrl/api/v1/updates/check" `
        -Method Post `
        -ContentType "application/json" `
        -Body ($request | ConvertTo-Json) `
        -TimeoutSec 15

    if ($null -eq $response.success -or -not [bool]$response.success) {
        throw "Update check failed. Product=$ProductCode Architecture=$Architecture Response=$($response | ConvertTo-Json -Depth 8 -Compress)"
    }

    return $response
}

function Test-CorsPreflight {
    param(
        [Parameter(Mandatory = $true)][string]$BaseUrl,
        [Parameter(Mandatory = $true)][string]$Origin
    )

    $headers = @{
        Origin = $Origin
        "Access-Control-Request-Method" = "POST"
        "Access-Control-Request-Headers" = "content-type"
    }

    $response = Invoke-WebRequest `
        -Uri "$BaseUrl/api/v1/updates/check" `
        -Method Options `
        -Headers $headers `
        -UseBasicParsing `
        -TimeoutSec 10

    $allowedOrigin = [string]$response.Headers["Access-Control-Allow-Origin"]

    if ($allowedOrigin -ne $Origin) {
        throw "CORS preflight did not allow the configured AdminWeb origin. Expected=$Origin Actual=$allowedOrigin"
    }
}

$scriptDirectory = Split-Path -Parent $MyInvocation.MyCommand.Path
$repositoryRoot = [System.IO.Path]::GetFullPath((Join-Path $scriptDirectory ".."))
$dockerfilePath = Join-Path $repositoryRoot "Dockerfile"
$migrationPath = Join-Path $repositoryRoot "database\migrations\V002__split_pccam_products.sql"
$reportDirectory = Join-Path $repositoryRoot "artifacts\local-docker-deploy"
$timestamp = Get-Date -Format "yyyyMMdd-HHmmss"
$backupContainerName = "$ContainerName-backup-$timestamp"
$baseUrl = "http://127.0.0.1:$HostPort"

Write-Step "Local target safety validation"

if ($ContainerName -ne "poscam-update-server-local") {
    throw "This script is restricted to the local test container: poscam-update-server-local"
}

if ($ImageName -ne "poscam-update-server:local") {
    throw "This script is restricted to the local test image: poscam-update-server:local"
}

if ($HostPort -ne 8083) {
    throw "This script is restricted to the local test host port 8083."
}

if (-not $UpdateStoragePublicBaseUrl.StartsWith("http://127.0.0.1:", [System.StringComparison]::OrdinalIgnoreCase)) {
    throw "UpdateStoragePublicBaseUrl must remain a loopback HTTP URL for this local deployment."
}

if ($AdminWebOrigins.Count -lt 1) {
    throw "At least one local AdminWeb CORS origin is required."
}

foreach ($origin in $AdminWebOrigins) {
    if (-not ($origin.StartsWith("http://127.0.0.1:", [System.StringComparison]::OrdinalIgnoreCase) -or
              $origin.StartsWith("http://localhost:", [System.StringComparison]::OrdinalIgnoreCase))) {
        throw "Only loopback AdminWeb origins are allowed by this local deployment script: $origin"
    }
}

if ($null -eq (Get-Command docker -ErrorAction SilentlyContinue)) {
    throw "Docker CLI was not found. Start Docker Desktop and ensure docker.exe is on PATH."
}

Assert-File -Path $dockerfilePath -Description "UpdateServer Dockerfile"
Assert-File -Path $migrationPath -Description "PCCAM product split migration"
Assert-File -Path $InternalServiceKeyPath -Description "Internal service key secret"
Assert-File -Path $ConnectionStringPath -Description "UpdateServer connection string secret"
Assert-Directory -Path $StorageHostPath -Description "Update storage"

$networkNames = Invoke-Docker -Arguments @("network", "ls", "--format", "{{.Name}}") -Capture
if (@($networkNames -split "`r?`n") -notcontains $NetworkName) {
    throw "Required Docker network was not found: $NetworkName"
}

Write-Step "Read local database target"

$connectionString = (Get-Content -LiteralPath $ConnectionStringPath -Raw).Trim()
if ([string]::IsNullOrWhiteSpace($connectionString)) {
    throw "UpdateServer connection string secret is empty."
}

$connectionBuilder = New-Object System.Data.Common.DbConnectionStringBuilder
$connectionBuilder.ConnectionString = $connectionString
$dbServer = Get-ConnectionStringValue -Builder $connectionBuilder -Keys @("Server", "Host", "Data Source")
$dbPortText = Get-ConnectionStringValue -Builder $connectionBuilder -Keys @("Port") -DefaultValue "3306"
$dbName = Get-ConnectionStringValue -Builder $connectionBuilder -Keys @("Database", "Initial Catalog")
$dbUser = Get-ConnectionStringValue -Builder $connectionBuilder -Keys @("User ID", "Uid", "Username", "User")
$dbPassword = Get-ConnectionStringValue -Builder $connectionBuilder -Keys @("Password", "Pwd")

[int]$dbPort = 0
if (-not [int]::TryParse($dbPortText, [ref]$dbPort)) {
    throw "Database port is invalid in the local connection string."
}

if ($dbName -ne "poscam_update") {
    throw "This migration is restricted to the local poscam_update database. Actual=$dbName"
}

if ([string]::IsNullOrWhiteSpace($dbUser) -or [string]::IsNullOrWhiteSpace($dbPassword)) {
    throw "Database user or password is missing from the local connection string."
}

$resolvedDatabaseContainer = Resolve-DatabaseContainer `
    -ConfiguredContainer $DatabaseContainer `
    -Server $dbServer `
    -Port $dbPort

Write-Host "Database container: $resolvedDatabaseContainer"
Write-Host "Database name: $dbName"

Write-Step "Build local UpdateServer image"

$buildArguments = @("build", "-f", "Dockerfile", "-t", $ImageName)
if ($PullBaseImages) {
    $buildArguments += "--pull"
}
$buildArguments += "."

Push-Location $repositoryRoot
try {
    Invoke-Docker -Arguments $buildArguments
}
finally {
    Pop-Location
}

$newImageId = Invoke-Docker -Arguments @("image", "inspect", "--format", "{{.Id}}", $ImageName) -Capture
Write-Host "Built image ID: $newImageId"

if (-not $SkipMigration) {
    Write-Step "Apply idempotent PCCAM product split migration"
    Invoke-DatabaseScript `
        -DbContainer $resolvedDatabaseContainer `
        -DbUser $dbUser `
        -DbPassword $dbPassword `
        -DbName $dbName `
        -LocalScriptPath $migrationPath
}
else {
    Write-Warning "Database migration was skipped by request."
}

$productRows = Get-ProductRows `
    -DbContainer $resolvedDatabaseContainer `
    -DbUser $dbUser `
    -DbPassword $dbPassword `
    -DbName $dbName

$requiredProducts = @("PCCAM", "PCCAM_X86", "PCCAM_X64")
foreach ($requiredProduct in $requiredProducts) {
    if ($productRows -notmatch "(?m)^$([regex]::Escape($requiredProduct))\s+1$") {
        throw "Required active update product was not found after migration: $requiredProduct Rows=$productRows"
    }
}

Write-Host "Verified products:"
Write-Host $productRows

Write-Step "Replace local container with rollback protection"

$allContainers = Get-AllContainerNames
$existingContainerFound = $allContainers -contains $ContainerName
$backupCreated = $false
$oldImageId = ""

if ($existingContainerFound) {
    $oldImageId = Invoke-Docker -Arguments @("inspect", "--format", "{{.Image}}", $ContainerName) -Capture
    $wasRunning = Invoke-Docker -Arguments @("inspect", "--format", "{{.State.Running}}", $ContainerName) -Capture

    if ($wasRunning -eq "true") {
        Invoke-Docker -Arguments @("stop", $ContainerName)
    }

    Invoke-Docker -Arguments @("rename", $ContainerName, $backupContainerName)
    $backupCreated = $true
    Write-Host "Previous container preserved as: $backupContainerName"
}

try {
    $runArguments = @(
        "run", "-d",
        "--name", $ContainerName,
        "--network", $NetworkName,
        "--restart", "unless-stopped",
        "-p", "127.0.0.1:${HostPort}:8080",
        "-v", "${StorageHostPath}:/app/update-storage",
        "-v", "${InternalServiceKeyPath}:/run/secrets/AuthServer__InternalServiceKey:ro",
        "-v", "${ConnectionStringPath}:/run/secrets/ConnectionStrings__DefaultConnection:ro",
        "-e", "ASPNETCORE_ENVIRONMENT=Development",
        "-e", "DOTNET_ENVIRONMENT=Development",
        "-e", "ASPNETCORE_HTTP_PORTS=8080",
        "-e", "AuthServer__BaseUrl=$AuthServerBaseUrl",
        "-e", "UpdateStorage__RootPath=/app/update-storage",
        "-e", "UpdateStorage__PublicBaseUrl=$UpdateStoragePublicBaseUrl",
        "-e", "ForwardedHeaders__KnownProxies__0=127.0.0.1"
    )

    for ($index = 0; $index -lt $AdminWebOrigins.Count; $index++) {
        $runArguments += @("-e", "Cors__AdminWebOrigins__$index=$($AdminWebOrigins[$index])")
    }

    $runArguments += $ImageName
    $newContainerId = Invoke-Docker -Arguments $runArguments -Capture
    Write-Host "Started container ID: $newContainerId"

    Write-Step "Verify local container"

    $readyHealth = Wait-ReadyHealth -Uri "$baseUrl/health/ready"
    $liveHealth = Invoke-RestMethod -Uri "$baseUrl/health/live" -Method Get -TimeoutSec 10

    Invoke-Docker -Arguments @(
        "exec", $ContainerName,
        "sh", "-lc",
        "touch /app/update-storage/.staging/local-deploy-write-test && rm -f /app/update-storage/.staging/local-deploy-write-test"
    )

    Test-CorsPreflight -BaseUrl $baseUrl -Origin $AdminWebOrigins[0]
    $x86Check = Invoke-UpdateCheck -BaseUrl $baseUrl -ProductCode "PCCAM_X86" -Architecture "x86"
    $x64Check = Invoke-UpdateCheck -BaseUrl $baseUrl -ProductCode "PCCAM_X64" -Architecture "x64"
    $legacyCheck = Invoke-UpdateCheck -BaseUrl $baseUrl -ProductCode "PCCAM" -Architecture "x86"

    New-Item -ItemType Directory -Path $reportDirectory -Force | Out-Null
    $reportPath = Join-Path $reportDirectory "local-docker-deploy-$timestamp.json"

    $report = [PSCustomObject]@{
        deployedAt = (Get-Date).ToString("o")
        containerName = $ContainerName
        imageName = $ImageName
        previousImageId = $oldImageId
        newImageId = $newImageId
        backupContainerName = if ($backupCreated) { $backupContainerName } else { $null }
        network = $NetworkName
        baseUrl = $baseUrl
        databaseContainer = $resolvedDatabaseContainer
        products = $productRows -split "`r?`n"
        liveHealth = $liveHealth
        readyHealth = $readyHealth
        updateChecks = [PSCustomObject]@{
            pccamX86 = $x86Check
            pccamX64 = $x64Check
            legacyPccam = $legacyCheck
        }
    }

    $report | ConvertTo-Json -Depth 12 | Set-Content -LiteralPath $reportPath -Encoding UTF8
    Write-Host "Deployment evidence: $reportPath"

    if ($backupCreated -and $RemoveBackupOnSuccess) {
        Invoke-Docker -Arguments @("rm", $backupContainerName)
        $backupCreated = $false
        Write-Host "Removed backup container after successful validation."
    }
}
catch {
    Write-Error "Local deployment verification failed. Rolling back. $($_.Exception.Message)"

    try {
        $currentContainers = Get-AllContainerNames
        if ($currentContainers -contains $ContainerName) {
            Invoke-Docker -Arguments @("rm", "-f", $ContainerName)
        }

        if ($backupCreated) {
            Invoke-Docker -Arguments @("rename", $backupContainerName, $ContainerName)
            Invoke-Docker -Arguments @("start", $ContainerName)
            Write-Host "Rollback completed: $ContainerName"
        }
    }
    catch {
        Write-Error "Automatic rollback failed. $($_.Exception.Message)"
    }

    throw
}

Write-Step "Local Docker deployment completed"
Write-Host "Container: $ContainerName"
Write-Host "Image: $ImageName"
Write-Host "Image ID: $newImageId"
Write-Host "Ready health: $baseUrl/health/ready"
Write-Host "Products: PCCAM, PCCAM_X86, PCCAM_X64"

if ($backupCreated) {
    Write-Host "Rollback container retained: $backupContainerName"
    Write-Host "Remove after validation: docker rm $backupContainerName"
}
