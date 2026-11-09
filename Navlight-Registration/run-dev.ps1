$ErrorActionPreference = "Stop"

$projectRoot = $PSScriptRoot
$projectPath = Join-Path $projectRoot "Navlight.Registration.App\Navlight.Registration.App.csproj"

if (-not (Test-Path -LiteralPath $projectPath)) {
    throw "Project file not found at '$projectPath'."
}

Write-Host "Starting Navlight Registration in development mode..."
Write-Host "Database target: 127.0.0.1:3306 / navlight-registration"

function Test-DbPort {
    (Test-NetConnection -ComputerName 127.0.0.1 -Port 3306 -WarningAction SilentlyContinue).TcpTestSucceeded
}

if (-not (Test-DbPort)) {
    $svc = Get-Service | Where-Object { $_.Name -match '^(mysql|mariadb)' } | Select-Object -First 1
    if ($svc) {
        Write-Host "MySQL not reachable; starting service '$($svc.Name)'..."
        Start-Service $svc.Name
        $deadline = (Get-Date).AddSeconds(30)
        while (-not (Test-DbPort) -and (Get-Date) -lt $deadline) { Start-Sleep -Milliseconds 500 }
    }
}

if (-not (Test-DbPort)) {
    throw "MySQL is not listening on 127.0.0.1:3306 and no MySQL/MariaDB Windows service was found. Install MySQL Server 8.0 (the folder 'C:\Program Files\MySQL\MySQL Server 8.0' has no mysqld.exe) and create the database with database\schema.sql, or run reset-dev-data.ps1."
}

Set-Location $projectRoot
& dotnet run --project $projectPath
