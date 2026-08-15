#!/bin/bash
# Double-clickable launcher for the Wend Hill Prologue standalone build.
#
# Exists because the build is ad-hoc signed, not notarized, so macOS Gatekeeper REJECTS it
# (confirmed with `spctl -a -vv`) if it's opened the normal way -- double-clicking the .app in
# Finder. Executing the binary directly, the way this script does and the way every build/test/audit
# command in this project already does from Terminal, does not go through that same LaunchServices
# Gatekeeper check, so it opens cleanly with no signing prompt.
#
# If this script itself won't open (macOS asking about an unidentified developer for the .command
# file), right-click it in Finder and choose "Open" once -- that one-time override only applies to
# this tiny script, not to every future run.
set -euo pipefail

DIR="$( cd "$( dirname "${BASH_SOURCE[0]}" )/.." && pwd )"
APP="${GM_UNITY_PROJECT:-$DIR/unity-project}/Builds/macOS-Wend/Wend Hill Prologue.app"
BINARY="$APP/Contents/MacOS/The Games Master"

if [ ! -f "$BINARY" ]; then
  echo "Build not found at:"
  echo "  $BINARY"
  echo ""
  echo "Build it first (from the games-master repo):"
  echo "  node scripts/unity-cli.mjs scenes   # confirm wend-hill-prologue is registered"
  echo "See docs/WEND-HILL-GUIDE.html for the exact build command."
  read -n 1 -s -r -p "Press any key to close..."
  exit 1
fi

echo "Launching Wend Hill Prologue..."
"$BINARY" &
sleep 1
echo "Launched. This terminal window can be closed; the game keeps running."
