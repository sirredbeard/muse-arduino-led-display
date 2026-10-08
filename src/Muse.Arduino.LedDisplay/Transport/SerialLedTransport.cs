using System.IO.Ports;
using System.Text;
using Muse.Arduino.LedDisplay.Display;
using Arduino.Led;

namespace Muse.Arduino.LedDisplay.Transport;

/// <summary>
/// Drives the MCU sketch over the SoC-to-MCU USB serial link
/// (default <c>/dev/ttyACM0</c> on the VENTUNO Q). All rendering happens in
/// .NET; the sketch only unpacks frames and calls Arduino_LED_Matrix.
/// </summary>
public sealed class SerialLedTransport : ILedTransport
{
    private readonly SerialPort _port;
    private readonly SemaphoreSlim _writeLock = new(1, 1);
    private bool _disposed;

    /// <summary>Creates a transport for the given serial port.</summary>
    public SerialLedTransport(string portName, int baudRate = 115200)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(portName);

        _port = new SerialPort(portName, baudRate, Parity.None, 8, StopBits.One)
        {
            Handshake = Handshake.None,
            ReadTimeout = 2000,
            WriteTimeout = 2000,
            NewLine = "\n",
        };
    }

    /// <summary>Opens the serial port.</summary>
    /// <exception cref="InvalidOperationException">When the port cannot be opened.</exception>
    public void Open()
    {
        try
        {
            _port.Open();
            // Give the MCU a beat to settle after the USB CDC open.
            Thread.Sleep(250);
            _port.DiscardInBuffer();
            _port.DiscardOutBuffer();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException)
        {
            throw new InvalidOperationException(
                $"Could not open serial port '{_port.PortName}': {ex.Message}", ex);
        }
    }

    /// <inheritdoc/>
    public async Task ShowFrameAsync(Frame frame, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(frame);
        await WriteLineAsync(Protocol.EncodeFrame1(frame), cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task ShowGrayFrameAsync(GrayFrame frame, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(frame);
        await WriteLineAsync(Protocol.EncodeFrame3(frame), cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task SetRgbAsync(int index, byte r, byte g, byte b, CancellationToken cancellationToken)
    {
        await WriteLineAsync(Protocol.EncodeRgb(index, r, g, b), cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task ClearAsync(CancellationToken cancellationToken)
    {
        await WriteLineAsync(Protocol.EncodeClear(), cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task<bool> PingAsync(CancellationToken cancellationToken)
    {
        try
        {
            await WriteLineAsync(Protocol.EncodePing(), cancellationToken).ConfigureAwait(false);
            string? reply = await ReadLineAsync(cancellationToken).ConfigureAwait(false);
            return reply is not null && Protocol.TryParsePong(reply.AsSpan()) == Protocol.Version;
        }
        catch (OperationCanceledException)
        {
            return false;
        }
        catch (IOException)
        {
            return false;
        }
        catch (TimeoutException)
        {
            return false;
        }
    }

    private async Task WriteLineAsync(string command, CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        byte[] line = Protocol.ToLineBytes(command);
        await _writeLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await _port.BaseStream.WriteAsync(line, cancellationToken).ConfigureAwait(false);
            await _port.BaseStream.FlushAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _writeLock.Release();
        }
    }

    private async Task<string?> ReadLineAsync(CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        var sb = new StringBuilder(64);
        var one = new byte[1];
        Stream stream = _port.BaseStream;

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(2));

        while (sb.Length < Protocol.MaxLineLength)
        {
            int read = await stream.ReadAsync(one.AsMemory(), timeout.Token).ConfigureAwait(false);
            if (read == 0)
            {
                return null;
            }

            char c = (char)one[0];
            if (c == '\n')
            {
                break;
            }

            if (c != '\r')
            {
                sb.Append(c);
            }
        }

        return sb.ToString();
    }

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _writeLock.Dispose();
        if (_port.IsOpen)
        {
            _port.Close();
        }

        _port.Dispose();
        await Task.CompletedTask.ConfigureAwait(false);
    }
}
