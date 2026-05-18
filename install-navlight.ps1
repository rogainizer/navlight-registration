[CmdletBinding(SupportsShouldProcess = $true)]
param(
    [ValidateSet("ClientOnly", "HostAndClient")]
    [string]$InstallMode,
    [string]$InstallRoot,
    [string]$DatabaseServer = "navlighthost",
    [int]$DatabasePort = 3306,
    [string]$DatabaseName = "navlight_registration",
    [string]$DatabaseUser = "root",
    [SecureString]$DatabasePassword = (ConvertTo-SecureString "root" -AsPlainText -Force),
    [string]$MySqlZipPath,
    [string]$MySqlDownloadUrl = "https://dev.mysql.com/get/Downloads/MySQL-8.0/mysql-8.0.42-winx64.zip",
    [switch]$ConfigureStaticIp,
    [string]$AdapterName,
    [string]$IPAddress,
    [ValidateRange(1, 32)]
    [int]$PrefixLength = 24,
    [string]$DefaultGateway,
    [string[]]$DnsServers = @()
)

$ErrorActionPreference = "Stop"

function Read-RequiredValue {
    param(
        [string]$Prompt,
        [string]$DefaultValue
    )

    while ($true) {
        $displayPrompt = if ([string]::IsNullOrWhiteSpace($DefaultValue)) { $Prompt } else { "$Prompt [$DefaultValue]" }
        $value = Read-Host $displayPrompt
        if (-not [string]::IsNullOrWhiteSpace($value)) {
            return $value.Trim()
        }

        if (-not [string]::IsNullOrWhiteSpace($DefaultValue)) {
            return $DefaultValue
        }
    }
}

function Read-YesNo {
    param(
        [string]$Prompt,
        [bool]$DefaultValue = $false
    )

    $defaultSuffix = if ($DefaultValue) { "Y/n" } else { "y/N" }
    while ($true) {
        $answer = Read-Host "$Prompt [$defaultSuffix]"
        if ([string]::IsNullOrWhiteSpace($answer)) {
            return $DefaultValue
        }

        switch ($answer.Trim().ToLowerInvariant()) {
            "y" { return $true }
            "yes" { return $true }
            "n" { return $false }
            "no" { return $false }
        }
    }
}

function Read-ValueWithDefault {
    param(
        [string]$Prompt,
        [string]$DefaultValue
    )

    $displayPrompt = if ([string]::IsNullOrWhiteSpace($DefaultValue)) { $Prompt } else { "$Prompt [$DefaultValue]" }
    $value = Read-Host $displayPrompt
    if ([string]::IsNullOrWhiteSpace($value)) {
        return $DefaultValue
    }

    return $value.Trim()
}

function Test-Administrator {
    $currentIdentity = [Security.Principal.WindowsIdentity]::GetCurrent()
    $principal = New-Object Security.Principal.WindowsPrincipal($currentIdentity)
    return $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
}

function Test-PathUnderRoot {
    param(
        [string]$Path,
        [string]$RootPath
    )

    if ([string]::IsNullOrWhiteSpace($Path) -or [string]::IsNullOrWhiteSpace($RootPath)) {
        return $false
    }

    $normalizedPath = [System.IO.Path]::GetFullPath($Path).TrimEnd('\\')
    $normalizedRoot = [System.IO.Path]::GetFullPath($RootPath).TrimEnd('\\')

    return $normalizedPath.StartsWith($normalizedRoot, [System.StringComparison]::OrdinalIgnoreCase)
}

function Get-PlainText {
    param([SecureString]$Value)

    $credential = New-Object System.Management.Automation.PSCredential "unused", $Value
    return $credential.GetNetworkCredential().Password
}

function Ensure-Directory {
    param([string]$Path)

    if (-not (Test-Path -LiteralPath $Path)) {
        New-Item -ItemType Directory -Path $Path | Out-Null
    }
}

function Get-PrimaryIPv4Address {
    $addresses = Get-NetIPAddress -AddressFamily IPv4 -ErrorAction SilentlyContinue |
        Where-Object {
            $_.IPAddress -notlike '127.*' -and
            $_.IPAddress -notlike '169.254.*' -and
            $_.PrefixOrigin -ne 'WellKnown'
        } |
        Sort-Object -Property SkipAsSource, InterfaceMetric, IPAddress

    return ($addresses | Select-Object -First 1 -ExpandProperty IPAddress)
}

