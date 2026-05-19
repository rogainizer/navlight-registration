param(
    [int]$TimeoutMs = 750,

    [int]$InitialDelayMs = 200,

    [switch]$AllMatches,

    [int]$MaxPortNumber = 20
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Get-CandidatePorts {
    param([int]$MaxPort)

    [System.IO.Ports.SerialPort]::GetPortNames() |
        Where-Object {
            $_ -match '^COM\d+$' -and ([int]($_.Substring(3)) -le $MaxPort)
        } |
        Sort-Object { [int]($_.Substring(3)) }
}

function Read-NavlightProbeResponse {
    param(
        [Parameter(Mandatory = $true)]
        [System.IO.Ports.SerialPort]$SerialPort,

        [Parameter(Mandatory = $true)]
        [int]$TimeoutMilliseconds
    )

    $deadline = [DateTime]::UtcNow.AddMilliseconds($TimeoutMilliseconds)
    $buffer = New-Object System.Text.StringBuilder

    while ([DateTime]::UtcNow -lt $deadline) {
        $chunk = $SerialPort.ReadExisting()
        if ($chunk) {
            [void]$buffer.Append($chunk)
            $text = $buffer.ToString()
            if ($text.Contains('^') -or $text.IndexOf('Connected', [System.StringComparison]::OrdinalIgnoreCase) -ge 0) {
                return $text
            }
        }

        Start-Sleep -Milliseconds 10
    }

    return $buffer.ToString()
}

function Send-NavlightProbe {
    param([Parameter(Mandatory = $true)][System.IO.Ports.SerialPort]$SerialPort)

    $payload = '*]' + "`r`n"
    foreach ($character in $payload.ToCharArray()) {
        $SerialPort.Write($character)
        Start-Sleep -Milliseconds 2
    }
}

function Test-NavlightPort {
    param(
        [Parameter(Mandatory = $true)]
        [string]$PortName,

        [Parameter(Mandatory = $true)]
        [int]$TimeoutMilliseconds,

        [Parameter(Mandatory = $true)]
        [int]$DelayMilliseconds
    )

    $serialPort = [System.IO.Ports.SerialPort]::new()
    $serialPort.PortName = $PortName
    $serialPort.BaudRate = 19200
    $serialPort.Parity = [System.IO.Ports.Parity]::None
    $serialPort.DataBits = 8
    $serialPort.Handshake = [System.IO.Ports.Handshake]::None
    $serialPort.StopBits = [System.IO.Ports.StopBits]::One
    $serialPort.ReadBufferSize = 40
    $serialPort.ReceivedBytesThreshold = 1
    $serialPort.ReadTimeout = $TimeoutMilliseconds
    $serialPort.WriteTimeout = $TimeoutMilliseconds
    $serialPort.DtrEnable = $true
    $serialPort.NewLine = "`r`n"

    try {
        $serialPort.Open()
        $serialPort.DiscardInBuffer()
        $serialPort.DiscardOutBuffer()
        Start-Sleep -Milliseconds $DelayMilliseconds
        Send-NavlightProbe -SerialPort $serialPort
        $response = Read-NavlightProbeResponse -SerialPort $serialPort -TimeoutMilliseconds $TimeoutMilliseconds

        [pscustomobject]@{
            PortName    = $PortName
            IsNavLight  = ($response.Contains('^') -or $response.IndexOf('Connected', [System.StringComparison]::OrdinalIgnoreCase) -ge 0)
            RawResponse = $response.Trim()
        }
    }
    catch {
        [pscustomobject]@{
            PortName    = $PortName
            IsNavLight  = $false
            RawResponse = ''
            Error       = $_.Exception.Message
        }
    }
    finally {
        if ($serialPort.IsOpen) {
            $serialPort.Close()
        }
        $serialPort.Dispose()
    }
}

$ports = Get-CandidatePorts -MaxPort $MaxPortNumber
if (-not $ports) {
    throw 'No candidate COM ports were found.'
}

$matches = foreach ($port in $ports) {
    $result = Test-NavlightPort -PortName $port -TimeoutMilliseconds $TimeoutMs -DelayMilliseconds $InitialDelayMs
    if ($result.IsNavLight) {
        $result
        if (-not $AllMatches) {
            break
        }
    }
}

if (-not $matches) {
    throw 'No NavLight reader was detected on the scanned COM ports.'
}

$matches