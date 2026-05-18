[CmdletBinding(SupportsShouldProcess = $true)]
param(
    [string]$AdapterName,
    [string]$IPAddress,
    [ValidateRange(1, 32)]
    [int]$PrefixLength = 24,
    [string]$DefaultGateway,
    [string[]]$DnsServers = @(),
    [switch]$Dhcp
)

$ErrorActionPreference = "Stop"

function Test-Administrator {
    $currentIdentity = [Security.Principal.WindowsIdentity]::GetCurrent()
    $principal = New-Object Security.Principal.WindowsPrincipal($currentIdentity)
    return $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
}

function Get-TargetAdapter {
    param([string]$RequestedAdapterName)

    $adapters = Get-NetAdapter |
        Where-Object { $_.Status -ne "Disabled" } |
        Sort-Object -Property Status, Name

    if (-not $adapters) {
        throw "No network adapters were found."
    }

    if ($RequestedAdapterName) {
        $adapter = $adapters | Where-Object Name -eq $RequestedAdapterName | Select-Object -First 1
        if (-not $adapter) {
            $availableAdapters = ($adapters | Select-Object -ExpandProperty Name) -join ", "
            throw "Adapter '$RequestedAdapterName' was not found. Available adapters: $availableAdapters"
        }

        return $adapter
    }

    $connectedAdapters = $adapters | Where-Object Status -eq "Up"
    if ($connectedAdapters.Count -eq 1) {
        return $connectedAdapters[0]
    }

    $availableAdapters = ($adapters | Select-Object -ExpandProperty Name) -join ", "
    throw "Multiple adapters are available. Rerun with -AdapterName. Available adapters: $availableAdapters"
}

if (-not (Test-Administrator)) {
    throw "This script must be run from an elevated PowerShell window (Run as Administrator)."
}

if (-not $Dhcp -and [string]::IsNullOrWhiteSpace($IPAddress)) {
    throw "Specify -IPAddress for static configuration, or use -Dhcp to revert to automatic settings."
}

$adapter = Get-TargetAdapter -RequestedAdapterName $AdapterName
$interfaceAlias = $adapter.Name

Write-Host "Using adapter: $interfaceAlias"

if ($Dhcp) {
    if ($PSCmdlet.ShouldProcess($interfaceAlias, "Enable DHCP and automatic DNS")) {
        Get-NetIPAddress -InterfaceAlias $interfaceAlias -AddressFamily IPv4 -ErrorAction SilentlyContinue |
            Where-Object PrefixOrigin -ne "WellKnown" |
            Remove-NetIPAddress -Confirm:$false -ErrorAction SilentlyContinue

        Set-NetIPInterface -InterfaceAlias $interfaceAlias -Dhcp Enabled
        Set-DnsClientServerAddress -InterfaceAlias $interfaceAlias -ResetServerAddresses
    }

    Write-Host "DHCP enabled on '$interfaceAlias'."
    return
}

if ($PSCmdlet.ShouldProcess($interfaceAlias, "Assign static IPv4 settings")) {
    Get-NetIPAddress -InterfaceAlias $interfaceAlias -AddressFamily IPv4 -ErrorAction SilentlyContinue |
        Where-Object PrefixOrigin -ne "WellKnown" |
        Remove-NetIPAddress -Confirm:$false -ErrorAction SilentlyContinue

    Set-NetIPInterface -InterfaceAlias $interfaceAlias -Dhcp Disabled

    $newIpAddressParams = @{
        InterfaceAlias = $interfaceAlias
        AddressFamily  = "IPv4"
        IPAddress      = $IPAddress
        PrefixLength   = $PrefixLength
    }

    if (-not [string]::IsNullOrWhiteSpace($DefaultGateway)) {
        $newIpAddressParams.DefaultGateway = $DefaultGateway
    }

    New-NetIPAddress @newIpAddressParams | Out-Null

    if ($DnsServers.Count -gt 0) {
        Set-DnsClientServerAddress -InterfaceAlias $interfaceAlias -ServerAddresses $DnsServers
    }
    else {
        Set-DnsClientServerAddress -InterfaceAlias $interfaceAlias -ResetServerAddresses
    }
}

Write-Host "Static IP configured on '$interfaceAlias'."
Write-Host "IP Address: $IPAddress/$PrefixLength"
if ($DefaultGateway) {
    Write-Host "Gateway: $DefaultGateway"
}
if ($DnsServers.Count -gt 0) {
    Write-Host "DNS: $($DnsServers -join ', ')"
}