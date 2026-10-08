# Deployment

## 1. Flash the MCU sketch

In Arduino App Lab, create a new App. Paste `firmware/VentunoQLedMatrix/VentunoQLedMatrix.ino` into the sketch part. It needs the `Arduino_LED_Matrix` library. See the [LED matrix guide](https://docs.arduino.cc/tutorials/ventuno-q/led-matrix/). Run it to deploy to the STM32H5 MCU.

Notes:

- Flashing replaces the factory MCU firmware. That includes the boot animation shown during Linux startup. Keep a copy of the factory sketch if you want to restore it later.
- The sketch listens on `SerialUSB`. That is the raw USB link. On the Linux side it normally appears as `/dev/ttyACM0`. If yours differs, pass `--port /dev/ttyXXX`. Or set it in the systemd unit's `ExecStart`.
- Verify the link: `muse-led clear --port /dev/ttyACM0` should blank the matrix. `muse-led text "HI" --virtual` renders ASCII art without hardware.

## 2. Build the deploy tarball

On any machine with the .NET 11 SDK. Do the one-time GitHub Packages auth first (see README).

```bash
./deploy/build-tarball.sh linux-arm64
```

That publishes a self-contained trimmed `muse-led` for the board. It packs the binary with `libSystem.IO.Ports.Native.so`. That is the serial port native shim. .NET 10 and 11 do not ship it in the shared framework. Without it every serial open fails. It also packs `deploy/install.sh` and the systemd unit.

For a NativeAOT binary instead, set `PUBLISH_AOT=1`. That needs the target's native toolchain, so run it on the VENTUNO Q itself. For x64 test machines: `./deploy/build-tarball.sh linux-x64`.

## 3. Install on the VENTUNO Q

```bash
scp muse-led-deploy-linux-arm64.tar.gz <user>@<ventuno-q>:~/
ssh <user>@<ventuno-q>
tar xzf muse-led-deploy-linux-arm64.tar.gz
cd ~ && sudo ./deploy/install.sh ./muse-led
```

`install.sh` puts the binary in `/opt/muse-led-display/`. It creates the `muse-led` system user (in the `dialout` group for serial access). It sets up `/var/lib/muse-led-display/requests` (with `done/` and `error/`). It enables the `muse-led-display.service` systemd unit, which runs:

```
muse-led watch /var/lib/muse-led-display/requests
```

Check it: `systemctl status muse-led-display`, `journalctl -u muse-led-display -f`.

## 4. Muse Gadget SDK wiring

The service uses the installed Muse Gadget SDK on the board for two things.

- `muse-led status` reports SDK installation, token, and service state. Add `--report` to publish the status to the `led-display` side chat.
- `show`, `text`, and `watch` publish "Showing ..." events to that side chat. Disable with `--no-report`.

The service account needs read access to `/run/musegadget/musegadget.sock`. If the socket is root-only, run the service as root or add a group. Reporting degrades gracefully when Muse is unreachable. The display keeps working.

### Letting Muse drive the board via system.run

The CLI is safe for the SDK's `system.run`. It binds no network ports. It writes only to the serial device and the watch directory. It validates every argument. Example prompts for the gadget's Muse:

- "Run `muse-led text 'DINNER AT 7' --effect scroll --json`"
- "Run `muse-led status --json` and tell me if the SDK is healthy"

## Updating

Republish. Copy the new binary over `/opt/muse-led-display/muse-led`. Then `sudo systemctl restart muse-led-display`.
