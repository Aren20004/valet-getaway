#!/bin/bash
set -euo pipefail
project="$(cd "$(dirname "$0")" && pwd)"
version="$(awk '/^m_EditorVersion:/ { print $2; exit }' "$project/ProjectSettings/ProjectVersion.txt" | tr -d '\r')"
editor="${UNITY_EDITOR_PATH:-/Applications/Unity/Hub/Editor/$version/Unity.app/Contents/MacOS/Unity}"
if [[ -z "$version" || ! -x "$editor" ]]; then
    printf 'Install Unity %s using Unity Hub, then add this project folder in Hub.\n' "$version"
    exit 1
fi
exec "$editor" -projectPath "$project" -openfile "$project/Assets/Scenes/ValetTestCity.unity"
