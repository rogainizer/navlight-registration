using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO.Ports;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace NavLight;

public sealed class NavLightTagReadResult
{
    public required string TagIdAlpha { get; init; }
    public required string TagIdHex { get; init; }
    public required string CourseHex { get; init; }
    public required string RawReply { get; init; }
}

public sealed class NavLightTagReader
{
    private static readonly TimeSpan DefaultResponseTimeout = TimeSpan.FromSeconds(1);
    private static readonly TimeSpan DefaultTagDetectTimeout = Timeout.InfiniteTimeSpan;

    public async Task<NavLightTagReadResult> ReadAndClearTagAsync(
        string portName,
        CancellationToken cancellationToken,
        TimeSpan? responseTimeout = null,
        TimeSpan? tagDetectTimeout = null,
        bool resetInterface = false)
    {
        if (string.IsNullOrWhiteSpace(portName))
        {
            throw new ArgumentException("A COM port name is required.", nameof(portName));
        }

        var effectiveResponseTimeout = responseTimeout ?? DefaultResponseTimeout;
        var effectiveTagDetectTimeout = tagDetectTimeout ?? DefaultTagDetectTimeout;

        using var serialPort = CreatePort(portName, effectiveResponseTimeout);
        serialPort.Open();
        serialPort.DiscardInBuffer();
        serialPort.DiscardOutBuffer();

        var pendingBuffer = new StringBuilder();
        try
        {
            if (resetInterface)
            {
                await SendCommandAsync(serialPort, pendingBuffer, "*T", cancellationToken, ignoreResponse: true).ConfigureAwait(false);
                serialPort.DiscardInBuffer();
            }

            await ShowReaderReadyLightAsync(serialPort, pendingBuffer, cancellationToken).ConfigureAwait(false);
            await WaitForTagInsertionAsync(serialPort, pendingBuffer, effectiveTagDetectTimeout, cancellationToken).ConfigureAwait(false);

            var tagIdReply = await SendCommandExpectingDataAsync(
                serialPort,
                pendingBuffer,
                "T9",
                effectiveResponseTimeout,
                cancellationToken).ConfigureAwait(false);

            var tagInfo = ParseTagIdResponse(tagIdReply);

            var baseIdSumHex = CalculateBaseIdSumHex(tagInfo.TagIdHex);
            var zeroPointerReply = await SendCommandExpectingDataAsync(
                serialPort,
                pendingBuffer,
                "T8",
                effectiveResponseTimeout,
                cancellationToken).ConfigureAwait(false);

            ValidateErasePreparationResponse(zeroPointerReply, baseIdSumHex);

            var eraseReply = await SendCommandExpectingDataAsync(
                serialPort,
                pendingBuffer,
                "T607B0FFFFFFFF",
                effectiveResponseTimeout,
                cancellationToken).ConfigureAwait(false);

            ValidateAckResponse(eraseReply);

            return tagInfo;
        }
        finally
        {
            if (serialPort.IsOpen)
            {
                try
                {
                    await ShowReaderIdleLightAsync(serialPort, pendingBuffer, CancellationToken.None).ConfigureAwait(false);
                }
                catch
                {
                    // Preserve the original failure or cancellation if returning the reader to idle also fails.
                }
            }
        }
    }

    private static SerialPort CreatePort(string portName, TimeSpan responseTimeout)
    {
        var timeoutMs = responseTimeout == Timeout.InfiniteTimeSpan
            ? SerialPort.InfiniteTimeout
            : Math.Max(1, (int)responseTimeout.TotalMilliseconds);

        return new SerialPort
        {
            PortName = portName,
            BaudRate = 19200,
            Parity = Parity.None,
            DataBits = 8,
            Handshake = Handshake.None,
            StopBits = StopBits.One,
            ReadBufferSize = 40,
            ReceivedBytesThreshold = 1,
            ReadTimeout = timeoutMs,
            WriteTimeout = timeoutMs,
            DtrEnable = true,
            NewLine = "\r\n"
        };
    }

