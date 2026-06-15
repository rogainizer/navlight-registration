param(
    [SecureString]$RootPassword = (ConvertTo-SecureString "root" -AsPlainText -Force),
    [int]$Port = 3306,
    [string]$SchemaPath
)

$ErrorActionPreference = "Stop"

$databaseRoot = $PSScriptRoot
$mysqlRoot = Join-Path $databaseRoot "mysql"
$binRoot = Join-Path $mysqlRoot "bin"
$dataRoot = Join-Path $databaseRoot "data"

if ([string]::IsNullOrWhiteSpace($SchemaPath)) {
    $localSchemaPath = Join-Path $databaseRoot "schema.sql"
    $repoSchemaPath = Join-Path $databaseRoot "..\..\Navlight-Registration\database\schema.sql"
    $SchemaPath = if (Test-Path -LiteralPath $localSchemaPath) { $localSchemaPath } else { $repoSchemaPath }
}

$mysqldPath = Join-Path $binRoot "mysqld.exe"
$mysqlPath = Join-Path $binRoot "mysql.exe"
$mysqlAdminPath = Join-Path $binRoot "mysqladmin.exe"

function Assert-FileExists {
    param(
        [string]$Path,
        [string]$Description
    )

    if (-not (Test-Path -LiteralPath $Path)) {
        throw "$Description was not found at '$Path'."
    }
}

function Wait-ForPort {
    param(
        [int]$PortNumber,
        [int]$TimeoutSeconds = 30
    )

    $deadline = (Get-Date).AddSeconds($TimeoutSeconds)
    do {
        $connection = Get-NetTCPConnection -LocalPort $PortNumber -State Listen -ErrorAction SilentlyContinue
        if ($connection) {
            return
        }

        Start-Sleep -Milliseconds 500
    }
    while ((Get-Date) -lt $deadline)

    throw "MySQL did not start listening on port $PortNumber within $TimeoutSeconds seconds."
}

function Get-PlainText {
    param(
        [SecureString]$Value
    )

    $credential = New-Object System.Management.Automation.PSCredential "unused", $Value
    return $credential.GetNetworkCredential().Password
}

function Get-LogTail {
    param(
        [string]$Path,
        [int]$LineCount = 40
    )

    if (-not (Test-Path -LiteralPath $Path)) {
        return $null
    }

    $content = Get-Content -LiteralPath $Path -Tail $LineCount -ErrorAction SilentlyContinue
    if (-not $content) {
        return $null
    }

    return ($content -join [Environment]::NewLine)
}

Assert-FileExists -Path $mysqldPath -Description "mysqld.exe"
Assert-FileExists -Path $mysqlPath -Description "mysql.exe"
Assert-FileExists -Path $mysqlAdminPath -Description "mysqladmin.exe"
Assert-FileExists -Path $SchemaPath -Description "schema.sql"

if (-not (Test-Path -LiteralPath $dataRoot)) {
    New-Item -ItemType Directory -Path $dataRoot | Out-Null
}

$existingListener = Get-NetTCPConnection -LocalPort $Port -State Listen -ErrorAction SilentlyContinue
if ($existingListener) {
    throw "Port $Port is already in use by PID $($existingListener.OwningProcess). Stop that process before running this script."
}

$dataFiles = Get-ChildItem -Path $dataRoot -Force -ErrorAction SilentlyContinue
if (-not $dataFiles) {
    Write-Host "Initializing MySQL data directory..."
    & $mysqldPath --initialize-insecure --basedir="$mysqlRoot" --datadir="$dataRoot"
    if ($LASTEXITCODE -ne 0) {
        throw "MySQL data directory initialization failed with exit code $LASTEXITCODE."
    }
}
else {
    Write-Host "Data directory already contains files. Skipping initialization."
}

Write-Host "Starting MySQL temporarily on port $Port..."
$serverStdOutPath = Join-Path $dataRoot "mysqld-bootstrap.stdout.log"
$serverStdErrPath = Join-Path $dataRoot "mysqld-bootstrap.stderr.log"
$null = New-Item -ItemType File -Path $serverStdOutPath -Force
$null = New-Item -ItemType File -Path $serverStdErrPath -Force

$serverProcess = Start-Process -FilePath $mysqldPath -ArgumentList @(
    "--basedir=$mysqlRoot",
    "--datadir=$dataRoot",
    "--port=$Port"
) -RedirectStandardOutput $serverStdOutPath -RedirectStandardError $serverStdErrPath -PassThru

Write-Host "MySQL bootstrap logs:"
Write-Host "  STDOUT: $serverStdOutPath"
Write-Host "  STDERR: $serverStdErrPath"

$plainRootPassword = Get-PlainText -Value $RootPassword

try {
    try {
        Wait-ForPort -PortNumber $Port
    }
    catch {
        $serverExitCode = if ($serverProcess.HasExited) { $serverProcess.ExitCode } else { $null }
        $stderrTail = Get-LogTail -Path $serverStdErrPath
        $stdoutTail = Get-LogTail -Path $serverStdOutPath

        $details = @(
            $_.Exception.Message,
            "Bootstrap STDOUT log: $serverStdOutPath",
            "Bootstrap STDERR log: $serverStdErrPath"
        )

        if ($serverExitCode -ne $null) {
            $details += "mysqld exited early with code $serverExitCode."
        }

        if (-not [string]::IsNullOrWhiteSpace($stderrTail)) {
            $details += "Last STDERR lines:"
            $details += $stderrTail
        }

        if (-not [string]::IsNullOrWhiteSpace($stdoutTail)) {
            $details += "Last STDOUT lines:"
            $details += $stdoutTail
        }

        throw ($details -join [Environment]::NewLine)
    }

    $escapedRootPassword = $plainRootPassword.Replace("'", "''")
    $bootstrapSql = Join-Path $env:TEMP ("navlight-bootstrap-" + [Guid]::NewGuid().ToString("N") + ".sql")
    @"
ALTER USER 'root'@'localhost' IDENTIFIED BY '$escapedRootPassword';
FLUSH PRIVILEGES;
"@ | Set-Content -Path $bootstrapSql -Encoding ASCII

    try {
        Write-Host "Setting MySQL root password..."
        Get-Content -Raw -Path $bootstrapSql | & $mysqlPath --protocol=TCP --port=$Port -u root
        if ($LASTEXITCODE -ne 0) {
            throw "Setting the MySQL root password failed with exit code $LASTEXITCODE."
        }

        Write-Host "Loading registration schema..."
        Get-Content -Raw -Path $SchemaPath | & $mysqlPath --protocol=TCP --port=$Port -u root "--password=$plainRootPassword"
        if ($LASTEXITCODE -ne 0) {
            throw "Loading the registration schema failed with exit code $LASTEXITCODE."
        }
    }
    finally {
        if (Test-Path -LiteralPath $bootstrapSql) {
            Remove-Item -LiteralPath $bootstrapSql -Force
        }
    }

    Write-Host "MySQL initialized and schema loaded successfully."
}
finally {
    if ($serverProcess -and -not $serverProcess.HasExited) {
        Write-Host "Shutting down temporary MySQL process..."
        & $mysqlAdminPath --protocol=TCP --port=$Port -u root "--password=$plainRootPassword" shutdown | Out-Null
        $serverProcess.WaitForExit(10000) | Out-Null

        if (-not $serverProcess.HasExited) {
            Stop-Process -Id $serverProcess.Id -Force
        }
    }
}
