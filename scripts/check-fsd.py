"""Check the single-project FSD layout and explicit C# namespace imports (not a semantic analyzer)."""

from pathlib import Path
import re
import sys
import xml.etree.ElementTree as ET

ROOT = Path(__file__).resolve().parents[1]
LAYERS = {"shared": 0, "entities": 1, "features": 2, "pages": 3, "app": 4}
errors = []
PROJECT_ROOT = ROOT / "MilkDeadLockDry"
project = PROJECT_ROOT / "MilkDeadLockDry.csproj"
projects = {p.resolve() for p in ROOT.rglob("*.csproj")
            if not any(part in {".dotnet", "bin", "obj", ".git"} for part in p.relative_to(ROOT).parts)}
solution = ET.parse(ROOT / "MilkDeadLockDry.slnx")
listed = {(ROOT / p.attrib["Path"]).resolve() for p in solution.iter("Project")}
if projects != {project.resolve()} or listed != {project.resolve()}:
    errors.append("The repository and solution must contain exactly MilkDeadLockDry.csproj.")
if list(ET.parse(project).iter("ProjectReference")):
    errors.append("The application must not reference other projects.")

for folder in PROJECT_ROOT.iterdir():
    if not folder.is_dir() or folder.name in {"bin", "obj"}:
        continue
    layer = folder.name.lower()
    if layer not in LAYERS:
        errors.append(f"Unknown FSD layer: {layer}")
        continue
    for source in folder.rglob("*.cs"):
        if "bin" in source.parts or "obj" in source.parts:
            continue
        text = source.read_text()
        namespace = re.search(r"^namespace\s+([\w.]+)\s*;", text, re.MULTILINE)
        if not namespace:
            errors.append(f"Missing file-scoped namespace: {source.relative_to(ROOT)}")
            continue
        owner = namespace.group(1).split(".")
        if len(owner) < 2 or owner[:2] != ["MilkDeadLockDry", folder.name]:
            errors.append(f"Namespace does not match layer: {source.relative_to(ROOT)}")
            continue
        imports = re.findall(
            r"^\s*(?:global\s+)?using\s+(?:static\s+|\w+\s*=\s*)?"
            r"(MilkDeadLockDry\.[\w.]+)\s*;", text, re.MULTILINE
        )
        for imported in imports:
            parts = imported.split(".")
            other = parts[1].lower()
            if other not in LAYERS or LAYERS[other] > LAYERS[layer]:
                errors.append(f"Upward/unknown import: {source.relative_to(ROOT)} -> {imported}")
            elif other == layer and layer not in {"app", "shared"}:
                if len(parts) < 3 or len(owner) < 3 or parts[2] != owner[2]:
                    errors.append(f"Cross-slice import: {source.relative_to(ROOT)} -> {imported}")

if errors:
    print("\n".join(errors), file=sys.stderr)
    sys.exit(1)
print("FSD checks passed: one project, directory layers and namespace imports.")
