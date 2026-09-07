#!/usr/bin/env bash
# Builds a universal release binary and assembles build/ClaudeToolbar.app and build/ClaudeToolbar-mac.zip.
set -euo pipefail
cd "$(dirname "$0")/.."

BUILD_DIR="build"
APP="$BUILD_DIR/ClaudeToolbar.app"
ZIP="$BUILD_DIR/ClaudeToolbar-mac.zip"

swift build -c release --arch arm64 --arch x86_64 --product ClaudeToolbar

BIN=".build/apple/Products/Release/ClaudeToolbar"
if [ ! -f "$BIN" ]; then
  BIN="$(find .build -type f -path '*Products/Release/ClaudeToolbar' | head -n 1)"
fi
if [ ! -f "$BIN" ]; then
  echo "built binary not found" >&2
  exit 1
fi

rm -rf "$APP"
mkdir -p "$APP/Contents/MacOS" "$APP/Contents/Resources"
cp "$BIN" "$APP/Contents/MacOS/ClaudeToolbar"
cp Resources/Info.plist "$APP/Contents/Info.plist"
printf 'APPL????' > "$APP/Contents/PkgInfo"
bash scripts/make-icns.sh Resources/AppIcon.png "$APP/Contents/Resources/AppIcon.icns"

codesign --force --deep --sign - "$APP"
codesign --verify --deep --strict "$APP"
plutil -lint "$APP/Contents/Info.plist"
lipo -info "$APP/Contents/MacOS/ClaudeToolbar"

rm -f "$ZIP"
ditto -c -k --keepParent "$APP" "$ZIP"
echo "built $APP and $ZIP"
