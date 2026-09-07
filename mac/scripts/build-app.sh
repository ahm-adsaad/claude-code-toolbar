#!/usr/bin/env bash
# Builds a universal release binary and assembles build/ClaudeToolbar.app and build/ClaudeToolbar-mac.zip.
set -euo pipefail
cd "$(dirname "$0")/.."

BUILD_DIR="build"
APP="$BUILD_DIR/ClaudeToolbar.app"
ZIP="$BUILD_DIR/ClaudeToolbar-mac.zip"

# Built as two separate single-arch release builds (native SwiftPM build
# system) and merged with lipo, rather than `swift build --arch arm64 --arch
# x86_64` in one invocation: the combined-arch form routes through the
# XCBuild backend, which fails to translate the package's swiftLanguageMode
# setting (`SWIFT_VERSION '' is unsupported`).
swift build -c release --arch arm64 --product ClaudeToolbar
swift build -c release --arch x86_64 --product ClaudeToolbar

ARM_BIN=".build/arm64-apple-macosx/release/ClaudeToolbar"
X86_BIN=".build/x86_64-apple-macosx/release/ClaudeToolbar"
if [ ! -f "$ARM_BIN" ]; then
  ARM_BIN="$(find .build -type f -path '*arm64-apple-macosx/release/ClaudeToolbar' -print -quit)"
fi
if [ ! -f "$X86_BIN" ]; then
  X86_BIN="$(find .build -type f -path '*x86_64-apple-macosx/release/ClaudeToolbar' -print -quit)"
fi
if [ ! -f "$ARM_BIN" ] || [ ! -f "$X86_BIN" ]; then
  echo "built binary not found" >&2
  exit 1
fi

mkdir -p ".build/apple/Products/Release"
BIN=".build/apple/Products/Release/ClaudeToolbar"
lipo -create -output "$BIN" "$ARM_BIN" "$X86_BIN"

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
ARCHS="$(lipo -archs "$APP/Contents/MacOS/ClaudeToolbar")"
case "$ARCHS" in
  *arm64*x86_64*|*x86_64*arm64*) ;;
  *) echo "expected a universal binary, got: $ARCHS" >&2; exit 1 ;;
esac

rm -f "$ZIP"
ditto -c -k --keepParent "$APP" "$ZIP"
echo "built $APP and $ZIP"
