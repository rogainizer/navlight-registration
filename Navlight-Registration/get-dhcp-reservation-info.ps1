[CmdletBinding()]
param(
    [string]$AdapterName
)

$ErrorActionPreference = "Stop"

function Get-TargetConfiguration {
    param([string]$RequestedAdapterName)

    $configurations = Get-NetIPConfiguration |
        Where-Object {
            $_.NetAdapter.Status -eq "Up" -and
            $_.IPv4Address -and
            $_.IPv4Address.IPAddress -notlike '127.*' -and
            $_.IPv4Address.IPAddress -notlike '169.254.*'
        } |
        Sort-Object -Property InterfaceMetric, InterfaceAlias

    if (-not $configurations) {
        throw "No active IPv4 network adapters were found."
    }

    if (-not [string]::IsNullOrWhiteSpace($RequestedAdapterName)) {
        $configuration = $configurations |
            Where-Object { $_.InterfaceAlias -eq $RequestedAdapterName } |
            Select-Object -First 1

        if (-not $configuration) {
            $availableAdapters = ($configurations | Select-Object -ExpandProperty InterfaceAlias) -join ", "
            throw "Adapter '$RequestedAdapterName' was not found. Available adapters: $availableAdapters"
        }

        return $configuration
    }

    $defaultGatewayConfigurations = $configurations | Where-Object { $_.IPv4DefaultGateway -and $_.IPv4DefaultGateway.NextHop }
    if ($defaultGatewayConfigurations.Count -eq 1) {
        return $defaultGatewayConfigurations[0]
    }

    if ($configurations.Count -eq 1) {
        return $configurations[0]
    }

    $availableAdapters = ($configurations | Select-Object -ExpandProperty InterfaceAlias) -join ", "
    throw "Multiple active adapters were found. Rerun with -AdapterName. Available adapters: $availableAdapters"
}

$configuration = Get-TargetConfiguration -RequestedAdapterName $AdapterName
$adapter = Get-NetAdapter -InterfaceIndex $configuration.InterfaceIndex -ErrorAction Stop

$gateway = if ($configuration.IPv4DefaultGateway) { $configuration.IPv4DefaultGateway.NextHop } else { "" }

Write-Host "Use these details for the router DHCP reservation:"
Write-Host "Adapter: $($configuration.InterfaceAlias)"
Write-Host "IPv4 Address: $($configuration.IPv4Address.IPAddress)"
if (-not [string]::IsNullOrWhiteSpace($gateway)) {
    Write-Host "Default Gateway: $gateway"
}
Write-Host "MAC Address: $($adapter.MacAddress)"

[pscustomobject]@{
    AdapterName    = $configuration.InterfaceAlias
    IPv4Address    = $configuration.IPv4Address.IPAddress
    DefaultGateway = $gateway
    MacAddress     = $adapter.MacAddress
}