#!/usr/bin/env bash
# Build assets/ottoaura_fogbubble.bundle, the Wisp Torch fog shader, with the Windows
# Unity editor from WSL. Commit the bundle it writes: the mod build embeds it, and CI has
# no Unity editor.
#
# Needs the Unity editor that matches Valheim's engine version, installed through Unity Hub
# and signed in. Override the editor path with UNITY_EXE.
#
# The Unity project is copied to a Windows temp folder first, because the editor does not
# work reliably on a \\wsl$ path. Prints DONE or FAILED as its last line.
set -euo pipefail

here="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
repo="$(cd "$here/.." && pwd)"
project="$repo/unity/OttoAuraShaders"
unity_version="$(sed -n 's/^m_EditorVersion: //p' "$project/ProjectSettings/ProjectVersion.txt")"
unity_exe="${UNITY_EXE:-/mnt/c/Program Files/Unity/Hub/Editor/$unity_version/Editor/Unity.exe}"

finish() { if [ "$1" -eq 0 ]; then echo DONE; else echo FAILED; fi; }
trap 'rc=$?; finish $rc' EXIT

if [ ! -x "$unity_exe" ]; then
    echo "Unity $unity_version is not installed at $unity_exe." >&2
    echo "Install it through Unity Hub, or set UNITY_EXE." >&2
    exit 1
fi

win_temp="$(wslpath "$(cmd.exe /c 'echo %LOCALAPPDATA%' 2>/dev/null | tr -d '\r')")/Temp/OttoAuraShaders"
mkdir -p "$win_temp"
# Keep the editor's Library cache between runs; replace everything else.
rsync -a --delete --exclude Library/ --exclude Logs/ --exclude Build/ "$project/" "$win_temp/"
rm -rf "$win_temp/Build"

log="$win_temp/Logs/build.log"
mkdir -p "$win_temp/Logs"
set +e
"$unity_exe" -batchmode -nographics -quit \
    -projectPath "$(wslpath -w "$win_temp")" \
    -executeMethod BuildFogBubbleBundle.Build \
    -bundleOutput "$(wslpath -w "$win_temp/Build")" \
    -logFile "$(wslpath -w "$log")"
status=$?
set -e
# The editor writes its log on the Windows side; show the end of it either way.
tail -n 40 "$log" | tr -d '\r' || true
if [ "$status" -ne 0 ]; then
    echo "Unity exited with status $status. Full log: $log" >&2
    exit "$status"
fi

cp "$win_temp/Build/ottoaura_fogbubble.bundle" "$repo/assets/ottoaura_fogbubble.bundle"
ls -l "$repo/assets/ottoaura_fogbubble.bundle"
