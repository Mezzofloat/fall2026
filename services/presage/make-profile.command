#!/bin/bash
# Run from Terminal.app on the Mac (needs the GUI keychain session). Asks Xcode to create/refresh the
# development provisioning profile for com.breatheexe.presage on your Personal Team.
export PATH=/opt/homebrew/bin:$PATH DEVELOPER_DIR=/Applications/Xcode.app/Contents/Developer
cd "$(dirname "$0")/provisioning" || exit 1
LOG=/tmp/breathe-profile.log
{
xcodegen generate --quiet
xcodebuild -project PresageProvisioning.xcodeproj -scheme PresageProvisioning -configuration Debug \
  -derivedDataPath /tmp/breathe-prov -allowProvisioningUpdates build
echo "xcodebuild-exit=$?"
ls -la "$HOME/Library/Developer/Xcode/UserData/Provisioning Profiles" "$HOME/Library/MobileDevice/Provisioning Profiles" 2>&1
} > "$LOG" 2>&1
exit
