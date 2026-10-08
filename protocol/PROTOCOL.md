# Wire protocol: .NET service to MCU sketch

Version 1. Line-based ASCII over USB serial. 115200 baud, 8N1. Every command is one line ending in `\n`. Lines over 512 bytes are dropped.

On the sketch side this is `SerialUSB`. That is the raw USB link. `Serial` is virtualized through RouterBridge and does not reach the sketch. On the Linux side it is normally `/dev/ttyACM0`.

The sketch (`firmware/VentunoQLedMatrix/VentunoQLedMatrix.ino`) keeps a 104-byte framebuffer. Row-major, 8 rows by 13 columns, brightness 0-7. It renders with the official `Arduino_LED_Matrix` library (`setGrayscaleBits(3)` plus `draw()`). See https://docs.arduino.cc/tutorials/ventuno-q/led-matrix/.

## Commands (.NET to sketch)

| Command | Format | What it does |
|---|---|---|
| `PING` | `PING\n` | Health check. |
| `F1` | `F1 <26 hex chars>\n` | 1-bit frame. 13 packed bytes, row-major. Bit (y*13+x) is pixel (x, y). On means full brightness. Fast path for text and effects. |
| `F3` | `F3 <104 hex chars>\n` | Grayscale frame. One hex digit per pixel (0-7), row-major. For pulse, fade, and animations. |
| `RGB` | `RGB <i> <r> <g> <b>\n` | Set RGB LED `i` (0-3). Channels 0-255. Over 127 counts as on. LEDs are active-low. |
| `CLEAR` | `CLEAR\n` | Clear the matrix. Turn off all RGB LEDs. |

All pixels on, 1-bit:

```
F1 FFFFFFFFFFFF... (26 F's)
```

RGB LED 0 blue:

```
RGB 0 0 0 255
```

## Replies (sketch to .NET)

| Reply | What it means |
|---|---|
| `PONG 1\n` | Answer to `PING`. The number is the protocol version. |

The sketch sends nothing else. Unknown or malformed lines are ignored silently. A stray boot log line cannot wedge the parser.

## Throughput

At 115200 baud a 1-bit frame is about 30 bytes on the wire (2.6 ms). About 300 fps is theoretically possible. The .NET player paces frames with `Task.Delay` per the request's `speedMs` or `frameMs`. Grayscale frames are about 108 bytes (9.4 ms). Good for about 100 fps.
