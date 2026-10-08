# Agent instructions

Start with [`README.md`](README.md) for the project description, hardware notes,
and the display-request format.

Key references before changing code, protocol, firmware, or docs:

- [`protocol/PROTOCOL.md`](protocol/PROTOCOL.md): the wire protocol between the
  .NET service and the MCU sketch. Keep the C# transport, the Arduino sketch,
  and this document in agreement.
- [`docs/DISPLAY_REQUESTS.md`](docs/DISPLAY_REQUESTS.md): the JSON schema the
  assistant generates when the user asks for graphics or text. Keep the schema,
  `DisplayRequest.cs`, the samples, and this document in agreement.
- [`docs/DEPLOYMENT.md`](docs/DEPLOYMENT.md): how the app is published and
  installed on the VENTUNO Q.

Conventions:

- Target `net11.0`. Nullable and implicit usings are on; warnings are errors.
- Keep the app Native AOT and trim compatible: no reflection-based
  serialization (use the source-generated `JsonSerializerContext`s), no
  dynamic code.
- `src/Muse.Arduino.LedDisplay/Display/Font5x7.cs` is generated from the
  upstream Muse Gadgets SDK font; do not hand-edit glyphs, keep the
  attribution header.
- The `Muse.Gadget.Sdk.Linux` NuGet package is the only Muse integration
  point: status checks and chat messages. Never read or copy SDK tokens or
  pairing credentials.
- Do not add co-author metadata to commits.
