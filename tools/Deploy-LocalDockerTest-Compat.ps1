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

function Normalize-ConnectionStringKey {
    param([Parameter(Mandatory = $true)][string]$Value)

    return (($Value -replace '[\s_\-]', '').ToLowerInvariant())
}

function Get-NormalizedConnectionStringValue {
    param(
        [Parameter(Mandatory = $true)]
        [System.Data.Common.DbConnectionStringBuilder]$Builder,

        [Parameter(Mandatory = $true)]
        [string[]]$Aliases,

        [Parameter(Mandatory = $false)]
        [string]$DefaultValue = ""
    )

    $normalizedAliases = @($Aliases | ForEach-Object {
        Normalize-ConnectionStringKey -Value $_
    })

    foreach ($actualKey in $Builder.Keys) {
        $normalizedActualKey = Normalize-ConnectionStringKey -Value ([string]$actualKey)
        if ($normalizedAliases -contains $normalizedActualKey) {
            return [string]$Builder[[string]$actualKey]
        }
    }

    return $DefaultValue
}

function Remove-ConnectionStringWrapper {
    param([Parameter(Mandatory = $true)][string]$Value)

    $normalized = $Value.Trim([char]0xFEFF).Trim()

    $wrapperPattern = '^\s*(?:ConnectionStrings__DefaultConnection|ConnectionStrings:DefaultConnection|DefaultConnection)\s*=\s*(.+)$'
    if ($normalized -match $wrapperPattern) {
        $normalized = $Matches[1].Trim()
    }

    if ($normalized.Length -ge 2) {
        $first = $normalized[0]
        $last = $normalized[$normalized.Length - 1]
        if (($first -eq '"' -and $last -eq '"') -or
            ($first -eq "'" -and $last -eq "'")) {
            $normalized = $normalized.Substring(1, $normalized.Length - 2).Trim()
        }
    }

    return $normalized
}

if (-not (Test-Path -LiteralPath $ConnectionStringPath -PathType Leaf)) {
    throw "UpdateServer connection string secret was not found: $ConnectionStringPath"
}

$rawConnectionString = Get-Content -LiteralPath $ConnectionStringPath -Raw
$connectionString = Remove-ConnectionStringWrapper -Value $rawConnectionString

if ([string]::IsNullOrWhiteSpace($connectionString)) {
    throw "UpdateServer connection string secret is empty."
}

$builder = New-Object System.Data.Common.DbConnectionStringBuilder
try {
    $builder.ConnectionString = $connectionString
}
catch {
    throw "UpdateServer connection string could not be parsed. Check only its key/value format; the secret value was not logged. $($_.Exception.Message)"
}

$dbServer = Get-NormalizedConnectionStringValue `
    -Builder $builder `
    -Aliases @("Server", "Host", "Data Source", "Address", "Addr", "Network Address")
$dbName = Get-NormalizedConnectionStringValue `
    -Builder $builder `
    -Aliases @("Database", "Initial Catalog", "Database Name", "DatabaseName", "Catalog", "Db")

$allowedLocalServers = @(
    "localhost",
    "127.0.0.1",
    "host.docker.internal",
    "poscam-db",
    "poscam-db-new"
)

if ([string]::IsNullOrWhiteSpace($dbName)) {
    if ($allowedLocalServers -notcontains $dbServer.ToLowerInvariant()) {
        throw "The database name is missing and the server is not an approved local target. Server=$dbServer"
    }

    $builder["Database"] = "poscam_update"
    $dbName = "poscam_update"
    Write-Host "Database key was missing. Added Database=poscam_update to a temporary local-only connection string."
}
elseif ($dbName -ne "poscam_update") {
    throw "This deployment is restricted to the local poscam_update database. Actual=$dbName"
}

$scriptDirectory = Split-Path -Parent $MyInvocation.MyCommand.Path
$deploymentScript = Join-Path $scriptDirectory "Deploy-LocalDockerTest.ps1"

if (-not (Test-Path -LiteralPath $deploymentScript -PathType Leaf)) {
    throw "Base local deployment script was not found: $deploymentScript"
}

$tempConnectionStringPath = Join-Path `
    ([System.IO.Path]::GetTempPath()) `
    ("poscam-update-connection-{0}.txt" -f [Guid]::NewGuid().ToString("N"))

try {
    $utf8WithoutBom = New-Object System.Text.UTF8Encoding($false)
    [System.IO.File]::WriteAllText(
        $tempConnectionStringPath,
        $builder.ConnectionString,
        $utf8WithoutBom)

    $deploymentParameters = @{
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
        DatabaseContainer = $DatabaseContainer
        SkipMigration = $SkipMigration
        PullBaseImages = $PullBaseImages
        RemoveBackupOnSuccess = $RemoveBackupOnSuccess
    }

    & $deploymentScript @deploymentParameters
}
finally {
    if (Test-Path -LiteralPath $tempConnectionStringPath -PathType Leaf) {
        Remove-Item -LiteralPath $tempConnectionStringPath -Force
    }
}
