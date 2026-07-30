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
    [AllowEmptyString()]
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
        [AllowEmptyString()]
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

function Invoke-DockerCapture {
    param(
        [Parameter(Mandatory = $true)]
        [string[]]$Arguments,

        [Parameter(Mandatory = $false)]
        [switch]$Sensitive
    )

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

function Get-RunningContainerNames {
    $raw = Invoke-DockerCapture -Arguments @("ps", "--format", "{{.Names}}")

    if ([string]::IsNullOrWhiteSpace($raw)) {
        return @()
    }

    return @($raw -split "`r?`n" | Where-Object {
        -not [string]::IsNullOrWhiteSpace($_)
    })
}

function Get-ContainerEnvironmentMap {
    param([Parameter(Mandatory = $true)][string]$Container)

    $raw = Invoke-DockerCapture `
        -Arguments @("inspect", "--format", "{{range .Config.Env}}{{println .}}{{end}}", $Container) `
        -Sensitive

    $result = @{}

    foreach ($line in @($raw -split "`r?`n")) {
        if ([string]::IsNullOrWhiteSpace($line)) {
            continue
        }

        $separatorIndex = $line.IndexOf('=')
        if ($separatorIndex -le 0) {
            continue
        }

        $key = $line.Substring(0, $separatorIndex)
        $value = $line.Substring($separatorIndex + 1)
        $result[$key] = $value
    }

    return $result
}

function Get-EnvironmentValue {
    param(
        [Parameter(Mandatory = $true)][hashtable]$Environment,
        [Parameter(Mandatory = $true)][string[]]$Keys,
        [Parameter(Mandatory = $false)][AllowEmptyString()][string]$DefaultValue = ""
    )

    foreach ($key in $Keys) {
        if ($Environment.ContainsKey($key) -and
            -not [string]::IsNullOrWhiteSpace([string]$Environment[$key])) {
            return [string]$Environment[$key]
        }
    }

    return $DefaultValue
}

function Get-ContainerFileValue {
    param(
        [Parameter(Mandatory = $true)][string]$Container,
        [Parameter(Mandatory = $false)][AllowEmptyString()][string]$Path = ""
    )

    if ([string]::IsNullOrWhiteSpace($Path)) {
        return ""
    }

    try {
        return Invoke-DockerCapture `
            -Arguments @("exec", $Container, "sh", "-lc", "cat -- '$Path'") `
            -Sensitive
    }
    catch {
        return ""
    }
}

function Get-EnvironmentOrFileValue {
    param(
        [Parameter(Mandatory = $true)][string]$Container,
        [Parameter(Mandatory = $true)][hashtable]$Environment,
        [Parameter(Mandatory = $true)][string[]]$ValueKeys,
        [Parameter(Mandatory = $true)][string[]]$FileKeys
    )

    $directValue = Get-EnvironmentValue -Environment $Environment -Keys $ValueKeys
    if (-not [string]::IsNullOrWhiteSpace($directValue)) {
        return $directValue
    }

    $filePath = Get-EnvironmentValue -Environment $Environment -Keys $FileKeys
    return Get-ContainerFileValue -Container $Container -Path $filePath
}

function Resolve-LocalDatabaseContainer {
    param(
        [Parameter(Mandatory = $false)]
        [AllowEmptyString()]
        [string]$ConfiguredContainer = "",

        [Parameter(Mandatory = $false)]
        [AllowEmptyString()]
        [string]$ParsedServer = ""
    )

    $allowedContainers = @("poscam-db-new", "poscam-db")
    $running = Get-RunningContainerNames

    if (-not [string]::IsNullOrWhiteSpace($ConfiguredContainer)) {
        if ($allowedContainers -notcontains $ConfiguredContainer) {
            throw "Only local database containers are allowed: $($allowedContainers -join ', ')"
        }

        if ($running -notcontains $ConfiguredContainer) {
            throw "Configured local database container is not running: $ConfiguredContainer"
        }

        return $ConfiguredContainer
    }

    if (-not [string]::IsNullOrWhiteSpace($ParsedServer) -and
        $allowedContainers -contains $ParsedServer -and
        $running -contains $ParsedServer) {
        return $ParsedServer
    }

    $candidates = @($allowedContainers | Where-Object { $running -contains $_ })

    if ($candidates.Count -eq 1) {
        return $candidates[0]
    }

    if ($candidates.Count -gt 1) {
        throw "Multiple local database containers are running. Specify one explicitly with -DatabaseContainer. Candidates=$($candidates -join ', ')"
    }

    throw "No approved local MariaDB container is running. Expected=poscam-db-new or poscam-db"
}

if (-not (Test-Path -LiteralPath $ConnectionStringPath -PathType Leaf)) {
    throw "UpdateServer connection string secret was not found: $ConnectionStringPath"
}

if ($null -eq (Get-Command docker -ErrorAction SilentlyContinue)) {
    throw "Docker CLI was not found. Start Docker Desktop and ensure docker.exe is on PATH."
}

$rawConnectionString = Get-Content -LiteralPath $ConnectionStringPath -Raw
$connectionString = Remove-ConnectionStringWrapper -Value $rawConnectionString

if ([string]::IsNullOrWhiteSpace($connectionString)) {
    throw "UpdateServer connection string secret is empty."
}

$parsedBuilder = New-Object System.Data.Common.DbConnectionStringBuilder
try {
    $parsedBuilder.ConnectionString = $connectionString
}
catch {
    Write-Warning "The existing secret is not a standard ADO.NET connection string. Local Docker container settings will be used without logging the secret."
    $parsedBuilder = New-Object System.Data.Common.DbConnectionStringBuilder
}

$parsedServer = Get-NormalizedConnectionStringValue `
    -Builder $parsedBuilder `
    -Aliases @("Server", "Host", "Data Source", "Address", "Addr", "Network Address")
$parsedDatabase = Get-NormalizedConnectionStringValue `
    -Builder $parsedBuilder `
    -Aliases @("Database", "Initial Catalog", "Database Name", "DatabaseName", "Catalog", "Db")
$parsedUser = Get-NormalizedConnectionStringValue `
    -Builder $parsedBuilder `
    -Aliases @("User ID", "Uid", "Username", "User")
$parsedPassword = Get-NormalizedConnectionStringValue `
    -Builder $parsedBuilder `
    -Aliases @("Password", "Pwd")

$resolvedDatabaseContainer = Resolve-LocalDatabaseContainer `
    -ConfiguredContainer $DatabaseContainer `
    -ParsedServer $parsedServer

$databaseEnvironment = Get-ContainerEnvironmentMap -Container $resolvedDatabaseContainer
$containerDatabase = Get-EnvironmentValue `
    -Environment $databaseEnvironment `
    -Keys @("MARIADB_DATABASE", "MYSQL_DATABASE")

$dbName = $parsedDatabase
if ([string]::IsNullOrWhiteSpace($dbName)) {
    if (-not [string]::IsNullOrWhiteSpace($containerDatabase)) {
        $dbName = $containerDatabase
    }
    else {
        $dbName = "poscam_update"
    }
}

if ($dbName -ne "poscam_update") {
    throw "This deployment is restricted to the local poscam_update database. Actual=$dbName Container=$resolvedDatabaseContainer"
}

$dbUser = $parsedUser
$dbPassword = $parsedPassword

if ([string]::IsNullOrWhiteSpace($dbUser)) {
    $dbUser = Get-EnvironmentValue `
        -Environment $databaseEnvironment `
        -Keys @("MARIADB_USER", "MYSQL_USER")
}

if ([string]::IsNullOrWhiteSpace($dbPassword)) {
    $dbPassword = Get-EnvironmentOrFileValue `
        -Container $resolvedDatabaseContainer `
        -Environment $databaseEnvironment `
        -ValueKeys @("MARIADB_PASSWORD", "MYSQL_PASSWORD") `
        -FileKeys @("MARIADB_PASSWORD_FILE", "MYSQL_PASSWORD_FILE")
}

if ([string]::IsNullOrWhiteSpace($dbUser) -or
    [string]::IsNullOrWhiteSpace($dbPassword)) {
    $rootPassword = Get-EnvironmentOrFileValue `
        -Container $resolvedDatabaseContainer `
        -Environment $databaseEnvironment `
        -ValueKeys @("MARIADB_ROOT_PASSWORD", "MYSQL_ROOT_PASSWORD") `
        -FileKeys @("MARIADB_ROOT_PASSWORD_FILE", "MYSQL_ROOT_PASSWORD_FILE")

    if (-not [string]::IsNullOrWhiteSpace($rootPassword)) {
        $dbUser = "root"
        $dbPassword = $rootPassword
    }
}

if ([string]::IsNullOrWhiteSpace($dbUser) -or
    [string]::IsNullOrWhiteSpace($dbPassword)) {
    throw "Database credentials could not be resolved from the existing secret or the selected local database container. Container=$resolvedDatabaseContainer"
}

$normalizedBuilder = New-Object System.Data.Common.DbConnectionStringBuilder
$normalizedBuilder["Server"] = $resolvedDatabaseContainer
$normalizedBuilder["Port"] = "3306"
$normalizedBuilder["Database"] = $dbName
$normalizedBuilder["User ID"] = $dbUser
$normalizedBuilder["Password"] = $dbPassword
$normalizedBuilder["SslMode"] = "None"
$normalizedBuilder["AllowPublicKeyRetrieval"] = "True"

Write-Host "Selected local database container: $resolvedDatabaseContainer"
Write-Host "Normalized database target: $dbName"
Write-Host "Database credentials resolved without logging secret values."

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
        $normalizedBuilder.ConnectionString,
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
        DatabaseContainer = $resolvedDatabaseContainer
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
