#!/usr/bin/env bash
set -euo pipefail
project_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
check_directory="$(mktemp -d "${TMPDIR:-/tmp}/milk-selection.XXXXXX")"
trap 'rm -rf "$check_directory"' EXIT

# Compile the pure production model in a disposable portable harness outside the repository.
python3 - "$project_root" "$check_directory" <<'PY'
from pathlib import Path
from xml.sax.saxutils import escape
import sys
root, output = map(Path, sys.argv[1:])
files = [root / 'MilkDeadLockDry/Entities/Game/Model/GameDirectory.cs',
         root / 'MilkDeadLockDry/Shared/Api/Directories/IDirectoryPicker.cs',
         root / 'Tests/SelectionBehavior/Program.cs']
files.extend((root / 'MilkDeadLockDry/Features/SelectGameDirectory/Model').glob('*.cs'))
items = ''.join(f'<Compile Include="{escape(str(p), {chr(34): "&quot;"})}" />' for p in files)
(output / 'SelectionBehavior.csproj').write_text(
    '<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><OutputType>Exe</OutputType>'
    '<TargetFramework>net10.0</TargetFramework><ImplicitUsings>enable</ImplicitUsings>'
    '<Nullable>enable</Nullable></PropertyGroup><ItemGroup>' + items + '</ItemGroup></Project>')
PY
cd "$project_root"
./.dotnet/dotnet run --project "$check_directory/SelectionBehavior.csproj"