function Get-PrimaryNetworkReservationInfo {
    $address = Get-NetIPAddress -AddressFamily IPv4 -ErrorAction SilentlyContinue |
        Where-Object {
            $_.IPAddress -notlike '127.*' -and
            $_.IPAddress -notlike '169.254.*' -and
            $_.PrefixOrigin -ne 'WellKnown'
        } |
        Sort-Object -Property SkipAsSource, InterfaceMetric, IPAddress |
        Select-Object -First 1

    if (-not $address) {
        return $null
    }

    $adapter = Get-NetAdapter -InterfaceIndex $address.InterfaceIndex -ErrorAction SilentlyContinue
    return [pscustomobject]@{
        IPAddress      = $address.IPAddress
        InterfaceAlias = $address.InterfaceAlias
        MacAddress     = if ($adapter) { $adapter.MacAddress } else { $null }
    }
}

function Write-AppSettings {
    param(
        [string]$Path,
        [string]$Server,
        [int]$Port,
        [string]$Name,
        [string]$User,
        [string]$Password
    )

    $settings = [ordered]@{
        Database = [ordered]@{
            Server   = $Server
            Port     = $Port
            Database = $Name
            User     = $User
            Password = $Password
        }
    }

    $settings | ConvertTo-Json -Depth 5 | Set-Content -Path $Path -Encoding UTF8
}

function Install-Shortcut {
    param(
        [string]$ShortcutPath,
        [string]$TargetPath,
        [string]$WorkingDirectory,
        [string]$Arguments = ""
    )

    $shell = New-Object -ComObject WScript.Shell
    $shortcut = $shell.CreateShortcut($ShortcutPath)
    $shortcut.TargetPath = $TargetPath
    $shortcut.WorkingDirectory = $WorkingDirectory
    $shortcut.Arguments = $Arguments
    $shortcut.Save()
}

function Expand-MySqlArchive {
    param(
        [string]$ArchivePath,
        [string]$DestinationPath
    )

    $extractRoot = Join-Path ([System.IO.Path]::GetTempPath()) ("navlight-mysql-" + [Guid]::NewGuid().ToString("N"))
    try {
        Expand-Archive -LiteralPath $ArchivePath -DestinationPath $extractRoot -Force

        $mysqlFolder = Get-ChildItem -LiteralPath $extractRoot -Directory | Select-Object -First 1
        if (-not $mysqlFolder) {
            throw "The MySQL archive did not contain a top-level directory."
        }

        Ensure-Directory -Path $DestinationPath
        Get-ChildItem -LiteralPath $DestinationPath -Force -ErrorAction SilentlyContinue | Remove-Item -Recurse -Force
        Copy-Item -Path (Join-Path $mysqlFolder.FullName "*") -Destination $DestinationPath -Recurse -Force
    }
    finally {
        if (Test-Path -LiteralPath $extractRoot) {
            Remove-Item -LiteralPath $extractRoot -Recurse -Force
        }
    }
}

if ([string]::IsNullOrWhiteSpace($InstallMode)) {
    Write-Host "Choose installation mode:"
    Write-Host "1. Client only"
    Write-Host "2. Host and client"
    do {
        $selection = Read-Host "Enter 1 or 2"
    }
    until ($selection -in @("1", "2"))

    $InstallMode = if ($selection -eq "1") { "ClientOnly" } else { "HostAndClient" }
}

if ([string]::IsNullOrWhiteSpace($InstallRoot)) {
    $defaultInstallRoot = Join-Path $env:USERPROFILE "NavlightRegistration"
    $InstallRoot = Read-ValueWithDefault -Prompt "Install folder" -DefaultValue $defaultInstallRoot
}

if ($ConfigureStaticIp -or
    -not [string]::IsNullOrWhiteSpace($AdapterName) -or
    -not [string]::IsNullOrWhiteSpace($IPAddress) -or
    -not [string]::IsNullOrWhiteSpace($DefaultGateway) -or
    $DnsServers.Count -gt 0) {
    throw "This installer no longer changes the PC network configuration. Create a DHCP reservation on the router for the host machine, then rerun the installer without static IP parameters."
}