    private static async Task ShowReaderReadyLightAsync(
        SerialPort serialPort,
        StringBuilder pendingBuffer,
        CancellationToken cancellationToken)
    {
        await SendCommandAsync(serialPort, pendingBuffer, "T104", cancellationToken, ignoreResponse: true).ConfigureAwait(false);
        await Task.Delay(50, cancellationToken).ConfigureAwait(false);
        await SendCommandAsync(serialPort, pendingBuffer, "T106", cancellationToken, ignoreResponse: true).ConfigureAwait(false);
    }

    private static Task ShowReaderIdleLightAsync(
        SerialPort serialPort,
        StringBuilder pendingBuffer,
        CancellationToken cancellationToken)
    {
        return SendCommandAsync(serialPort, pendingBuffer, "T100", cancellationToken, ignoreResponse: true);
    }

    private static async Task WaitForTagInsertionAsync(
        SerialPort serialPort,
        StringBuilder pendingBuffer,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        var deadline = timeout == Timeout.InfiniteTimeSpan
            ? DateTimeOffset.MaxValue
            : DateTimeOffset.UtcNow.Add(timeout);

        while (DateTimeOffset.UtcNow < deadline)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var line = await ReadNextLineAsync(serialPort, pendingBuffer, timeout, cancellationToken).ConfigureAwait(false);
            if (line.Length == 0)
            {
                continue;
            }

            if (line.Equals("FOUND", StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            if (line.Equals("Connected", StringComparison.OrdinalIgnoreCase))
            {
                await SendCommandAsync(serialPort, pendingBuffer, "*T", cancellationToken, ignoreResponse: true).ConfigureAwait(false);
                continue;
            }
        }

        throw new TimeoutException("Timed out waiting for a tag to be placed on the reader.");
    }

    private static async Task<string> SendCommandExpectingDataAsync(
        SerialPort serialPort,
        StringBuilder pendingBuffer,
        string command,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        await SendCommandAsync(serialPort, pendingBuffer, command, cancellationToken, ignoreResponse: false).ConfigureAwait(false);

        while (true)
        {
            var line = await ReadNextLineAsync(serialPort, pendingBuffer, timeout, cancellationToken).ConfigureAwait(false);
            if (line.Length == 0 || IsStatusLine(line))
            {
                continue;
            }

            return line;
        }
    }

    private static async Task SendCommandAsync(
        SerialPort serialPort,
        StringBuilder pendingBuffer,
        string command,
        CancellationToken cancellationToken,
        bool ignoreResponse)
    {
        var payload = command + "\r\n";
        foreach (var character in payload)
        {
            cancellationToken.ThrowIfCancellationRequested();
            serialPort.Write(character.ToString());
            await Task.Delay(2, cancellationToken).ConfigureAwait(false);
        }

        if (ignoreResponse)
        {
            await Task.Delay(100, cancellationToken).ConfigureAwait(false);
            pendingBuffer.Clear();
            pendingBuffer.Append(serialPort.ReadExisting());
        }
    }

    private static async Task<string> ReadNextLineAsync(
        SerialPort serialPort,
        StringBuilder pendingBuffer,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        var deadline = timeout == Timeout.InfiniteTimeSpan
            ? DateTimeOffset.MaxValue
            : DateTimeOffset.UtcNow.Add(timeout);

        while (DateTimeOffset.UtcNow < deadline)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var chunk = serialPort.ReadExisting();
            if (!string.IsNullOrEmpty(chunk))
            {
                pendingBuffer.Append(chunk);
            }

            var bufferText = pendingBuffer.ToString();
            var newlineIndex = bufferText.IndexOf("\r\n", StringComparison.Ordinal);
            if (newlineIndex >= 0)
            {
                var line = bufferText[..newlineIndex].Trim();
                pendingBuffer.Clear();
                pendingBuffer.Append(bufferText[(newlineIndex + 2)..]);
                return line;
            }

            await Task.Delay(10, cancellationToken).ConfigureAwait(false);
        }

        var partial = pendingBuffer.ToString().Trim();
        if (partial.Length > 0)
        {
            throw new TimeoutException($"Timed out waiting for a complete response line. Partial data: '{partial}'");
        }

        throw new TimeoutException("Timed out waiting for a response from the NavLight reader.");
    }

