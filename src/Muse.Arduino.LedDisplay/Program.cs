using System.Text.Json;
using Muse.Arduino.LedDisplay.Display;
using Muse.Arduino.LedDisplay.Muse;
using Muse.Arduino.LedDisplay.Transport;

namespace Muse.Arduino.LedDisplay;

/// <summary>
/// muse-led: drive the VENTUNO Q 8x13 LED matrix from .NET 11.
///
///   muse-led show &lt;request.json&gt;   Play a display request file.
///   muse-led text "HELLO"            Render text with an effect.
///   muse-led clear                   Clear the matrix and RGB LEDs.
///   muse-led status                  Check the Muse Gadget SDK state.
///   muse-led watch &lt;dir&gt;             Watch a directory for *.led.json requests.
///   muse-led send "message"          Send a message to the Muse chat.
///
/// Global options: --port &lt;dev&gt; (default /dev/ttyACM0), --virtual (no hardware),
/// --no-report (do not publish events to Muse).
/// </summary>
internal static class Program
{
    private const int ExitOk = 0;
    private const int ExitUsage = 2;
    private const int ExitRuntime = 3;

    private const string DefaultPort = "/dev/ttyACM0";

    internal static async Task<int> Main(string[] args)
    {
        using var cts = new CancellationTokenSource();
        Console.CancelKeyPress += (_, e) =>
        {
            e.Cancel = true;
            cts.Cancel();
        };

        try
        {
            return await RunAsync(args, cts.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            return ExitOk;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"error: {ex.Message}");
            return ExitRuntime;
        }
    }

    private static async Task<int> RunAsync(string[] args, CancellationToken ct)
    {
        var options = Options.Parse(args);
        if (options is null)
        {
            PrintUsage();
            return ExitUsage;
        }

        await using ILedTransport? transport = options.Command switch
        {
            "show" or "text" or "clear" => CreateTransport(options),
            _ => null,
        };
        var reporter = options.NoReport ? null : new GadgetReporter();

        return options.Command switch
        {
            "show" => await ShowAsync(options, transport!, reporter, ct).ConfigureAwait(false),
            "text" => await TextAsync(options, transport!, reporter, ct).ConfigureAwait(false),
            "clear" => await ClearAsync(transport!, reporter, ct).ConfigureAwait(false),
            "status" => await StatusAsync(options, reporter, ct).ConfigureAwait(false),
            "watch" => await WatchAsync(options, reporter, ct).ConfigureAwait(false),
            "send" => await SendAsync(options, reporter, ct).ConfigureAwait(false),
            _ => Usage($"unknown command '{options.Command}'"),
        };
    }

    private static ILedTransport CreateTransport(Options options)
    {
        if (options.Virtual)
        {
            return new VirtualLedTransport(Console.Out);
        }

        var serial = new SerialLedTransport(options.Port);
        serial.Open();
        return serial;
    }

    private static async Task<int> ShowAsync(
        Options options, ILedTransport transport, GadgetReporter? reporter, CancellationToken ct)
    {
        if (options.Args.Count < 1)
        {
            return Usage("show needs a request file: muse-led show <request.json>");
        }

        string path = options.Args[0];
        string json;
        try
        {
            json = await File.ReadAllTextAsync(path, ct).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Console.Error.WriteLine($"error: cannot read '{path}': {ex.Message}");
            return ExitRuntime;
        }

        DisplayRequest request;
        try
        {
            request = DisplayRequest.Parse(json);
        }
        catch (InvalidOperationException ex)
        {
            Console.Error.WriteLine($"error: {ex.Message}");
            return ExitUsage;
        }

        Console.WriteLine($"Showing '{Path.GetFileName(path)}' ({request.Kind}).");
        await AnimationPlayer.PlayAsync(request, transport, reporter, ct).ConfigureAwait(false);
        return ExitOk;
    }

    private static async Task<int> TextAsync(
        Options options, ILedTransport transport, GadgetReporter? reporter, CancellationToken ct)
    {
        if (options.Args.Count < 1)
        {
            return Usage("text needs a string: muse-led text \"HELLO\"");
        }

        var request = new DisplayRequest
        {
            Kind = RequestKind.Text,
            Text = options.Args[0],
            Effect = options.Effect,
            SpeedMs = options.SpeedMs,
            HoldMs = options.HoldMs,
            Repeat = options.Repeat,
        };

        List<string> errors = request.Validate();
        if (errors.Count > 0)
        {
            Console.Error.WriteLine("error: " + string.Join(" ", errors));
            return ExitUsage;
        }

        await AnimationPlayer.PlayAsync(request, transport, reporter, ct).ConfigureAwait(false);
        return ExitOk;
    }

