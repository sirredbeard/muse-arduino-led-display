using Muse.Arduino.LedDisplay.Muse;
using Muse.Arduino.LedDisplay.Transport;
using Arduino.Led;

namespace Muse.Arduino.LedDisplay.Display;

/// <summary>
/// Expands a <see cref="DisplayRequest"/> into frames and plays them on a
/// transport. Frame buffers are reused across the loop; no per-frame
/// allocations in the steady state.
/// </summary>
public static class AnimationPlayer
{
    /// <summary>Plays a request to completion (or until <paramref name="cancellationToken"/> fires).</summary>
    public static async Task PlayAsync(
        DisplayRequest request,
        ILedTransport transport,
        GadgetReporter? reporter,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(transport);

        await ApplyRgbAsync(request, transport, cancellationToken).ConfigureAwait(false);

        switch (request.Kind)
        {
            case RequestKind.Clear:
                await transport.ClearAsync(cancellationToken).ConfigureAwait(false);
                await ReportAsync(reporter, "LED matrix cleared.", cancellationToken).ConfigureAwait(false);
                return;

            case RequestKind.Text:
                await PlayTextAsync(request, transport, reporter, cancellationToken).ConfigureAwait(false);
                return;

            case RequestKind.Animation:
                await PlayAnimationAsync(request, transport, reporter, cancellationToken).ConfigureAwait(false);
                return;

            default:
                throw new InvalidOperationException($"Unknown request kind '{request.Kind}'.");
        }
    }

    private static async Task PlayTextAsync(
        DisplayRequest request,
        ILedTransport transport,
        GadgetReporter? reporter,
        CancellationToken cancellationToken)
    {
        string label = $"\"{request.Text}\" ({request.Effect.ToString().ToLowerInvariant()})";
        await ReportAsync(reporter, $"Showing {label} on the LED matrix.", cancellationToken).ConfigureAwait(false);

        int plays = 0;
        while (!cancellationToken.IsCancellationRequested && (request.Repeat == 0 || plays < request.Repeat))
        {
            switch (request.Effect)
            {
                case TextEffect.Static:
                    await transport.ShowFrameAsync(
                        TextRenderer.RenderStatic(request.Text), cancellationToken).ConfigureAwait(false);
                    await Task.Delay(request.HoldMs, cancellationToken).ConfigureAwait(false);
                    break;

                case TextEffect.Scroll:
                    foreach (Frame frame in TextRenderer.RenderScroll(request.Text))
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        await transport.ShowFrameAsync(frame, cancellationToken).ConfigureAwait(false);
                        await Task.Delay(request.SpeedMs, cancellationToken).ConfigureAwait(false);
                    }

                    break;

                case TextEffect.Blink:
                    foreach (Frame frame in TextRenderer.RenderBlink(request.Text, blinks: 3))
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        await transport.ShowFrameAsync(frame, cancellationToken).ConfigureAwait(false);
                        await Task.Delay(request.SpeedMs, cancellationToken).ConfigureAwait(false);
                    }

                    break;

                case TextEffect.Pulse:
                    foreach (GrayFrame frame in TextRenderer.RenderPulse(request.Text, pulses: 2))
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        await transport.ShowGrayFrameAsync(frame, cancellationToken).ConfigureAwait(false);
                        await Task.Delay(request.SpeedMs / 2, cancellationToken).ConfigureAwait(false);
                    }

                    break;

                default:
                    throw new InvalidOperationException($"Unknown text effect '{request.Effect}'.");
            }

            plays++;
        }
    }

    private static async Task PlayAnimationAsync(
        DisplayRequest request,
        ILedTransport transport,
        GadgetReporter? reporter,
        CancellationToken cancellationToken)
    {
        List<GrayFrame> frames = request.Frames!
            .Select(DisplayRequest.FrameFromRows)
            .ToList();

        await ReportAsync(
            reporter,
            $"Playing animation ({frames.Count} frames) on the LED matrix.",
            cancellationToken).ConfigureAwait(false);

        int plays = 0;
        while (!cancellationToken.IsCancellationRequested && (request.Repeat == 0 || plays < request.Repeat))
        {
            foreach (GrayFrame frame in frames)
            {
                cancellationToken.ThrowIfCancellationRequested();
                await transport.ShowGrayFrameAsync(frame, cancellationToken).ConfigureAwait(false);
                await Task.Delay(request.FrameMs, cancellationToken).ConfigureAwait(false);
            }

            plays++;
        }
    }

    private static async Task ApplyRgbAsync(
        DisplayRequest request,
        ILedTransport transport,
        CancellationToken cancellationToken)
    {
        if (request.Rgb is null)
        {
            return;
        }

        for (int i = 0; i < Math.Min(request.Rgb.Count, LedMatrix.RgbLedCount); i++)
        {
            RgbColor? color = request.Rgb[i];
            if (color is not null)
            {
                await transport.SetRgbAsync(i, color.R, color.G, color.B, cancellationToken)
                    .ConfigureAwait(false);
            }
        }
    }

    private static Task ReportAsync(GadgetReporter? reporter, string message, CancellationToken ct) =>
        reporter is null
            ? Task.CompletedTask
            : reporter.ReportAsync(message, ct);
}