    private static bool IsStatusLine(string line)
    {
        return line.Equals("FOUND", StringComparison.OrdinalIgnoreCase)
            || line.Equals("LOST", StringComparison.OrdinalIgnoreCase)
            || line.Equals("Connected", StringComparison.OrdinalIgnoreCase);
    }

    private static NavLightTagReadResult ParseTagIdResponse(string responseLine)
    {
        var tokens = responseLine.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (tokens.Length != 6)
        {
            throw new InvalidOperationException($"Unexpected T9 response format: '{responseLine}'");
        }

        if (!tokens[0].Equals("5A", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"Unexpected T9 response prefix: '{responseLine}'");
        }

        var checksum = ConvertHexToUInt32("5A");
        for (var index = 1; index <= 4; index++)
        {
            checksum += ConvertHexToUInt32(tokens[index]);
        }

        checksum %= 256;
        if (checksum != ConvertHexToUInt32(tokens[5]))
        {
            throw new InvalidOperationException($"Invalid T9 checksum in response: '{responseLine}'");
        }

        var tagIdHex = string.Concat(tokens[1], tokens[2], tokens[3]);

        return new NavLightTagReadResult
        {
            TagIdHex = tagIdHex,
            TagIdAlpha = ConvertDecimalToNavLightAlpha(ConvertHexToUInt32(tagIdHex)),
            CourseHex = tokens[4],
            RawReply = responseLine
        };
    }

    private static void ValidateErasePreparationResponse(string responseLine, string expectedBaseIdSumHex)
    {
        var tokens = responseLine.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (tokens.Length < 2)
        {
            throw new InvalidOperationException($"Unexpected T8 response format: '{responseLine}'");
        }

        if (!tokens[0].Equals("5A", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"Unexpected T8 response prefix: '{responseLine}'");
        }

        if (!tokens[1].Equals(expectedBaseIdSumHex, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"Unexpected T8 checksum byte '{tokens[1]}'. Expected '{expectedBaseIdSumHex}'.");
        }
    }

    private static void ValidateAckResponse(string responseLine)
    {
        if (!responseLine.StartsWith("5A", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"Command failed. Expected a response starting with 5A, got '{responseLine}'");
        }
    }

    private static string CalculateBaseIdSumHex(string tagIdHex)
    {
        if (tagIdHex.Length != 6)
        {
            throw new ArgumentException("Tag ID hex must be exactly 6 characters.", nameof(tagIdHex));
        }

        var byte1 = tagIdHex[..2];
        var byte2 = tagIdHex.Substring(2, 2);
        var byte3 = tagIdHex.Substring(4, 2);
        var total = (ConvertHexToUInt32(byte1) + ConvertHexToUInt32(byte2) + ConvertHexToUInt32(byte3)) % 256;
        return total.ToString("X2", CultureInfo.InvariantCulture);
    }

    private static uint ConvertHexToUInt32(string hex)
    {
        return uint.Parse(hex, NumberStyles.HexNumber, CultureInfo.InvariantCulture);
    }

    private static string ConvertDecimalToNavLightAlpha(uint value)
    {
        const string alphabet = "ABCDEFGHJKLMNOPQRSTUVWXYZ";
        const uint baseValue = 25;
        var number = value % (baseValue * baseValue * baseValue * baseValue);

        var a = (int)(number % baseValue);
        var b = (int)(((number - a) / baseValue) % baseValue);
        var c = (int)(((number - a - (baseValue * (uint)b)) / (baseValue * baseValue)) % baseValue);
        var d = (int)(((number - a - (baseValue * (uint)b) - ((baseValue * baseValue) * (uint)c)) /
            (baseValue * baseValue * baseValue)) % baseValue);

        return string.Create(4, (a, b, c, d), static (span, state) =>
        {
            const string localAlphabet = "ABCDEFGHJKLMNOPQRSTUVWXYZ";
            span[0] = localAlphabet[state.d];
            span[1] = localAlphabet[state.a];
            span[2] = localAlphabet[state.c];
            span[3] = localAlphabet[state.b];
        });
    }
}