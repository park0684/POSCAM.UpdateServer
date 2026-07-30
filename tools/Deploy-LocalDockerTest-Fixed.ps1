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

$oldAssignment = '$connectionBuilder.ConnectionString = $connectionString'
$newAssignment = '$connectionBuilder.set_ConnectionString($connectionString)'

function Get-TestConnectionStringValue {
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

if ($ParserSelfTest) {
    $builder = New-Object System.Data.Common.DbConnectionStringBuilder
    $builder.set_ConnectionString(
        "server=poscam-db-new;port=3306;database=poscam_update;user id=test_user;password=test_password;")

    $server = Get-TestConnectionStringValue -Builder $builder -Keys @("Server", "Host", "Data Source")
    $database = Get-TestConnectionStringValue -Builder $builder -Keys @("Database", "Initial Catalog")
    $user = Get-TestConnectionStringValue -Builder $builder -Keys @("User ID", "Uid", "Username", "User")

    if ($builder.Keys.Count -lt 5) {
        throw "Connection-string setter self-test did not parse individual keys. KeysCount=$($builder.Keys.Count)"
    }

    if ($server -ne "poscam-db-new") {
        throw "Connection-string setter self-test failed for Server. Actual=$server"
    }

    if ($database -ne "poscam_update") {
        throw "Connection-string setter self-test failed for Database. Actual=$database"
    }

    if ($user -ne "test_user") {
        throw "Connection-string setter self-test failed for User ID. Actual=$user"
    }

    Write-Host "Explicit DbConnectionStringBuilder setter self-test passed."
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
$assignmentPatched = $false

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

    if ($baseScriptContent.Contains($oldAssignment)) {
        $patchedContent = $baseScriptContent.Replace($oldAssignment, $newAssignment)
        [System.IO.File]::WriteAllText($baseScriptPath, $patchedContent, $utf8)
        $assignmentPatched = $true
        Write-Host "Applied explicit DbConnectionStringBuilder setter for this deployment run."
    }
    elseif ($baseScriptContent.Contains($newAssignment)) {
        Write-Host "Base deployment script already uses the explicit connection-string setter."
    }
    else {
        throw "Expected DbConnectionStringBuilder assignment was not found in Deploy-LocalDockerTest.ps1."
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
    if ($assignmentPatched -and $null -ne $originalBytes) {
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
