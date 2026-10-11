#!/usr/bin/env bash
# Sync the monorepo (edited on Linux) to the Mac working copy where Unity/camera/mic run.
# Unity owns *.meta GUIDs, so pull metas back first and never delete them on the Mac.
set -euo pipefail
MAC=${BREATHE_MAC:-awsomebroshanks@10.75.97.73}
DEST=${BREATHE_MAC_DIR:-/Users/awsomebroshanks/Documents/Codex/breathe}
SSH="ssh -i $HOME/.ssh/id_ed25519_amarel -o IdentitiesOnly=yes"
cd "$(dirname "$0")/.."
$SSH $MAC "mkdir -p $DEST"
# 1) pull Unity-generated .meta files
rsync -a -e "$SSH" --include='*/' --include='*.meta' --exclude='*' "$MAC:$DEST/game/Assets/" game/Assets/
# 2) push sources (no Library/Builds/secrets); keep Mac-only metas
rsync -a -e "$SSH" --exclude=Library --exclude=Builds --exclude=Logs --exclude=Temp --exclude=UserSettings \
  --exclude=obj --exclude='*.csproj' --exclude='*.slnx' --exclude=.git --exclude=.DS_Store --exclude=__pycache__ \
  --exclude='services/presage/build' --exclude=firmware --exclude=reciever-esp32 --exclude=dumb \
  --filter='P *.meta' ./game ./services ./hub ./docs ./scripts ./AGENTS.md ./CLAUDE.md "$MAC:$DEST/"
echo "synced to $MAC:$DEST"