$requiresAdminForInstallRoot = Test-PathUnderRoot -Path $InstallRoot -RootPath $env:ProgramFiles
if (-not (Test-Administrator) -and $requiresAdminForInstallRoot) {
    throw "Install folder '$InstallRoot' is under Program Files. Choose a non-Program Files folder or rerun as Administrator."
}

$scriptRoot = $PSScriptRoot
$payloadRoot = Join-Path $scriptRoot "payload"
$sourceAppDir = Join-Path $payloadRoot "Navlight.Registration.App"
$sourceDatabaseDir = Join-Path $payloadRoot "Database"

if (-not (Test-Path -LiteralPath $sourceAppDir)) {
    throw "The release payload was not found. Expected '$sourceAppDir'."
}

if ($InstallMode -eq "ClientOnly" -and [string]::IsNullOrWhiteSpace($DatabaseServer)) {
    $DatabaseServer = Read-RequiredValue -Prompt "Enter the database host name" -DefaultValue "navlighthost"
}

Ensure-Directory -Path $InstallRoot

$targetAppDir = Join-Path $InstallRoot "Navlight.Registration.App"
$targetDatabaseDir = Join-Path $InstallRoot "Database"

if ($PSCmdlet.ShouldProcess($InstallRoot, "Install Navlight files")) {
    if (Test-Path -LiteralPath $targetAppDir) {
        Remove-Item -LiteralPath $targetAppDir -Recurse -Force
    }

    Copy-Item -Path $sourceAppDir -Destination $targetAppDir -Recurse
}

$plainDatabasePassword = Get-PlainText -Value $DatabasePassword
$resolvedDatabaseServer = if ([string]::IsNullOrWhiteSpace($DatabaseServer)) {
    "navlighthost"
}
else {
    $DatabaseServer
}

if ($InstallMode -eq "HostAndClient") {
    $reservationInfo = Get-PrimaryNetworkReservationInfo
    if ($reservationInfo) {
        Write-Host "Router DHCP reservation details for this host:"
        Write-Host "  Adapter: $($reservationInfo.InterfaceAlias)"
        Write-Host "  Current IPv4: $($reservationInfo.IPAddress)"
        if (-not [string]::IsNullOrWhiteSpace($reservationInfo.MacAddress)) {
            Write-Host "  MAC address: $($reservationInfo.MacAddress)"
        }
    }

    $resolvedDatabaseServer = Read-ValueWithDefault -Prompt "Host name for other client PCs to use" -DefaultValue $resolvedDatabaseServer
    if ([string]::IsNullOrWhiteSpace($resolvedDatabaseServer)) {
        throw "Host installs require a client-facing host name."
    }

    $reservationConfirmed = Read-YesNo -Prompt "Have you already created a DHCP reservation on the router for this host so clients will always use $resolvedDatabaseServer" -DefaultValue $false
    if (-not $reservationConfirmed) {
        throw "Host install stopped. Create the DHCP reservation on the router first, then rerun the installer."
    }

    Write-Host "Checking that hostname '$resolvedDatabaseServer' resolves on this PC..."
    $hostPingSucceeded = Test-Connection -ComputerName $resolvedDatabaseServer -Count 1 -Quiet -ErrorAction SilentlyContinue
    if (-not $hostPingSucceeded) {
        throw "Host install stopped. Ping to '$resolvedDatabaseServer' failed. Confirm the router reservation and local DNS entry exist, then rerun the installer."
    }
}

Write-AppSettings -Path (Join-Path $targetAppDir "appsettings.json") -Server $resolvedDatabaseServer -Port $DatabasePort -Name $DatabaseName -User $DatabaseUser -Password $plainDatabasePassword

