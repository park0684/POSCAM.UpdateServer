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
    [switch]$SkipBuild,

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
        [switch]$Capture
    )

    if ($Capture) {
        $output = & docker @Arguments 2>&1
        $exitCode = $LASTEXITCODE

        if ($exitCode -ne 0) {
            throw "Docker command failed. ExitCode=$exitCode Arguments=$($Arguments -join ' ') Output=$($output -join [Environment]::NewLine)"
        }

        return (($output | ForEach-Object { $_.ToString() }) -join [Environment]::NewLine).Trim()
    }

    & docker @Arguments
    $exitCode = $LASTEXITCODE

    if ($exitCode -ne 0) {
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

function Get-AllContainerNames {
    $raw = Invoke-Docker -Arguments @("ps", "-a", "--format", "{{.Names}}") -Capture

    if ([string]::IsNullOrWhiteSpace($raw)) {
        return @()
    }

    return @($raw -split "`r?`n" | Where-Object { -not [string]::IsNullOrWhiteSpace($_) })
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
            return Invoke-RestMethod -Uri $Uri -Method Get -TimeoutSec 5
        }
        catch {
            $lastError = $_
            Start-Sleep -Seconds $DelaySeconds
        }
    }

    throw "Ready health check failed: $Uri LastError=$($lastError.Exception.Message)"
}

function Test-LocalHttpUrl {
    param(
        [Parameter(Mandatory = $true)][string]$Value,
        [Parameter(Mandatory = $true)][string]$Description,
        [Parameter(Mandatory = $false)][switch]$AllowDockerHost
    )

    $allowedPrefixes = @(
        "http://127.0.0.1:",
        "http://localhost:"
    )

    if ($AllowDockerHost) {
        $allowedPrefixes += "http://host.docker.internal:"
    }

    foreach ($prefix in $allowedPrefixes) {
        if ($Value.StartsWith($prefix, [System.StringComparison]::OrdinalIgnoreCase)) {
            return
        }
    }

    throw "$Description must use an approved local HTTP address. Actual=$Value"
}

$scriptDirectory = Split-Path -Parent $MyInvocation.MyCommand.Path
$repositoryRoot = [System.IO.Path]::GetFullPath((Join-Path $scriptDirectory ".."))
$dockerfilePath = Join-Path $repositoryRoot "Dockerfile"
$reportDirectory = Join-Path $repositoryRoot "artifacts\local-docker-deploy"
$timestamp = Get-Date -Format "yyyyMMdd-HHmmss"
$backupContainerName = "$ContainerName-backup-$timestamp"
$baseUrl = "http://127.0.0.1:$HostPort"

Write-Step "Validate local image-only deployment target"

if ($ContainerName -ne "poscam-update-server-local") {
    throw "This script is restricted to the local test container: poscam-update-server-local"
}

if ($ImageName -ne "poscam-update-server:local") {
    throw "This script is restricted to the local test image: poscam-update-server:local"
}

if ($HostPort -ne 8083) {
    throw "This script is restricted to the local test host port 8083."
}

if ($NetworkName -ne "poscam-internal") {
    throw "This script is restricted to the local Docker network: poscam-internal"
}

Test-LocalHttpUrl -Value $AuthServerBaseUrl -Description "AuthServerBaseUrl" -AllowDockerHost
Test-LocalHttpUrl -Value $UpdateStoragePublicBaseUrl -Description "UpdateStoragePublicBaseUrl"

if ($AdminWebOrigins.Count -lt 1) {
    throw "At least one local AdminWeb CORS origin is required."
}

foreach ($origin in $AdminWebOrigins) {
    Test-LocalHttpUrl -Value $origin -Description "AdminWeb origin"
}

if ($null -eq (Get-Command docker -ErrorAction SilentlyContinue)) {
    throw "Docker CLI was not found. Start Docker Desktop and ensure docker.exe is on PATH."
}

Assert-File -Path $dockerfilePath -Description "UpdateServer Dockerfile"
Assert-File -Path $InternalServiceKeyPath -Description "Internal service key secret"
Assert-File -Path $ConnectionStringPath -Description "UpdateServer connection string secret"
Assert-Directory -Path $StorageHostPath -Description "Update storage"

