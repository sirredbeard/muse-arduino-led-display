using Muse.Arduino.LedDisplay.Display;
using Muse.Arduino.LedDisplay.Transport;
using Xunit;
using Arduino.Led;

namespace Muse.Arduino.LedDisplay.Tests;

public class ProtocolTests
{
    [Fact]
    public void EncodeFrame1_Produces_26HexChars()
    {
        var frame = new Frame();
        frame.SetPixel(0, 0, true);
        frame.SetPixel(12, 7, true);

        string line = Protocol.EncodeFrame1(frame);
        Assert.StartsWith("F1 ", line);
        Assert.Equal(3 + 26, line.Length);

        Frame decoded = Protocol.DecodeFrame1(line.AsSpan(3));
        Assert.True(decoded.GetPixel(0, 0));
        Assert.True(decoded.GetPixel(12, 7));
        Assert.False(decoded.GetPixel(1, 0));
    }

    [Fact]
    public void EncodeFrame3_Produces_104HexChars_OnePerPixel()
    {
        var gray = new GrayFrame();
        gray.SetPixel(0, 0, 7);
        gray.SetPixel(12, 7, 3);

        string line = Protocol.EncodeFrame3(gray);
        Assert.StartsWith("F3 ", line);
        Assert.Equal(3 + 104, line.Length);

        // First pixel (7) is the first hex digit; last pixel (3) is the last.
        Assert.Equal('7', line[3]);
        Assert.Equal('3', line[^1]);
    }

    [Fact]
    public void EncodeRgb_Formats_Command()
    {
        Assert.Equal("RGB 0 0 0 255", Protocol.EncodeRgb(0, 0, 0, 255));
        Assert.Equal("RGB 3 255 128 0", Protocol.EncodeRgb(3, 255, 128, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => Protocol.EncodeRgb(4, 0, 0, 0));
    }

    [Fact]
    public void TryParsePong_Accepts_Pong1()
    {
        Assert.Equal(1, Protocol.TryParsePong("PONG 1".AsSpan()));
        Assert.Null(Protocol.TryParsePong("PONG".AsSpan()));
        Assert.Null(Protocol.TryParsePong("HELLO".AsSpan()));
    }

    [Fact]
    public void DecodeFrame1_Rejects_BadPayload()
    {
        Assert.Throws<ArgumentException>(() => Protocol.DecodeFrame1("ZZ".AsSpan()));
        Assert.Throws<ArgumentException>(() => Protocol.DecodeFrame1("00".AsSpan()));
    }

    [Fact]
    public void ToLineBytes_Appends_Newline()
    {
        byte[] line = Protocol.ToLineBytes("PING");
        Assert.Equal(new byte[] { (byte)'P', (byte)'I', (byte)'N', (byte)'G', (byte)'\n' }, line);
    }
}
