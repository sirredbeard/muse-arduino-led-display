using Arduino.Led;
using Muse.Arduino.LedDisplay.Display;

namespace Muse.Arduino.LedDisplay.Transport;

/// <summary>
/// Abstraction over how frames reach the LED board: the real serial link to
/// the MCU sketch, or a virtual sink for testing without hardware.
/// </summary>
public interface ILedTransport : IAsyncDisposable
{
    /// <summary>Shows a 1-bit frame.</summary>
    Task ShowFrameAsync(Frame frame, CancellationToken cancellationToken);

    /// <summary>Shows a 3-bit grayscale frame.</summary>
    Task ShowGrayFrameAsync(GrayFrame frame, CancellationToken cancellationToken);

    /// <summary>Sets one RGB LED (index 0-3), channels 0-255.</summary>
    Task SetRgbAsync(int index, byte r, byte g, byte b, CancellationToken cancellationToken);

    /// <summary>Clears the matrix and turns off the RGB LEDs.</summary>
    Task ClearAsync(CancellationToken cancellationToken);

    /// <summary>Pings the MCU sketch; true when it answers with a PONG.</summary>
    Task<bool> PingAsync(CancellationToken cancellationToken);
}