$networkNames = Invoke-Docker -Arguments @("network", "ls", "--format", "{{.Name}}") -Capture
if (@($networkNames -split "`r?`n") -notcontains $NetworkName) {
    throw "Required Docker network was not found: $NetworkName"
}

if (-not $SkipBuild) {
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
}
else {
    Write-Warning "Image build was skipped. The existing local image will be deployed."
}

$newImageId = Invoke-Docker -Arguments @("image", "inspect", "--format", "{{.Id}}", $ImageName) -Capture
Write-Host "Image ID: $newImageId"

Write-Step "Replace local container with rollback protection"

$allContainers = Get-AllContainerNames
$existingContainerFound = $allContainers -contains $ContainerName
$backupCreated = $false
$oldImageId = ""
$wasRunning = $false

if ($allContainers -contains $backupContainerName) {
    throw "Backup container name already exists: $backupContainerName"
}

if ($existingContainerFound) {
    $oldImageId = Invoke-Docker -Arguments @("inspect", "--format", "{{.Image}}", $ContainerName) -Capture
    $wasRunningText = Invoke-Docker -Arguments @("inspect", "--format", "{{.State.Running}}", $ContainerName) -Capture
    $wasRunning = $wasRunningText -eq "true"

    if ($wasRunning) {
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
    $runningState = Invoke-Docker -Arguments @("inspect", "--format", "{{.State.Running}}", $ContainerName) -Capture

    if ($runningState -ne "true") {
        throw "The new local UpdateServer container is not running."
    }

    Invoke-Docker -Arguments @(
        "exec", $ContainerName,
        "sh", "-lc",
        "touch /app/update-storage/.staging/local-image-deploy-write-test && rm -f /app/update-storage/.staging/local-image-deploy-write-test"
    )

    New-Item -ItemType Directory -Path $reportDirectory -Force | Out-Null
    $reportPath = Join-Path $reportDirectory "local-image-deploy-$timestamp.json"

    $report = [PSCustomObject]@{
        deployedAt = (Get-Date).ToString("o")
        deploymentType = "image-only"
        containerName = $ContainerName
        imageName = $ImageName
        previousImageId = $oldImageId
        newImageId = $newImageId
        backupContainerName = if ($backupCreated) { $backupContainerName } else { $null }
        network = $NetworkName
        baseUrl = $baseUrl
        liveHealth = $liveHealth
        readyHealth = $readyHealth
    }

    $report | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $reportPath -Encoding UTF8
    Write-Host "Deployment evidence: $reportPath"

    if ($backupCreated -and $RemoveBackupOnSuccess) {
        Invoke-Docker -Arguments @("rm", $backupContainerName)
        $backupCreated = $false
        Write-Host "Removed backup container after successful validation."
    }
}
catch {
    $deploymentError = $_
    Write-Warning "Local image deployment verification failed. Rolling back. $($deploymentError.Exception.Message)"

    try {
        $currentContainers = Get-AllContainerNames
        if ($currentContainers -contains $ContainerName) {
            Invoke-Docker -Arguments @("rm", "-f", $ContainerName)
        }

        if ($backupCreated) {
            Invoke-Docker -Arguments @("rename", $backupContainerName, $ContainerName)
            if ($wasRunning) {
                Invoke-Docker -Arguments @("start", $ContainerName)
            }
            Write-Host "Rollback completed: $ContainerName"
        }
    }
    catch {
        Write-Warning "Automatic rollback failed. $($_.Exception.Message)"
    }

    throw $deploymentError
}

Write-Step "Local Docker image deployment completed"
Write-Host "Container: $ContainerName"
Write-Host "Image: $ImageName"
Write-Host "Image ID: $newImageId"
Write-Host "Ready health: $baseUrl/health/ready"
Write-Host "Database seed: not executed"

if ($backupCreated) {
    Write-Host "Rollback container retained: $backupContainerName"
    Write-Host "Remove after validation: docker rm $backupContainerName"
}
