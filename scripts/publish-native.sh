#!/usr/bin/env bash
set -euo pipefail

project_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$project_root"
./.dotnet/dotnet publish src/DeadLocky/DeadLocky.csproj -c Release -f net10.0-macos -r osx-arm64 -o artifacts/NativeAOT
ditto src/DeadLocky/bin/Release/net10.0-macos/osx-arm64/DeadLocky.app artifacts/NativeAOT/DeadLocky.app
