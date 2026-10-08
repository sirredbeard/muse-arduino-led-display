#!/usr/bin/env bash
# Builds the muse-led deploy tarball for a Linux RID.
#
# The tarball contains the published muse-led binary, the RID-specific
# libSystem.IO.Ports.Native.so it needs at runtime (.NET 10/11 does not ship
# it in the shared framework, so a bare binary fails to open the serial port),
# the install script, and the systemd unit.
#
# Usage: ./deploy/build-tarball.sh [rid] [output]
#   rid defaults to linux-arm64. Set PUBLISH_AOT=1 for a NativeAOT binary
#   (needs the target's native toolchain; building on the VENTUNO Q itself works).
set -euo pipefail

RID="${1:-linux-arm64}"
OUT="${2:-muse-led-deploy-${RID}.tar.gz}"
ROOT="$(cd "$(dirname "$0")/.." && pwd)"
STAGE="$(mktemp -d)"
trap 'rm -rf "$STAGE"' EXIT

PUBLISH_ARGS=(-c Release -r "$RID" --self-contained -p:PublishTrimmed=true -p:PublishSingleFile=true)
if [[ "${PUBLISH_AOT:-0}" == "1" ]]; then
  PUBLISH_ARGS+=(-p:PublishAot=true)
fi

dotnet publish "$ROOT/src/Muse.Arduino.LedDisplay/Muse.Arduino.LedDisplay.csproj" \
  "${PUBLISH_ARGS[@]}" -o "$STAGE/publish"

# System.IO.Ports needs its native shim next to the binary at runtime.
PKGROOT="$(dotnet nuget locals global-packages --list | sed 's/^global-packages: //')"
SO="$(ls "$PKGROOT/runtime.${RID}.runtime.native.system.io.ports/"*/"runtimes/${RID}/native/libSystem.IO.Ports.Native.so" 2>/dev/null | sort -V | tail -n 1 || true)"
if [[ -z "$SO" ]]; then
  echo "error: no libSystem.IO.Ports.Native.so for RID '$RID' under $PKGROOT" >&2
  echo "hint: restore the project for that RID first (dotnet restore -r $RID)" >&2
  exit 1
fi

mkdir -p "$STAGE/tarball/deploy"
cp "$STAGE/publish/muse-led" "$STAGE/tarball/muse-led"
cp "$SO" "$STAGE/tarball/libSystem.IO.Ports.Native.so"
cp "$ROOT/deploy/install.sh" "$ROOT/deploy/muse-led-display.service" "$STAGE/tarball/deploy/"
chmod +x "$STAGE/tarball/deploy/install.sh"

tar czf "$OUT" -C "$STAGE/tarball" muse-led libSystem.IO.Ports.Native.so deploy
echo "Wrote $OUT:"
tar tzvf "$OUT"
