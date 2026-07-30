[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [ValidateSet("poscam-db-new", "poscam-db")]
    [string]$DatabaseContainer,

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
    [switch]$SkipMigration,

    [Parameter(Mandatory = $false)]
    [switch]$PullBaseImages,

    [Parameter(Mandatory = $false)]
    [switch]$RemoveBackupOnSuccess
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

if ($ContainerName -ne "poscam-update-server-local") {
    throw "This launcher is restricted to poscam-update-server-local."
}

if ($ImageName -ne "poscam-update-server:local") {
    throw "This launcher is restricted to poscam-update-server:local."
}

if ($HostPort -ne 8083) {
    throw "This launcher is restricted to local host port 8083."
}

if ($null -eq (Get-Command docker -ErrorAction SilentlyContinue)) {
    throw "Docker CLI was not found. Start Docker Desktop and ensure docker.exe is on PATH."
}

$runningContainers = @(& docker ps --format "{{.Names}}" 2>&1)
if ($LASTEXITCODE -ne 0) {
    throw "Unable to read running Docker containers."
}

if ($runningContainers -notcontains $DatabaseContainer) {
    throw "Selected local database container is not running: $DatabaseContainer"
}

$scriptDirectory = Split-Path -Parent $MyInvocation.MyCommand.Path
$compatScript = Join-Path $scriptDirectory "Deploy-LocalDockerTest-Compat.ps1"

if (-not (Test-Path -LiteralPath $compatScript -PathType Leaf)) {
    throw "Compatibility deployment script was not found: $compatScript"
}

$tempConnectionStringPath = Join-Path `
    ([System.IO.Path]::GetTempPath()) `
    ("poscam-update-selected-db-{0}.txt" -f [Guid]::NewGuid().ToString("N"))

try {
    $bootstrapConnectionString = "Server=$DatabaseContainer;Port=3306;Database=poscam_update;SslMode=None;"
    $utf8WithoutBom = New-Object System.Text.UTF8Encoding($false)
    [System.IO.File]::WriteAllText(
        $tempConnectionStringPath,
        $bootstrapConnectionString,
        $utf8WithoutBom)

    Write-Host "Selected local database container: $DatabaseContainer"
    Write-Host "Bootstrap connection string contains no database credentials."

    $parameters = @{
        DatabaseContainer = $DatabaseContainer
        ContainerName = $ContainerName
        ImageName = $ImageName
        NetworkName = $NetworkName
        HostPort = $HostPort
        AuthServerBaseUrl = $AuthServerBaseUrl
        UpdateStoragePublicBaseUrl = $UpdateStoragePublicBaseUrl
        AdminWebOrigins = $AdminWebOrigins
        StorageHostPath = $StorageHostPath
        InternalServiceKeyPath = $InternalServiceKeyPath
        ConnectionStringPath = $tempConnectionStringPath
        SkipMigration = $SkipMigration
        PullBaseImages = $PullBaseImages
        RemoveBackupOnSuccess = $RemoveBackupOnSuccess
    }

    & $compatScript @parameters
}
finally {
    if (Test-Path -LiteralPath $tempConnectionStringPath -PathType Leaf) {
        Remove-Item -LiteralPath $tempConnectionStringPath -Force
    }
}
