using Muse.Arduino.LedDisplay.Display;
using Arduino.Led;

namespace Muse.Arduino.LedDisplay.Transport;

/// <summary>
/// A transport sink for testing without hardware: renders frames as ASCII art
/// to a <see cref="TextWriter"/>. Grayscale frames are dithered to characters.
/// </summary>
public sealed class VirtualLedTransport : ILedTransport
{
    private static readonly char[] Shades = [' ', '.', ':', '-', '=', '+', '*', '#'];

    private readonly TextWriter _output;
    private readonly bool _verbose;
    private bool _disposed;

    /// <summary>Creates a virtual transport writing to <paramref name="output"/>.</summary>
    public VirtualLedTransport(TextWriter output, bool verbose = true)
    {
        _output = output ?? throw new ArgumentNullException(nameof(output));
        _verbose = verbose;
    }

    /// <inheritdoc/>
    public Task ShowFrameAsync(Frame frame, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(frame);
        ObjectDisposedException.ThrowIf(_disposed, this);
        cancellationToken.ThrowIfCancellationRequested();

        if (_verbose)
        {
            _output.WriteLine("--- frame ---");
            _output.WriteLine(frame.ToAscii());
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task ShowGrayFrameAsync(GrayFrame frame, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(frame);
        ObjectDisposedException.ThrowIf(_disposed, this);
        cancellationToken.ThrowIfCancellationRequested();

        if (_verbose)
        {
            _output.WriteLine("--- frame (grayscale) ---");
            for (int y = 0; y < LedMatrix.Height; y++)
            {
                for (int x = 0; x < LedMatrix.Width; x++)
                {
                    _output.Write(Shades[frame.GetPixel(x, y)]);
                }

                _output.WriteLine();
            }
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task SetRgbAsync(int index, byte r, byte g, byte b, CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        cancellationToken.ThrowIfCancellationRequested();

        if (_verbose)
        {
            _output.WriteLine($"[rgb {index}] r={r} g={g} b={b}");
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task ClearAsync(CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        cancellationToken.ThrowIfCancellationRequested();

        if (_verbose)
        {
            _output.WriteLine("[clear]");
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task<bool> PingAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(true);
    }

    /// <inheritdoc/>
    public ValueTask DisposeAsync()
    {
        _disposed = true;
        return ValueTask.CompletedTask;
    }
}
