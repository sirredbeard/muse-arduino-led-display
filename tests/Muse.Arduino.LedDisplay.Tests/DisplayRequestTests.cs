using Muse.Arduino.LedDisplay.Display;
using Xunit;
using Arduino.Led;

namespace Muse.Arduino.LedDisplay.Tests;

public class DisplayRequestTests
{
    [Theory]
    [InlineData("hello-scroll.json")]
    [InlineData("wave.json")]
    [InlineData("pulse-rgb.json")]
    public void Samples_Parse_And_Validate(string fileName)
    {
        string path = Path.Combine(AppContext.BaseDirectory, "samples", fileName);
        string json = File.ReadAllText(path);

        DisplayRequest request = DisplayRequest.Parse(json);

        Assert.Equal(1, request.Version);
        Assert.Empty(request.Validate());
    }

    [Fact]
    public void Parse_Rejects_BadFrameDimensions()
    {
        const string json = """
            {"version":1,"kind":"animation","frameMs":100,"repeat":1,
             "frames":[["too short"]]}
            """;

        var ex = Assert.Throws<InvalidOperationException>(() => DisplayRequest.Parse(json));
        Assert.Contains("8 rows", ex.Message);
    }

    [Fact]
    public void Parse_Rejects_UnknownKind()
    {
        const string json = """{"version":1,"kind":"hologram"}""";
        Assert.Throws<InvalidOperationException>(() => DisplayRequest.Parse(json));
    }

    [Fact]
    public void Parse_Rejects_MalformedJson()
    {
        Assert.Throws<InvalidOperationException>(() => DisplayRequest.Parse("{nope"));
    }

    [Fact]
    public void FrameFromRows_Maps_HashToOn()
    {
        var rows = new List<string>
        {
            "#............",
            ".............",
            ".............",
            ".............",
            ".............",
            ".............",
            ".............",
            "............#",
        };

        GrayFrame frame = DisplayRequest.FrameFromRows(rows);
        Assert.Equal(7, frame.GetPixel(0, 0));
        Assert.Equal(7, frame.GetPixel(12, 7));
        Assert.Equal(0, frame.GetPixel(1, 0));
    }

    [Fact]
    public void Validate_Rejects_OutOfRangeTiming()
    {
        var request = new DisplayRequest
        {
            Kind = RequestKind.Text,
            Text = "HI",
            SpeedMs = 5,
        };

        Assert.Contains(request.Validate(), e => e.Contains("SpeedMs"));
    }
}
