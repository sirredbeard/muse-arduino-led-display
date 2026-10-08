using System.Buffers;
using System.Text;
using Muse.Arduino.LedDisplay.Display;
using Arduino.Led;

namespace Muse.Arduino.LedDisplay.Transport;

/// <summary>
/// Encodes the line-based wire protocol spoken to the MCU sketch
/// (see protocol/PROTOCOL.md). All rendering stays in .NET; the sketch
/// is a dumb framebuffer sink using Arduino_LED_Matrix.
/// </summary>
public static class Protocol
{
    /// <summary>Protocol version answered by the sketch to PING.</summary>
    public const int Version = 1;

    /// <summary>Maximum accepted line length (bytes, including newline).</summary>
    public const int MaxLineLength = 512;

    private static readonly SearchValues<char> HexDigits =
        SearchValues.Create("0123456789abcdefABCDEF");

    /// <summary>Encodes a 1-bit frame: <c>F1 &lt;26 hex chars&gt;</c>.</summary>
    public static string EncodeFrame1(Frame frame)
    {
        ArgumentNullException.ThrowIfNull(frame);
        return "F1 " + Convert.ToHexString(frame.Bits);
    }

    /// <summary>Encodes a 3-bit grayscale frame: <c>F3 &lt;104 hex chars&gt;</c> (one hex digit per pixel).</summary>
    public static string EncodeFrame3(GrayFrame frame)
    {
        ArgumentNullException.ThrowIfNull(frame);

        // 104 nibbles -> 52 bytes -> 104 hex chars.
        Span<byte> packed = stackalloc byte[LedMatrix.PixelCount / 2];
        ReadOnlySpan<byte> pixels = frame.Pixels;
        for (int i = 0; i < packed.Length; i++)
        {
            packed[i] = (byte)((pixels[i * 2] << 4) | (pixels[i * 2 + 1] & 0x0F));
        }

        return "F3 " + Convert.ToHexString(packed);
    }

    /// <summary>Encodes an RGB LED command: <c>RGB &lt;i&gt; &lt;r&gt; &lt;g&gt; &lt;b&gt;</c>.</summary>
    public static string EncodeRgb(int index, byte r, byte g, byte b)
    {
        if ((uint)index >= LedMatrix.RgbLedCount)
        {
            throw new ArgumentOutOfRangeException(
                nameof(index), $"RGB LED index must be 0-{LedMatrix.RgbLedCount - 1}.");
        }

        return $"RGB {index} {r} {g} {b}";
    }

    /// <summary>Encodes the clear command.</summary>
    public static string EncodeClear() => "CLEAR";

    /// <summary>Encodes the ping command.</summary>
    public static string EncodePing() => "PING";

    /// <summary>Parses a PONG reply, returning the protocol version or null.</summary>
    public static int? TryParsePong(ReadOnlySpan<char> line)
    {
        // Expected: "PONG <version>"
        if (!line.StartsWith("PONG", StringComparison.Ordinal))
        {
            return null;
        }

        ReadOnlySpan<char> rest = line.Slice(4).Trim();
        return int.TryParse(rest, out int version) ? version : null;
    }

    /// <summary>Decodes an F1 payload (26 hex chars) back into a frame.</summary>
    public static Frame DecodeFrame1(ReadOnlySpan<char> hex)
    {
        if (hex.Length != LedMatrix.FrameByteCount * 2 || hex.ContainsAnyExcept(HexDigits))
        {
            throw new ArgumentException("F1 payload must be 26 hex characters.", nameof(hex));
        }

        var frame = new Frame();
        Span<byte> bits = stackalloc byte[LedMatrix.FrameByteCount];
        Convert.FromHexString(hex.ToString(), bits, out _, out _);
        frame.CopyFrom(bits);
        return frame;
    }

    /// <summary>Builds the ASCII line (with newline) to write to the serial port.</summary>
    public static byte[] ToLineBytes(string command)
    {
        byte[] bytes = Encoding.ASCII.GetBytes(command);
        var line = new byte[bytes.Length + 1];
        bytes.CopyTo(line, 0);
        line[^1] = (byte)'\n';
        return line;
    }
}