    private static async Task<int> ClearAsync(
        ILedTransport transport, GadgetReporter? reporter, CancellationToken ct)
    {
        await transport.ClearAsync(ct).ConfigureAwait(false);
        Console.WriteLine("Matrix cleared.");
        if (reporter is not null)
        {
            await reporter.ReportAsync("LED matrix cleared.", ct).ConfigureAwait(false);
        }

        return ExitOk;
    }

    private static async Task<int> StatusAsync(Options options, GadgetReporter? reporter, CancellationToken ct)
    {
        var probe = reporter ?? new GadgetReporter();
        var status = await probe.GetStatusAsync(ct).ConfigureAwait(false);

        if (options.Json)
        {
            Console.WriteLine(JsonSerializer.Serialize(
                new { installation = status.Installation.ToString(), token = status.Token.ToString(), service = status.Service.ToString(), detail = status.Detail },
                StatusJsonContext.Default.StatusDto));
        }
        else
        {
            Console.WriteLine(GadgetReporter.FormatStatus(status));
        }

        if (options.Report && reporter is not null)
        {
            bool sent = await reporter.ReportAsync(
                "LED display status check: " + GadgetReporter.FormatStatus(status).Replace('\n', ' '),
                ct).ConfigureAwait(false);
            Console.WriteLine(sent ? "Status reported to Muse." : "Could not report status to Muse.");
        }

        return ExitOk;
    }

