#!/usr/bin/env bash
set -euo pipefail

app_dir="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"
binary="$app_dir/ExpeditionInfinity"
icon_src="$app_dir/expedition-infinity.png"
data_home="${XDG_DATA_HOME:-$HOME/.local/share}"
desktop_dir="$data_home/applications"
icon_dir="$data_home/icons"
desktop="$desktop_dir/expedition-infinity.desktop"
icon="$icon_dir/expedition-infinity.png"

if [[ ! -x "$binary" ]]; then
  echo "error: executable not found: $binary" >&2
  exit 1
fi
if [[ ! -f "$icon_src" ]]; then
  echo "error: icon not found: $icon_src" >&2
  exit 1
fi

mkdir -p "$desktop_dir" "$icon_dir"
cp -f "$icon_src" "$icon"

cat > "$desktop" <<EOF
[Desktop Entry]
Type=Application
Version=1.0
Name=Expedition Infinity Seed Generator
GenericName=Expedition 33 Seed Generator
Comment=Build Expedition Infinity seed packs
Exec="$binary"
Path=$app_dir
Icon=$icon
Terminal=false
Categories=Game;
Keywords=Expedition 33;Expedition Infinity;seed;mod;
StartupWMClass=ExpeditionInfinity
SingleMainWindow=true
EOF
chmod +x "$desktop"

if command -v update-desktop-database >/dev/null 2>&1; then
  update-desktop-database "$desktop_dir" >/dev/null 2>&1 || true
fi

echo "Installed desktop entry: $desktop"
echo "Installed icon: $icon"
