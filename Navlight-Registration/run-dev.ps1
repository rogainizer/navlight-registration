$ErrorActionPreference = "Stop"

$projectRoot = $PSScriptRoot
$projectPath = Join-Path $projectRoot "Navlight.Registration.App\Navlight.Registration.App.csproj"

if (-not (Test-Path -LiteralPath $projectPath)) {
    throw "Project file not found at '$projectPath'."
}

Write-Host "Starting Navlight Registration in development mode..."
Write-Host "Database target: 127.0.0.1:3306 / navlight-registration"

Set-Location $projectRoot
& dotnet run --project $projectPath