    private static async Task<int> WatchAsync(Options options, GadgetReporter? reporter, CancellationToken ct)
    {
        if (options.Args.Count < 1)
        {
            return Usage("watch needs a directory: muse-led watch <dir>");
        }

        string dir = options.Args[0];
        string doneDir = Path.Combine(dir, "done");
        string errorDir = Path.Combine(dir, "error");
        Directory.CreateDirectory(dir);
        Directory.CreateDirectory(doneDir);
        Directory.CreateDirectory(errorDir);

        Console.WriteLine($"Watching '{dir}' for *.led.json (Ctrl+C to stop).");

        using var watcher = new FileSystemWatcher(dir, "*.led.json")
        {
            NotifyFilter = NotifyFilters.FileName | NotifyFilters.CreationTime,
            EnableRaisingEvents = true,
        };

        var queue = new System.Collections.Concurrent.BlockingCollection<string>();

        watcher.Created += (_, e) => queue.Add(e.FullPath);
        watcher.Renamed += (_, e) => queue.Add(e.FullPath);

        // Pick up files that arrived while we were down.
        foreach (string existing in Directory.GetFiles(dir, "*.led.json"))
        {
            queue.Add(existing);
        }

        try
        {
            foreach (string path in queue.GetConsumingEnumerable(ct))
            {
                await HandleWatchFileAsync(path, doneDir, errorDir, options, reporter, ct)
                    .ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException)
        {
            // Ctrl+C: fall through.
        }

        Console.WriteLine("Watcher stopped.");
        return ExitOk;
    }

    private static async Task HandleWatchFileAsync(
        string path, string doneDir, string errorDir, Options options,
        GadgetReporter? reporter, CancellationToken ct)
    {
        string name = Path.GetFileName(path);

        // Wait for the writer to finish (retry briefly on sharing violations).
        string? json = null;
        for (int i = 0; i < 20 && json is null; i++)
        {
            try
            {
                json = await File.ReadAllTextAsync(path, ct).ConfigureAwait(false);
            }
            catch (IOException)
            {
                await Task.Delay(100, ct).ConfigureAwait(false);
            }
        }

        string targetDir = doneDir;
        try
        {
            if (json is null)
            {
                throw new InvalidOperationException($"Could not read '{name}'.");
            }

            DisplayRequest request = DisplayRequest.Parse(json);
            Console.WriteLine($"[{DateTimeOffset.Now:HH:mm:ss}] Showing '{name}' ({request.Kind}).");

            await using ILedTransport transport = CreateTransport(options);
            await AnimationPlayer.PlayAsync(request, transport, reporter, ct).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            Console.Error.WriteLine($"[{name}] failed: {ex.Message}");
            targetDir = errorDir;
        }

        try
        {
            string target = Path.Combine(targetDir, name);
            if (File.Exists(target))
            {
                File.Delete(target);
            }

            File.Move(path, target);
        }
        catch (IOException ex)
        {
            Console.Error.WriteLine($"Could not archive '{name}': {ex.Message}");
        }
    }

    private static async Task<int> SendAsync(Options options, GadgetReporter? reporter, CancellationToken ct)
    {
        if (options.Args.Count < 1)
        {
            return Usage("send needs a message: muse-led send \"hello\"");
        }

        var sender = reporter ?? new GadgetReporter();
        bool sent = await sender.ReportAsync(options.Args[0], ct).ConfigureAwait(false);
        Console.WriteLine(sent ? "Message sent to the led-display side chat."
            : "The Muse did not accept the message (see status).");
        return sent ? ExitOk : ExitRuntime;
    }

    private static int Usage(string message)
    {
        Console.Error.WriteLine($"error: {message}");
        Console.Error.WriteLine();
        PrintUsage();
        return ExitUsage;
    }

    private static void PrintUsage()
    {
        Console.Error.WriteLine(
            """
            usage: muse-led [options] <command> [args]

            commands:
              show <request.json>   Play a display request file (see docs/DISPLAY_REQUESTS.md).
              text "HELLO"           Render text. Options: --effect scroll|static|blink|pulse,
                                    --speed-ms N, --hold-ms N, --repeat N.
              clear                 Clear the matrix and turn off the RGB LEDs.
              status [--report] [--json]
                                    Check the Muse Gadget SDK state.
              watch <dir>           Watch a directory for *.led.json requests.
              send "message"        Send a message to the led-display Muse side chat.

            options:
              --port <dev>    Serial port for the MCU link (default /dev/ttyACM0).
              --virtual       Render to the console instead of hardware.
              --no-report     Do not publish display events to Muse.
              --json          Machine-readable output (status).
            """);
    }

    private sealed class Options
    {
        public string Command { get; private set; } = string.Empty;
        public List<string> Args { get; } = new();
        public string Port { get; private set; } = DefaultPort;
        public bool Virtual { get; private set; }
        public bool NoReport { get; private set; }
        public bool Json { get; private set; }
        public bool Report { get; private set; }
        public TextEffect Effect { get; private set; } = TextEffect.Scroll;
        public int SpeedMs { get; private set; } = 140;
        public int HoldMs { get; private set; } = 2500;
        public int Repeat { get; private set; } = 1;

        public static Options? Parse(string[] args)
        {
            var options = new Options();

            for (int i = 0; i < args.Length; i++)
            {
                string arg = args[i];
                if (arg.StartsWith("--", StringComparison.Ordinal))
                {
                    switch (arg)
                    {
                        case "--port" when i + 1 < args.Length:
                            options.Port = args[++i];
                            break;
                        case "--virtual":
                            options.Virtual = true;
                            break;
                        case "--no-report":
                            options.NoReport = true;
                            break;
                        case "--json":
                            options.Json = true;
                            break;
                        case "--report":
                            options.Report = true;
                            break;
                        case "--effect" when i + 1 < args.Length:
                            if (!Enum.TryParse<TextEffect>(args[++i], ignoreCase: true, out var effect))
                            {
                                return null;
                            }

                            options.Effect = effect;
                            break;
                        case "--speed-ms" when i + 1 < args.Length && int.TryParse(args[++i], out int speed):
                            options.SpeedMs = speed;
                            break;
                        case "--hold-ms" when i + 1 < args.Length && int.TryParse(args[++i], out int hold):
                            options.HoldMs = hold;
                            break;
                        case "--repeat" when i + 1 < args.Length && int.TryParse(args[++i], out int repeat):
                            options.Repeat = repeat;
                            break;
                        default:
                            return null;
                    }
                }
                else if (options.Command.Length == 0)
                {
                    options.Command = arg;
                }
                else
                {
                    options.Args.Add(arg);
                }
            }

            return options.Command.Length == 0 ? null : options;
        }
    }

    internal sealed record StatusDto(string Installation, string Token, string Service, string? Detail);
}

/// <summary>Source-generated JSON context for machine-readable status output.</summary>
[System.Text.Json.Serialization.JsonSerializable(typeof(Program.StatusDto))]
internal partial class StatusJsonContext : System.Text.Json.Serialization.JsonSerializerContext
{
}
