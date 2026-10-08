using Muse.Gadget.Sdk.Linux;

namespace Muse.Arduino.LedDisplay.Muse;

/// <summary>
/// Reports LED display events to Muse through the installed Muse Gadget SDK,
/// using the <c>Muse.Gadget.Sdk.Linux</c> NuGet package. Reporting never throws:
/// a display keeps working even when Muse is unreachable.
/// </summary>
public sealed class GadgetReporter
{
    /// <summary>Stable side-chat session for display events.</summary>
    public const string SessionId = "led-display";

    private readonly MuseGadgetClient _client;

    /// <summary>Creates a reporter with default SDK paths.</summary>
    public GadgetReporter()
        : this(new MuseGadgetClient())
    {
    }

    /// <summary>Creates a reporter with explicit SDK options.</summary>
    public GadgetReporter(MuseGadgetClientOptions options)
        : this(new MuseGadgetClient(options))
    {
    }

    private GadgetReporter(MuseGadgetClient client)
    {
        _client = client;
    }

    /// <summary>Gets the local SDK status without side effects.</summary>
    public Task<MuseGadgetStatus> GetStatusAsync(CancellationToken cancellationToken) =>
        _client.GetStatusAsync(cancellationToken);

    /// <summary>
    /// Sends a display event to the <c>led-display</c> Muse side chat.
    /// Returns true when the local SDK service accepted the message.
    /// </summary>
    public async Task<bool> ReportAsync(string message, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return false;
        }

        try
        {
            MuseGadgetSendResult result = await _client.SendMessageAsync(
                message, SessionId, cancellationToken).ConfigureAwait(false);
            return result.IsSuccess;
        }
        catch (OperationCanceledException)
        {
            return false;
        }
        catch (ArgumentException)
        {
            return false;
        }
    }

    /// <summary>Formats a status report for humans.</summary>
    public static string FormatStatus(MuseGadgetStatus status) =>
        $"SDK installation: {status.Installation}\n" +
        $"SDK token:        {status.Token}\n" +
        $"Muse service:     {status.Service}" +
        (status.Detail is null ? string.Empty : $"\nDetail: {status.Detail}");
}
