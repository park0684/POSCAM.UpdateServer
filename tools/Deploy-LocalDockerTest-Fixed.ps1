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
    [switch]$RemoveBackupOnSuccess,

    [Parameter(Mandatory = $false)]
    [switch]$ParserSelfTest
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

$oldParser = @'
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
'@

$newParser = @'
function Normalize-ConnectionStringKey {
    param([Parameter(Mandatory = $true)][string]$Value)

    return (($Value -replace '[\s_\-]', '').ToLowerInvariant())
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

    $normalizedKeys = @($Keys | ForEach-Object {
        Normalize-ConnectionStringKey -Value $_
    })

    foreach ($actualKey in $Builder.Keys) {
        $normalizedActualKey = Normalize-ConnectionStringKey -Value ([string]$actualKey)
        if ($normalizedKeys -contains $normalizedActualKey) {
            return [string]$Builder[[string]$actualKey]
        }
    }

    return $DefaultValue
}
'@

if ($ParserSelfTest) {
    Invoke-Expression $newParser

    $builder = New-Object System.Data.Common.DbConnectionStringBuilder
    $builder.ConnectionString = "server=poscam-db-new;port=3306;database=poscam_update;user id=test_user;password=test_password;"

    $server = Get-ConnectionStringValue -Builder $builder -Keys @("Server", "Host", "Data Source")
    $database = Get-ConnectionStringValue -Builder $builder -Keys @("Database", "Initial Catalog")
    $user = Get-ConnectionStringValue -Builder $builder -Keys @("User ID", "Uid", "Username", "User")

    if ($server -ne "poscam-db-new") {
        throw "Parser self-test failed for Server. Actual=$server"
    }

    if ($database -ne "poscam_update") {
        throw "Parser self-test failed for Database. Actual=$database"
    }

    if ($user -ne "test_user") {
        throw "Parser self-test failed for User ID. Actual=$user"
    }

    Write-Host "Case-insensitive local connection-string parser self-test passed."
    return
}

if ($ContainerName -ne "poscam-update-server-local") {
    throw "This launcher is restricted to poscam-update-server-local."
}

if ($ImageName -ne "poscam-update-server:local") {
    throw "This launcher is restricted to poscam-update-server:local."
}

if ($HostPort -ne 8083) {
    throw "This launcher is restricted to local host port 8083."
}

$scriptDirectory = Split-Path -Parent $MyInvocation.MyCommand.Path
$baseScriptPath = Join-Path $scriptDirectory "Deploy-LocalDockerTest.ps1"
$selectedDbScriptPath = Join-Path $scriptDirectory "Deploy-LocalDockerTest-SelectedDb.ps1"
$lockPath = Join-Path $scriptDirectory ".poscam-local-docker-parser.lock"

foreach ($requiredPath in @($baseScriptPath, $selectedDbScriptPath)) {
    if (-not (Test-Path -LiteralPath $requiredPath -PathType Leaf)) {
        throw "Required local deployment script was not found: $requiredPath"
    }
}

$lockStream = $null
$originalBytes = $null
$parserPatched = $false

try {
    try {
        $lockStream = New-Object System.IO.FileStream(
            $lockPath,
            [System.IO.FileMode]::CreateNew,
            [System.IO.FileAccess]::Write,
            [System.IO.FileShare]::None)
    }
    catch {
        throw "Another local Docker parser deployment is already running. Lock=$lockPath"
    }

    $originalBytes = [System.IO.File]::ReadAllBytes($baseScriptPath)
    $utf8 = New-Object System.Text.UTF8Encoding($false)
    $baseScriptContent = $utf8.GetString($originalBytes)

    if ($baseScriptContent.Contains($oldParser)) {
        $patchedContent = $baseScriptContent.Replace($oldParser, $newParser)
        [System.IO.File]::WriteAllText($baseScriptPath, $patchedContent, $utf8)
        $parserPatched = $true
        Write-Host "Applied case-insensitive connection-string parser for this deployment run."
    }
    elseif ($baseScriptContent.Contains("function Normalize-ConnectionStringKey")) {
        Write-Host "Base deployment script already contains the case-insensitive parser."
    }
    else {
        throw "Expected connection-string parser block was not found in Deploy-LocalDockerTest.ps1."
    }

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
        SkipMigration = $SkipMigration
        PullBaseImages = $PullBaseImages
        RemoveBackupOnSuccess = $RemoveBackupOnSuccess
    }

    & $selectedDbScriptPath @parameters
}
finally {
    if ($parserPatched -and $null -ne $originalBytes) {
        [System.IO.File]::WriteAllBytes($baseScriptPath, $originalBytes)
        Write-Host "Restored the original Deploy-LocalDockerTest.ps1 bytes."
    }

    if ($null -ne $lockStream) {
        $lockStream.Dispose()
    }

    if (Test-Path -LiteralPath $lockPath -PathType Leaf) {
        Remove-Item -LiteralPath $lockPath -Force
    }
}
