#!/usr/bin/env bash
# usage: scripts/mac-ssh.sh '<remote command>'   (stdin is forwarded)
exec ssh -i "$HOME/.ssh/id_ed25519_amarel" -o IdentitiesOnly=yes "${BREATHE_MAC:-awsomebroshanks@10.75.97.73}" "$@"
