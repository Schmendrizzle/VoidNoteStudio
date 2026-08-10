#!/usr/bin/env python3
from __future__ import annotations
import argparse, pathlib, re, sys

FORBIDDEN_PARTS = {".git", "bin", "obj", "TestResults", "models", ".venv", "venv"}
FORBIDDEN_SUFFIXES = {".log", ".pt", ".pth", ".onnx", ".safetensors", ".user", ".suo", ".pdb"}
TEXT_SUFFIXES = {".json", ".xml", ".config", ".md", ".txt", ".axaml", ".xaml", ".py", ".sh", ".ps1"}

parser = argparse.ArgumentParser()
parser.add_argument("directory")
args = parser.parse_args()
root = pathlib.Path(args.directory).resolve()
required = [root / "VoidNote.App.dll", root / "workers" / "python" / "voidnote_ai_worker.py", root / "README.md", root / "THIRD_PARTY_NOTICES.md"]
errors = [f"Missing required file: {path.relative_to(root)}" for path in required if not path.is_file()]
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
print(f"Package validation passed: {root}")
