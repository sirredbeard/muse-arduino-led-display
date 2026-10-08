# Display requests

A display request is the JSON document the assistant generates when you ask for graphics or text on the board. Save it as `<name>.led.json` and run:

```bash
muse-led show <name>.led.json
```

Or drop it into the watched directory of a running `muse-led watch`.

## Schema (version 1)

```jsonc
{
  "version": 1,            // required, must be 1
  "kind": "text",          // "text" | "animation" | "clear"

  // kind=text
  "text": "HELLO",         // required, max 200 chars (5x8 font, A-Z 0-9 punctuation)
  "effect": "scroll",      // "static" | "scroll" | "blink" | "pulse"
  "speedMs": 140,          // 20-5000: ms per scroll step, blink, or pulse step
  "holdMs": 2500,          // 0-60000: ms to hold static text
  "repeat": 1,             // 0 = loop until Ctrl+C

  // kind=animation
  "frameMs": 120,          // 20-5000: ms per frame
  "frames": [              // 1-600 frames; each frame is 8 strings of 13 chars
    [
      ".............",
      ".............",
      ".....###.....",
      "....#...#....",
      "....#####....",
      "....#...#....",
      ".............",
      "............."
    ]
  ],

  // optional for text/animation: RGB LEDs (index 0-3)
  "rgb": [
    { "r": 0, "g": 0, "b": 255 },
    null,
    null,
    null
  ]
}
```

Frame characters: `#`, `X`, `1`, `*` means on. Anything else means off. Rows go top to bottom. 13 characters wide, 8 rows tall. That is the physical layout of the VENTUNO Q matrix.

Effects:

- `static`: text centered, held for `holdMs`.
- `scroll`: marquee entering from the right, exiting left.
- `blink`: on and off at `speedMs`.
- `pulse`: grayscale brightness ramp 0-7 and back. Uses the matrix 3-bit grayscale.

## Tips

The matrix is tiny (13x8) and monochrome blue. Think icons and short words, not detail. Silhouettes read best.

For animation, 4-12 frames at 100-200 ms per frame looks smooth.

`repeat: 0` loops forever. Good for ambient displays. Use it with `watch`.

Keep `speedMs` at 60 or above for scroll. Below that the text is unreadable.

The four RGB LEDs are separate from the matrix. Use them for status color. Blue for idle, red for alert, that kind of thing.

## Validation

`muse-led show` validates the file before displaying anything. It reports exact errors: wrong row length, bad enum, out-of-range timing. The `samples/` directory has known-good examples to copy.
