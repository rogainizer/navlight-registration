param(
    [Parameter(Mandatory = $true)]
    [string]$PortName,

    [ValidateSet('ReadAndClear', 'ReadOnly', 'ClearOnly')]
    [string]$Mode = 'ReadAndClear',

    [int]$TimeoutMs = 1000,

    [int]$TagDetectTimeoutMs = -1,

    [switch]$ResetInterface,

    [switch]$NoPrompt,

    [switch]$Continuous
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Convert-HexToUInt32 {
    param([Parameter(Mandatory = $true)][string]$Hex)

    return [Convert]::ToUInt32($Hex, 16)
}

function Convert-DecimalToNavlightAlpha {
    param([Parameter(Mandatory = $true)][uint32]$Value)

    $alphabet = 'ABCDEFGHJKLMNOPQRSTUVWXYZ'.ToCharArray()
    $base = 25
    $number = [uint32]($Value % [math]::Pow($base, 4))

    $a = [int]($number % $base)
    $b = [int]((($number - $a) / $base) % $base)
    $c = [int]((($number - $a - ($base * $b)) / ($base * $base)) % $base)
    $d = [int]((($number - $a - ($base * $b) - (($base * $base) * $c)) / ($base * $base * $base)) % $base)

    return -join @(
        $alphabet[$d]
        $alphabet[$a]
        $alphabet[$c]
        $alphabet[$b]
    )
}

function Read-NavlightLine {
    param(
        [Parameter(Mandatory = $true)]
        [System.IO.Ports.SerialPort]$SerialPort,

        [Parameter(Mandatory = $true)]
        [int]$TimeoutMilliseconds
    )

    $deadline = [DateTime]::UtcNow.AddMilliseconds($TimeoutMilliseconds)
    $buffer = ''

    while ([DateTime]::UtcNow -lt $deadline) {
        $chunk = $SerialPort.ReadExisting()
        if ($chunk) {
            $buffer += $chunk

            while ($true) {
                $newlineIndex = $buffer.IndexOf("`r`n", [System.StringComparison]::Ordinal)
                if ($newlineIndex -lt 0) {
                    break
                }

                $line = $buffer.Substring(0, $newlineIndex).Trim()
                $buffer = $buffer.Substring($newlineIndex + 2)

                if (-not $line) {
                    continue
                }

                if ($line -match '^(FOUND|LOST|Connected)$') {
                    continue
                }

                return $line
            }
        }

        Start-Sleep -Milliseconds 10
    }

    $partial = $buffer.Trim()
    if ($partial) {
        throw "Timed out waiting for a complete response line. Partial data: '$partial'"
    }

    throw 'Timed out waiting for a response from the NavLight reader.'
}

function Send-NavlightCommand {
    param(
        [Parameter(Mandatory = $true)]
        [System.IO.Ports.SerialPort]$SerialPort,

        [Parameter(Mandatory = $true)]
        [string]$Command,

        [int]$TimeoutMilliseconds = 1000,

        [switch]$ExpectResponse,

        [switch]$IgnoreResponse
    )

    $payload = $Command + "`r`n"
    foreach ($character in $payload.ToCharArray()) {
        $SerialPort.Write($character)
        Start-Sleep -Milliseconds 2
    }

    if ($IgnoreResponse) {
        Start-Sleep -Milliseconds 100
        $null = $SerialPort.ReadExisting()
        return $null
    }

    if ($ExpectResponse) {
        return Read-NavlightLine -SerialPort $SerialPort -TimeoutMilliseconds $TimeoutMilliseconds
    }

    return $null
}

function Parse-TagIdResponse {
    param([Parameter(Mandatory = $true)][string]$ResponseLine)

    $tokens = $ResponseLine.Trim().Split(' ', [System.StringSplitOptions]::RemoveEmptyEntries)
    if ($tokens.Length -ne 6) {
        throw "Unexpected T9 response format: '$ResponseLine'"
    }

    if ($tokens[0] -ne '5A') {
        throw "Unexpected T9 response prefix: '$ResponseLine'"
    }

    $checksum = (Convert-HexToUInt32 '5A')
    for ($i = 1; $i -le 4; $i++) {
        $checksum += Convert-HexToUInt32 $tokens[$i]
    }
    $checksum = $checksum % 256

    if ($checksum -ne (Convert-HexToUInt32 $tokens[5])) {
        throw "Invalid T9 checksum in response: '$ResponseLine'"
    }

    $tagIdHex = '{0}{1}{2}' -f $tokens[1], $tokens[2], $tokens[3]
    $courseHex = $tokens[4]
    $tagIdAlpha = Convert-DecimalToNavlightAlpha -Value (Convert-HexToUInt32 $tagIdHex)

    [pscustomobject]@{
        TagIdHex   = $tagIdHex
        TagIdAlpha = $tagIdAlpha
        CourseHex  = $courseHex
        RawReply   = $ResponseLine
    }
}

function Assert-ErasePreparationResponse {
    param(
        [Parameter(Mandatory = $true)]
        [string]$ResponseLine,

        [Parameter(Mandatory = $true)]
        [string]$ExpectedBaseIdSumHex
    )

    $tokens = $ResponseLine.Trim().Split(' ', [System.StringSplitOptions]::RemoveEmptyEntries)
    if ($tokens.Length -lt 2) {
        throw "Unexpected T8 response format: '$ResponseLine'"
    }

    if ($tokens[0] -ne '5A') {
        throw "Unexpected T8 response prefix: '$ResponseLine'"
    }

    if ($tokens[1].ToUpperInvariant() -ne $ExpectedBaseIdSumHex.ToUpperInvariant()) {
        throw "Unexpected T8 checksum byte '$($tokens[1])'. Expected '$ExpectedBaseIdSumHex'."
    }
}

function Assert-AckResponse {
    param([Parameter(Mandatory = $true)][string]$ResponseLine)

    if (-not $ResponseLine.Trim().StartsWith('5A', [System.StringComparison]::OrdinalIgnoreCase)) {
        throw "Command failed. Expected a response starting with 5A, got '$ResponseLine'"
    }
}

function Show-ReaderReadyLight {
    param([Parameter(Mandatory = $true)][System.IO.Ports.SerialPort]$SerialPort)

    Send-NavlightCommand -SerialPort $SerialPort -Command 'T104' -IgnoreResponse
    Start-Sleep -Milliseconds 50
    Send-NavlightCommand -SerialPort $SerialPort -Command 'T106' -IgnoreResponse
}

function Show-ReaderIdleLight {
    param([Parameter(Mandatory = $true)][System.IO.Ports.SerialPort]$SerialPort)

    Send-NavlightCommand -SerialPort $SerialPort -Command 'T100' -IgnoreResponse
}

function Initialize-ReaderInterface {
    param(
        [Parameter(Mandatory = $true)]
        [System.IO.Ports.SerialPort]$SerialPort,

        [Parameter(Mandatory = $true)]
        [int]$TimeoutMilliseconds
    )

    Send-NavlightCommand -SerialPort $SerialPort -Command '*]' -IgnoreResponse

    $deadline = [DateTime]::UtcNow.AddMilliseconds([Math]::Max(200, $TimeoutMilliseconds))
    $rawBuffer = ''

    while ([DateTime]::UtcNow -lt $deadline) {
        $chunk = $SerialPort.ReadExisting()
        if ($chunk) {
            $rawBuffer += $chunk
            if ($rawBuffer.Contains('^') -or $rawBuffer.IndexOf('Connected', [System.StringComparison]::OrdinalIgnoreCase) -ge 0) {
                break
            }
        }

        Start-Sleep -Milliseconds 10
    }

    Send-NavlightCommand -SerialPort $SerialPort -Command '*T' -IgnoreResponse
    $SerialPort.DiscardInBuffer()
}

function Wait-ForReaderReady {
    param(
        [Parameter(Mandatory = $true)]
        [System.IO.Ports.SerialPort]$SerialPort,

        [Parameter(Mandatory = $true)]
        [int]$TimeoutMilliseconds
    )

    $deadline = if ($TimeoutMilliseconds -lt 0) {
        [DateTime]::MaxValue
    }
    else {
        [DateTime]::UtcNow.AddMilliseconds($TimeoutMilliseconds)
    }
    $buffer = ''

    while ([DateTime]::UtcNow -lt $deadline) {
        $chunk = $SerialPort.ReadExisting()
        if ($chunk) {
            $buffer += $chunk

            while ($true) {
                $newlineIndex = $buffer.IndexOf("`r`n", [System.StringComparison]::Ordinal)
                if ($newlineIndex -lt 0) {
                    break
                }

                $line = $buffer.Substring(0, $newlineIndex).Trim()
                $buffer = $buffer.Substring($newlineIndex + 2)

                if (-not $line) {
                    continue
                }

                if ($line -eq 'Connected') {
                    Send-NavlightCommand -SerialPort $SerialPort -Command '*T' -IgnoreResponse
                    continue
                }

                if ($line -eq 'FOUND') {
                    return
                }
            }
        }

        Start-Sleep -Milliseconds 10
    }

    throw 'Timed out waiting for the NavLight reader to detect a tag.'
}

function Wait-ForTagPlacement {
    param(
        [Parameter(Mandatory = $true)]
        [string]$PortName,

        [Parameter(Mandatory = $true)]
        [System.IO.Ports.SerialPort]$SerialPort,

        [Parameter(Mandatory = $true)]
        [string]$Mode,

        [Parameter(Mandatory = $true)]
        [int]$TimeoutMilliseconds,

        [switch]$SkipPrompt
    )

    if (-not $SkipPrompt) {
        $action = switch ($Mode) {
            'ReadOnly' { 'read the tag ID' }
            'ClearOnly' { 'clear the tag' }
            default { 'read and clear the tag' }
        }

        Write-Host "Reader is open on $PortName. Place the reader on the tag, then press Enter to $action..."
        [void](Read-Host)
    }

    Wait-ForReaderReady -SerialPort $SerialPort -TimeoutMilliseconds $TimeoutMilliseconds
}

function Confirm-ContinueWithNextTag {
    param([switch]$SkipPrompt)

    if ($SkipPrompt) {
        return $true
    }

    $reply = Read-Host 'Tag complete. Press Enter for the next tag, or type Q to stop'
    return ($reply.Trim().ToUpperInvariant() -ne 'Q')
}

function Invoke-TagRead {
    param(
        [Parameter(Mandatory = $true)]
        [System.IO.Ports.SerialPort]$SerialPort,

        [Parameter(Mandatory = $true)]
        [int]$TimeoutMilliseconds
    )

    $idReply = Send-NavlightCommand -SerialPort $SerialPort -Command 'T9' -TimeoutMilliseconds $TimeoutMilliseconds -ExpectResponse
    $tagInfo = Parse-TagIdResponse -ResponseLine $idReply

    [pscustomobject]@{
        TagInfo = $tagInfo
        IdReply = $idReply
    }
}

function Invoke-TagClear {
    param(
        [Parameter(Mandatory = $true)]
        [System.IO.Ports.SerialPort]$SerialPort,

        [Parameter(Mandatory = $true)]
        [string]$IdReply,

        [Parameter(Mandatory = $true)]
        [int]$TimeoutMilliseconds
    )

    $idTokens = $IdReply.Split(' ', [System.StringSplitOptions]::RemoveEmptyEntries)
    $baseIdSum = ((Convert-HexToUInt32 $idTokens[1]) +
        (Convert-HexToUInt32 $idTokens[2]) +
        (Convert-HexToUInt32 $idTokens[3])) % 256
    $baseIdSumHex = '{0:X2}' -f $baseIdSum

    $zeroPointerReply = Send-NavlightCommand -SerialPort $SerialPort -Command 'T8' -TimeoutMilliseconds $TimeoutMilliseconds -ExpectResponse
    Assert-ErasePreparationResponse -ResponseLine $zeroPointerReply -ExpectedBaseIdSumHex $baseIdSumHex

    $eraseReply = Send-NavlightCommand -SerialPort $SerialPort -Command 'T607B0FFFFFFFF' -TimeoutMilliseconds $TimeoutMilliseconds -ExpectResponse
    Assert-AckResponse -ResponseLine $eraseReply
}

$serialPort = [System.IO.Ports.SerialPort]::new()
$serialPort.PortName = $PortName
$serialPort.BaudRate = 19200
$serialPort.Parity = [System.IO.Ports.Parity]::None
$serialPort.DataBits = 8
$serialPort.Handshake = [System.IO.Ports.Handshake]::None
$serialPort.StopBits = [System.IO.Ports.StopBits]::One
$serialPort.ReadBufferSize = 40
$serialPort.ReceivedBytesThreshold = 1
$serialPort.ReadTimeout = $TimeoutMs
$serialPort.WriteTimeout = $TimeoutMs
$serialPort.DtrEnable = $true
$serialPort.NewLine = "`r`n"

try {
    $serialPort.Open()
    $serialPort.DiscardInBuffer()
    $serialPort.DiscardOutBuffer()

    Initialize-ReaderInterface -SerialPort $serialPort -TimeoutMilliseconds $TimeoutMs

    if ($ResetInterface) {
        Send-NavlightCommand -SerialPort $serialPort -Command '*T' -IgnoreResponse
        $serialPort.DiscardInBuffer()
    }

    $results = @()
    $keepGoing = $true

    while ($keepGoing) {
        Show-ReaderReadyLight -SerialPort $serialPort

        Wait-ForTagPlacement -PortName $PortName -SerialPort $serialPort -Mode $Mode -TimeoutMilliseconds $TagDetectTimeoutMs -SkipPrompt:$NoPrompt

        $readResult = Invoke-TagRead -SerialPort $serialPort -TimeoutMilliseconds $TimeoutMs
        $tagInfo = $readResult.TagInfo

        if ($Mode -in @('ReadAndClear', 'ClearOnly')) {
            Invoke-TagClear -SerialPort $serialPort -IdReply $readResult.IdReply -TimeoutMilliseconds $TimeoutMs
            Show-ReaderIdleLight -SerialPort $serialPort
        }
        elseif ($Mode -eq 'ReadOnly') {
            Show-ReaderIdleLight -SerialPort $serialPort
        }

        $results += [pscustomobject]@{
            Port       = $PortName
            Mode       = $Mode
            TagIdAlpha = $tagInfo.TagIdAlpha
            TagIdHex   = $tagInfo.TagIdHex
            CourseHex  = $tagInfo.CourseHex
            Cleared    = ($Mode -in @('ReadAndClear', 'ClearOnly'))
        }

        if (-not $Continuous -or $Mode -eq 'ReadOnly') {
            $keepGoing = $false
        }
        else {
            $keepGoing = Confirm-ContinueWithNextTag -SkipPrompt:$NoPrompt
        }
    }

    $results
}
finally {
    if ($serialPort.IsOpen) {
        $serialPort.Close()
    }
    $serialPort.Dispose()
}