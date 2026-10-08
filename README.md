# muse-arduino-led-display

`muse-led`: a .NET 11 service that drives the 8x13 LED matrix on the Arduino VENTUNO Q. You ask for text or graphics, it puts them on the board.

Rendering comes from the [Arduino.Led](https://github.com/sirredbeard/arduino-led-nuget) NuGet package: framebuffers, a 5x8 font, text effects, modeled on Arduino's `Arduino_LED_Matrix` library. Muse integration comes from [Muse.Gadget.Sdk.Linux](https://github.com/sirredbeard/muse-gadget-sdk-nuget). The service publishes display events to the `led-display` Muse side chat, and Muse can run the CLI through the SDK's `system.run`.

## Contents

- [How it works](#how-it-works)
- [Commands](#commands)
- [The "ask me" flow](#the-ask-me-flow)
- [Build and install](#build-and-install)
- [Layout](#layout)
- [License](#license)

## How it works

The VENTUNO Q has 104 blue LEDs wired to the STM32H5 MCU. A small sketch (`firmware/VentunoQLedMatrix/`) runs on the MCU as a framebuffer sink. It takes frames over USB serial and draws them with Arduino's `Arduino_LED_Matrix` library. See the [VENTUNO Q LED matrix guide](https://docs.arduino.cc/tutorials/ventuno-q/led-matrix/).

The sketch listens on `SerialUSB`. That is the raw USB link. `Serial` is virtualized through RouterBridge on this board and does not reach the sketch.

Everything else is .NET, on the Linux side.

- `Arduino.Led` builds the frames. Font rasterization. Scroll, blink, and pulse effects. Animation sequencing.
- `muse-led` plays them. It reads display-request JSON, paces the frames, and pushes them over serial. See `protocol/PROTOCOL.md`.
- The matrix does 8 grayscale levels per LED. Pulse is a real brightness ramp, not blinking.

```
you -> assistant -> request JSON -> muse-led -> serial -> MCU sketch -> LED matrix
                                              |
                                              +-> Muse chat ("Showing 'HELLO'")
```

## Commands

```
$ muse-led show <request.json>   # play a display request (docs/DISPLAY_REQUESTS.md)
$ muse-led text "HELLO"          # render text (--effect scroll|static|blink|pulse,
                                #   --speed-ms N, --hold-ms N, --repeat N)
$ muse-led clear                 # clear the matrix and RGB LEDs
$ muse-led status [--report] [--json]
                                # check the Muse Gadget SDK
$ muse-led watch <dir>           # long-running: display every *.led.json dropped in <dir>
$ muse-led send "message"        # send a message to the led-display Muse side chat
```

Global options: `--port <dev>` (default `/dev/ttyACM0`), `--virtual` (ASCII art to console, no hardware), `--no-report` (skip Muse reporting), `--json` (machine-readable `status` output for `system.run`).

## The "ask me" flow

1. You ask your assistant for something. "Scrolling 'GO AUBREY', blue RGB glow."
2. The assistant writes a display-request JSON per `docs/DISPLAY_REQUESTS.md`. Or you copy one from `samples/`.
3. Run `muse-led show request.json` on the board. Or drop the file in the `muse-led watch` directory. The service picks it up, displays it, archives it to `done/`, and tells Muse.

Muse can drive the board too. The CLI is narrow and loopback-safe. The gadget's Muse calls it via `system.run`. `muse-led text "DINNER AT 7" --effect scroll --json` is the whole trick.

## Build and install

You need the .NET 11 SDK. `Arduino.Led` and `Muse.Gadget.Sdk.Linux` are on GitHub Packages. That needs a classic PAT with `read:packages`, even for public packages.

```bash
$ dotnet nuget add source https://nuget.pkg.github.com/sirredbeard/index.json \
    --name sirredbeard-github \
    --username <github-user> \
    --password <github-token> \
    --store-password-in-clear-text
```

Do not put the token in a repo. It lives in your user NuGet config.

```bash
$ dotnet build Muse.Arduino.LedDisplay.slnx
$ ./deploy/build-tarball.sh linux-arm64
```

That makes `muse-led-deploy-linux-arm64.tar.gz`. It has the self-contained trimmed `muse-led` binary, the serial port native library, the install script, and the systemd unit. See `docs/DEPLOYMENT.md` for flashing the MCU sketch, installing, and App Lab.

## Layout

- `src/Muse.Arduino.LedDisplay` - the `muse-led` service. CLI, display requests, serial transport, Muse reporting.
- `firmware/VentunoQLedMatrix` - MCU sketch. Serial framebuffer sink using `Arduino_LED_Matrix`.
- `protocol/PROTOCOL.md` - wire protocol between service and sketch.
- `docs/DISPLAY_REQUESTS.md` - JSON schema the assistant generates.
- `docs/DEPLOYMENT.md` - publish, install, systemd, App Lab flashing.
- `samples/` - known-good display requests.
- `deploy/` - `install.sh` and systemd unit.
- `tests/` - protocol, schema, and player tests. No hardware needed.

## License

[MIT](LICENSE)
