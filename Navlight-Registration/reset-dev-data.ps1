param(
    [string]$MySqlRoot = "C:\Users\dougl\Documents\Rogaine\Navlight\Navlight-Host\Database\mysql",
    [SecureString]$RootPassword = (ConvertTo-SecureString "root" -AsPlainText -Force)
)

$ErrorActionPreference = "Stop"

$projectRoot = $PSScriptRoot
$schemaPath = Join-Path $projectRoot "database\schema.sql"
$testDataPath = Join-Path $projectRoot "database\test-data.sql"
$mysqlExePath = Join-Path $MySqlRoot "bin\mysql.exe"

if (-not (Test-Path -LiteralPath $mysqlExePath)) {
    throw "mysql.exe was not found at '$mysqlExePath'."
}

if (-not (Test-Path -LiteralPath $schemaPath)) {
    throw "schema.sql was not found at '$schemaPath'."
}

if (-not (Test-Path -LiteralPath $testDataPath)) {
    throw "test-data.sql was not found at '$testDataPath'."
}

$credential = New-Object System.Management.Automation.PSCredential "unused", $RootPassword
$plainRootPassword = $credential.GetNetworkCredential().Password
$dropDatabaseSql = "DROP DATABASE IF EXISTS navlight_registration;"

$previousMySqlPassword = $env:MYSQL_PWD
$env:MYSQL_PWD = $plainRootPassword

try {
    Write-Host "Dropping existing development database..."
    $dropDatabaseSql | & $mysqlExePath -u root
    if ($LASTEXITCODE -ne 0) {
        throw "Dropping navlight_registration failed with exit code $LASTEXITCODE."
    }

    Write-Host "Loading schema..."
    Get-Content -Raw -Path $schemaPath | & $mysqlExePath -u root
    if ($LASTEXITCODE -ne 0) {
        throw "Loading schema.sql failed with exit code $LASTEXITCODE."
    }

    Write-Host "Loading test data..."
    Get-Content -Raw -Path $testDataPath | & $mysqlExePath -u root
    if ($LASTEXITCODE -ne 0) {
        throw "Loading test-data.sql failed with exit code $LASTEXITCODE."
    }

    Write-Host "Development schema and test data loaded successfully."
}
finally {
    $env:MYSQL_PWD = $previousMySqlPassword
}
