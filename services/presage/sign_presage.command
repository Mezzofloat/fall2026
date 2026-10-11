#!/bin/bash
# Build + sign the Presage helper app with the Apple Development identity and provisioning profile.
# Run from Terminal.app on the Mac (the signing key lives in the GUI login keychain). Log: /tmp/breathe-sign.log
export PATH=/opt/homebrew/bin:$PATH DEVELOPER_DIR=/Applications/Xcode.app/Contents/Developer
HERE="$(cd "$(dirname "$0")" && pwd)"
BUNDLE_ID=com.breatheexe.presage
LOG=/tmp/breathe-sign.log
{
set -x
cd "$HERE" || exit 1
[ -f build/CMakeCache.txt ] && ! grep -q "$HERE" build/CMakeCache.txt && rm -rf build
mkdir -p build && cd build && cmake .. -DCMAKE_BUILD_TYPE=Release >/dev/null && cmake --build . -j 2>&1 | tail -3
APP="$HERE/build/breathe_presage.app"
[ -d "$APP" ] || { echo "sign-exit=10 (no app bundle)"; exit; }
PROFILE=""
for p in "$HOME/Library/Developer/Xcode/UserData/Provisioning Profiles"/*.provisionprofile "$HOME/Library/MobileDevice/Provisioning Profiles"/*.provisionprofile; do
  [ -f "$p" ] || continue
  if security cms -D -i "$p" 2>/dev/null | grep -q "\.$BUNDLE_ID<"; then PROFILE="$p"; break; fi
done
[ -n "$PROFILE" ] || { echo "sign-exit=11 (no profile for $BUNDLE_ID)"; exit; }
TEAM=$(security cms -D -i "$PROFILE" | plutil -extract Entitlements.com\\.apple\\.developer\\.team-identifier raw -o - -)
APPID=$(security cms -D -i "$PROFILE" | plutil -extract Entitlements.com\\.apple\\.application-identifier raw -o - -)
cp "$PROFILE" "$APP/Contents/embedded.provisionprofile"
cat > "$HERE/build/presage.entitlements" <<ENT
<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0"><dict>
<key>com.apple.application-identifier</key><string>$APPID</string>
<key>com.apple.developer.team-identifier</key><string>$TEAM</string>
<key>keychain-access-groups</key><array><string>$APPID</string></array>
</dict></plist>
ENT
ID=$(security find-identity -v -p codesigning | awk -F'"' '/Apple Development/{print $2; exit}')
codesign --force --sign "$ID" --entitlements "$HERE/build/presage.entitlements" "$APP"
echo "codesign-exit=$?"
codesign --display --entitlements :- "$APP" 2>&1 | head -20
codesign --verify --deep --strict "$APP" && echo VERIFY-OK
ln -sfn breathe_presage.app "$HERE/build/BreathePresage.app"
echo "== camera discovery"
"$APP/Contents/MacOS/breathe_presage" --list-cameras
echo "sign-exit=$?"
} > "$LOG" 2>&1
exit
