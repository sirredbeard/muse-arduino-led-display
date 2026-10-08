#!/usr/bin/env bash
# Installs muse-led on the VENTUNO Q (or any Linux host).
# Usage: sudo ./install.sh [/path/to/published/muse-led]
set -euo pipefail

SRC="${1:-./publish/muse-led}"
PREFIX="/opt/muse-led-display"
DATA_DIR="/var/lib/muse-led-display/requests"
SERVICE_USER="muse-led"

if [[ $EUID -ne 0 ]]; then
  echo "Run as root: sudo $0" >&2
  exit 1
fi

if [[ ! -x "$SRC" ]]; then
  echo "Cannot find executable '$SRC'. Build the deploy tarball first:" >&2
  echo "  ./deploy/build-tarball.sh linux-arm64" >&2
  exit 1
fi

id -u "$SERVICE_USER" >/dev/null 2>&1 || useradd --system --no-create-home --shell /usr/sbin/nologin "$SERVICE_USER"

install -d -o "$SERVICE_USER" -g "$SERVICE_USER" "$PREFIX" "$DATA_DIR" "$DATA_DIR/done" "$DATA_DIR/error"
install -m 0755 "$SRC" "$PREFIX/muse-led"
# System.IO.Ports needs its native shim next to the binary (.NET 10/11 does
# not ship it in the shared framework). The tarball builder packs it.
SO_SRC="$(dirname "$SRC")/libSystem.IO.Ports.Native.so"
if [[ -f "$SO_SRC" ]]; then
  install -m 0644 "$SO_SRC" "$PREFIX/libSystem.IO.Ports.Native.so"
fi
install -m 0644 "$(dirname "$0")/muse-led-display.service" /etc/systemd/system/muse-led-display.service

systemctl daemon-reload
systemctl enable --now muse-led-display.service
echo "Installed. Drop *.led.json files into $DATA_DIR (or: muse-led show <file>)."
