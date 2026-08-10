#!/usr/bin/env python3
from __future__ import annotations
import argparse, json, os, pathlib, re, sys

FORBIDDEN_PARTS = {".git", "bin", "obj", "TestResults", "models", ".venv", "venv"}
FORBIDDEN_SUFFIXES = {".log", ".pt", ".pth", ".onnx", ".safetensors", ".user", ".suo", ".pdb"}
TEXT_SUFFIXES = {".json", ".xml", ".config", ".md", ".txt", ".axaml", ".xaml", ".py", ".sh", ".ps1"}

parser = argparse.ArgumentParser()
parser.add_argument("directory")
parser.add_argument("--runtime", required=True, choices=("win-x64", "linux-x64"))
args = parser.parse_args()
root = pathlib.Path(args.directory).resolve()
native_runtime_files = {
    "win-x64": ("VoidNote.App.exe", "coreclr.dll", "hostfxr.dll", "hostpolicy.dll"),
    "linux-x64": ("VoidNote.App", "libcoreclr.so", "libhostfxr.so", "libhostpolicy.so"),
}
required = [
    root / "VoidNote.App.dll",
    root / "VoidNote.App.deps.json",
    root / "VoidNote.App.runtimeconfig.json",
    root / "System.Private.CoreLib.dll",
    root / "workers" / "python" / "voidnote_ai_worker.py",
    root / "README.md",
    root / "THIRD_PARTY_NOTICES.md",
    *(root / name for name in native_runtime_files[args.runtime]),
]
errors = [f"Missing required file: {path.relative_to(root)}" for path in required if not path.is_file()]

deps_path = root / "VoidNote.App.deps.json"
runtime_config_path = root / "VoidNote.App.runtimeconfig.json"
try:
    deps = json.loads(deps_path.read_text(encoding="utf-8"))
    runtime_target = deps.get("runtimeTarget", {}).get("name", "")
    runtime_pack = f"runtimepack.Microsoft.NETCore.App.Runtime.{args.runtime}/"
    if not runtime_target.endswith(f"/{args.runtime}"):
        errors.append(f"Publish metadata runtime target is not {args.runtime}: {runtime_target or '<missing>'}")
    if not any(name.startswith(runtime_pack) for name in deps.get("libraries", {})):
        errors.append(f"Publish metadata does not contain the .NET runtime pack for {args.runtime}")
except (OSError, json.JSONDecodeError) as exception:
    errors.append(f"Cannot read publish dependency metadata: {exception}")

try:
    runtime_options = json.loads(runtime_config_path.read_text(encoding="utf-8")).get("runtimeOptions", {})
    if "framework" in runtime_options or "frameworks" in runtime_options:
        errors.append("Runtime config still requests a globally installed shared framework")
    included = runtime_options.get("includedFrameworks", [])
    if not any(item.get("name") == "Microsoft.NETCore.App" and str(item.get("version", "")).startswith("10.") for item in included):
        errors.append("Runtime config does not record an included .NET 10 framework")
except (OSError, json.JSONDecodeError) as exception:
    errors.append(f"Cannot read runtime config metadata: {exception}")

linux_app_host = root / "VoidNote.App"
if args.runtime == "linux-x64" and linux_app_host.is_file() and not os.access(linux_app_host, os.X_OK):
    errors.append("Linux app host is not executable")
for path in root.rglob("*"):
    relative = path.relative_to(root)
    if FORBIDDEN_PARTS.intersection(relative.parts) or path.suffix.lower() in FORBIDDEN_SUFFIXES:
        errors.append(f"Forbidden package content: {relative}")
    if path.is_file() and path.stat().st_size < 2_000_000 and (path.suffix.lower() in TEXT_SUFFIXES or path.name == "LICENSE"):
        try:
            text = path.read_text(encoding="utf-8")
            if re.search(r"(?i)(C:\\Users\\[^\\\s]+|/home/[^/\s]+|/Users/[^/\s]+)", text):
                errors.append(f"Possible personal absolute path: {relative}")
        except (UnicodeDecodeError, OSError):
            pass
if errors:
    print("\n".join(errors), file=sys.stderr)
    raise SystemExit(1)
print(f"Self-contained package validation passed for {args.runtime}: {root}")
