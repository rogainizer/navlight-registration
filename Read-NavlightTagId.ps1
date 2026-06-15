param(
    [Parameter(Mandatory = $true)]
    [string]$PortName,

    [int]$TimeoutMs = 1000,

    [int]$TagDetectTimeoutMs = -1,

    [switch]$ResetInterface,

    [switch]$NoPrompt
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$scriptPath = Join-Path -Path $PSScriptRoot -ChildPath 'Clear-NavlightTag.ps1'
if (-not (Test-Path -LiteralPath $scriptPath)) {
    throw "Required script not found: $scriptPath"
}

& $scriptPath -PortName $PortName -Mode ReadAndClear -TimeoutMs $TimeoutMs -TagDetectTimeoutMs $TagDetectTimeoutMs -ResetInterface:$ResetInterface -NoPrompt:$NoPrompt