#!/usr/bin/env bash
set -euo pipefail

if [[ "$(uname -s)" != "Darwin" || "$(uname -m)" != "arm64" ]]; then
    echo "This launcher currently targets Apple Silicon macOS." >&2
    exit 1
fi

project_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$project_root"
xcodebuild -version
sdk_version="$(python3 -c 'import json; print(json.load(open("global.json"))["sdk"]["version"])')"

if [[ ! -x .dotnet/dotnet || ! -d ".dotnet/sdk/$sdk_version" ]]; then
    installer_path="$(mktemp -t milkdeadlockdry-dotnet)"
    trap 'rm -f "$installer_path"' EXIT
    curl --fail --silent --show-error --location https://dot.net/v1/dotnet-install.sh -o "$installer_path"
    bash "$installer_path" --version "$sdk_version" --install-dir "$project_root/.dotnet" --no-path
fi

./.dotnet/dotnet workload install macos