if ($InstallMode -eq "HostAndClient") {
    if ($PSCmdlet.ShouldProcess($targetDatabaseDir, "Install host database files")) {
        if (Test-Path -LiteralPath $targetDatabaseDir) {
            Remove-Item -LiteralPath $targetDatabaseDir -Recurse -Force
        }

        Copy-Item -Path $sourceDatabaseDir -Destination $targetDatabaseDir -Recurse
    }

    $targetMySqlDir = Join-Path $targetDatabaseDir "mysql"
    if (-not (Test-Path -LiteralPath (Join-Path $targetMySqlDir "bin\mysqld.exe"))) {
        $resolvedMySqlZipPath = $MySqlZipPath

        if ([string]::IsNullOrWhiteSpace($resolvedMySqlZipPath)) {
            if (Read-YesNo -Prompt "Download MySQL automatically during install" -DefaultValue $true) {
                $resolvedMySqlZipPath = Join-Path $env:TEMP "navlight-mysql.zip"
                Write-Host "Downloading MySQL archive from $MySqlDownloadUrl ..."
                Invoke-WebRequest -Uri $MySqlDownloadUrl -OutFile $resolvedMySqlZipPath
            }
            else {
                $resolvedMySqlZipPath = Read-RequiredValue -Prompt "Path to MySQL ZIP archive" -DefaultValue ""
            }
        }

        if (-not (Test-Path -LiteralPath $resolvedMySqlZipPath)) {
            throw "MySQL archive was not found at '$resolvedMySqlZipPath'."
        }

        Write-Host "Extracting MySQL archive..."
        Expand-MySqlArchive -ArchivePath $resolvedMySqlZipPath -DestinationPath $targetMySqlDir
    }

    $setupMySqlScript = Join-Path $targetDatabaseDir "setup-mysql.ps1"
    & $setupMySqlScript -RootPassword $DatabasePassword -Port $DatabasePort -SchemaPath (Join-Path $targetDatabaseDir "schema.sql")
    if ($LASTEXITCODE -ne 0) {
        throw "MySQL setup failed with exit code $LASTEXITCODE."
    }
}

$desktopPath = [Environment]::GetFolderPath("Desktop")
$startMenuProgramsPath = [Environment]::GetFolderPath("Programs")
$startMenuFolder = Join-Path $startMenuProgramsPath "Navlight"
Ensure-Directory -Path $startMenuFolder

Install-Shortcut -ShortcutPath (Join-Path $desktopPath "Navlight Registration.lnk") -TargetPath (Join-Path $targetAppDir "Navlight.Registration.App.exe") -WorkingDirectory $targetAppDir
Install-Shortcut -ShortcutPath (Join-Path $startMenuFolder "Navlight Registration.lnk") -TargetPath (Join-Path $targetAppDir "Navlight.Registration.App.exe") -WorkingDirectory $targetAppDir

if ($InstallMode -eq "HostAndClient") {
    $powerShellExe = Join-Path $PSHOME "powershell.exe"
    $startMySqlScript = Join-Path $targetDatabaseDir "start-mysql.ps1"
    $stopMySqlScript = Join-Path $targetDatabaseDir "stop-mysql.ps1"
    $startArgs = "-ExecutionPolicy Bypass -File `"$startMySqlScript`" -Port $DatabasePort"
    $stopArgs = "-ExecutionPolicy Bypass -File `"$stopMySqlScript`" -Port $DatabasePort"

    Install-Shortcut -ShortcutPath (Join-Path $desktopPath "Navlight Start MySQL.lnk") -TargetPath $powerShellExe -WorkingDirectory $targetDatabaseDir -Arguments $startArgs
    Install-Shortcut -ShortcutPath (Join-Path $desktopPath "Navlight Stop MySQL.lnk") -TargetPath $powerShellExe -WorkingDirectory $targetDatabaseDir -Arguments $stopArgs
    Install-Shortcut -ShortcutPath (Join-Path $startMenuFolder "Navlight Start MySQL.lnk") -TargetPath $powerShellExe -WorkingDirectory $targetDatabaseDir -Arguments $startArgs
    Install-Shortcut -ShortcutPath (Join-Path $startMenuFolder "Navlight Stop MySQL.lnk") -TargetPath $powerShellExe -WorkingDirectory $targetDatabaseDir -Arguments $stopArgs
}

Write-Host "Navlight installation complete."
Write-Host "Mode: $InstallMode"
Write-Host "Install root: $InstallRoot"
Write-Host "Database server for the client: $resolvedDatabaseServer"