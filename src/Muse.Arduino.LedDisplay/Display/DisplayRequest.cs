using System.Text.Json;
using System.Text.Json.Serialization;
using Arduino.Led;

namespace Muse.Arduino.LedDisplay.Display;

/// <summary>What a display request shows.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<RequestKind>))]
public enum RequestKind
{
    /// <summary>Render text with an effect.</summary>
    Text,

    /// <summary>Play explicit animation frames.</summary>
    Animation,

    /// <summary>Clear the matrix and turn off the RGB LEDs.</summary>
    Clear,
}

/// <summary>How text is presented.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<TextEffect>))]
public enum TextEffect
{
    /// <summary>Centered, held for <see cref="DisplayRequest.HoldMs"/>.</summary>
    Static,

    /// <summary>Marquee scrolling right to left.</summary>
    Scroll,

    /// <summary>Blinking on/off.</summary>
    Blink,

    /// <summary>Grayscale brightness pulse.</summary>
    Pulse,
}

/// <summary>An RGB LED color (0-255 per channel).</summary>
public sealed class RgbColor
{
    /// <summary>Red channel, 0-255.</summary>
    public byte R { get; set; }
    /// <summary>Green channel, 0-255.</summary>
    public byte G { get; set; }
    /// <summary>Blue channel, 0-255.</summary>
    public byte B { get; set; }
}

/// <summary>
/// A display request: the JSON document the assistant generates when the user
/// asks for graphics or text on the LED board. See docs/DISPLAY_REQUESTS.md.
/// </summary>
public sealed class DisplayRequest
{
    /// <summary>Schema version; currently 1.</summary>
    public int Version { get; set; } = 1;

    /// <summary>What the request shows.</summary>
    public RequestKind Kind { get; set; } = RequestKind.Text;

    /// <summary>Text to render (kind=text).</summary>
    public string Text { get; set; } = string.Empty;

    /// <summary>Text effect (kind=text).</summary>
    public TextEffect Effect { get; set; } = TextEffect.Scroll;

    /// <summary>Milliseconds per scroll step / blink / pulse step.</summary>
    public int SpeedMs { get; set; } = 140;

    /// <summary>Milliseconds to hold static text.</summary>
    public int HoldMs { get; set; } = 2500;

    /// <summary>
    /// Milliseconds per animation frame (kind=animation).
    /// </summary>
    public int FrameMs { get; set; } = 120;

    /// <summary>
    /// How many times to play; 0 means loop until cancelled.
    /// </summary>
    public int Repeat { get; set; } = 1;

    /// <summary>
    /// Explicit animation frames (kind=animation): each frame is 8 strings of
    /// 13 characters; '#' (or 'X'/'1') = on, anything else = off.
    /// </summary>
    public List<List<string>>? Frames { get; set; }

    /// <summary>
    /// Optional RGB LED colors, up to 4 entries (index 0-3). Null entries are skipped.
    /// </summary>
    public List<RgbColor?>? Rgb { get; set; }

    /// <summary>Validates the request, returning human-readable errors.</summary>
    public List<string> Validate()
    {
        var errors = new List<string>();

        if (Version != 1)
        {
            errors.Add($"Unsupported version {Version}; expected 1.");
        }

        if (SpeedMs is < 20 or > 5000)
        {
            errors.Add("SpeedMs must be between 20 and 5000.");
        }

        if (FrameMs is < 20 or > 5000)
        {
            errors.Add("FrameMs must be between 20 and 5000.");
        }

        if (HoldMs is < 0 or > 60000)
        {
            errors.Add("HoldMs must be between 0 and 60000.");
        }

        if (Repeat < 0)
        {
            errors.Add("Repeat must be 0 or greater.");
        }

        switch (Kind)
        {
            case RequestKind.Text:
                if (string.IsNullOrWhiteSpace(Text))
                {
                    errors.Add("Text must not be empty for kind=text.");
                }

                if (Text.Length > 200)
                {
                    errors.Add("Text must be at most 200 characters.");
                }

                break;

            case RequestKind.Animation:
                if (Frames is null || Frames.Count == 0)
                {
                    errors.Add("Frames must not be empty for kind=animation.");
                }
                else if (Frames.Count > 600)
                {
                    errors.Add("Frames must contain at most 600 frames.");
                }
                else
                {
                    for (int f = 0; f < Frames.Count; f++)
                    {
                        List<string> rows = Frames[f];
                        if (rows.Count != LedMatrix.Height)
                        {
                            errors.Add($"Frame {f} must have {LedMatrix.Height} rows, has {rows.Count}.");
                            continue;
                        }

                        for (int r = 0; r < rows.Count; r++)
                        {
                            if (rows[r].Length != LedMatrix.Width)
                            {
                                errors.Add(
                                    $"Frame {f} row {r} must have {LedMatrix.Width} characters, has {rows[r].Length}.");
                            }
                        }
                    }
                }

                break;

            case RequestKind.Clear:
                break;

            default:
                errors.Add($"Unknown kind '{Kind}'.");
                break;
        }

        if (Rgb is not null && Rgb.Count > LedMatrix.RgbLedCount)
        {
            errors.Add($"Rgb must have at most {LedMatrix.RgbLedCount} entries.");
        }

        return errors;
    }

    /// <summary>Parses and validates a request from JSON text.</summary>
    /// <exception cref="InvalidOperationException">When the JSON is invalid.</exception>
    public static DisplayRequest Parse(string json)
    {
        DisplayRequest? request;
        try
        {
            request = JsonSerializer.Deserialize(json, DisplayRequestJsonContext.Default.DisplayRequest);
        }
        catch (JsonException ex)
        {
            throw new InvalidOperationException($"Invalid display request JSON: {ex.Message}", ex);
        }

        if (request is null)
        {
            throw new InvalidOperationException("Invalid display request JSON: empty document.");
        }

        List<string> errors = request.Validate();
        if (errors.Count > 0)
        {
            throw new InvalidOperationException("Invalid display request: " + string.Join(" ", errors));
        }

        return request;
    }

    /// <summary>Builds a grayscale frame from an animation frame's string rows.</summary>
    public static GrayFrame FrameFromRows(List<string> rows)
    {
        var frame = new GrayFrame();
        for (int y = 0; y < Math.Min(rows.Count, LedMatrix.Height); y++)
        {
            string row = rows[y];
            for (int x = 0; x < Math.Min(row.Length, LedMatrix.Width); x++)
            {
                char c = row[x];
                if (c is '#' or 'X' or '1' or '*')
                {
                    frame.SetPixel(x, y, LedMatrix.MaxBrightness);
                }
            }
        }

        return frame;
    }
}

/// <summary>Source-generated JSON context (keeps the app Native AOT friendly).</summary>
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(DisplayRequest))]
internal partial class DisplayRequestJsonContext : JsonSerializerContext
{
}
