#!/usr/bin/env python3
"""Explicit local smoke tests. Nothing is installed and no real input/audio output is sent."""

from __future__ import annotations

import argparse
import json
import math
import pathlib
import shutil
import subprocess
import sys
import tempfile
import uuid
import wave

ROOT = pathlib.Path(__file__).resolve().parents[1]
WORKER = ROOT / "workers" / "python" / "voidnote_ai_worker.py"


def synthetic_wave(path: pathlib.Path) -> None:
    rate = 22050
    notes = [(440.0, 1.0), (523.251, 1.0)]
    with wave.open(str(path), "wb") as target:
        target.setparams((1, 2, rate, 0, "NONE", "not compressed"))
        frames = bytearray()
        for frequency, duration in notes:
            for index in range(int(rate * duration)):
                sample = int(12000 * math.sin(2 * math.pi * frequency * index / rate))
                frames.extend(sample.to_bytes(2, "little", signed=True))
        target.writeframes(frames)


def worker_request(operation: str, engine: str, source: dict, settings: dict) -> dict:
    request = {"protocolVersion": 1, "jobId": str(uuid.uuid4()), "operation": operation, "engine": engine, "input": source, "settings": settings}
    completed = subprocess.run([sys.executable, str(WORKER)], input=json.dumps(request) + "\n", text=True, capture_output=True, check=False)
    messages = [json.loads(line) for line in completed.stdout.splitlines() if line.strip()]
    result = next((item for item in reversed(messages) if item.get("kind") == "result"), None)
    if result is None:
        raise RuntimeError(f"Worker returned no result: {completed.stderr[-1000:]}")
    return result


def engine_smoke(allow_demucs_download: bool) -> int:
    unavailable = []
    for engine in ("demucs", "basic-pitch"):
        result = worker_request("discover", engine, {}, {})
        if not result.get("outputs", {}).get("installed"):
            unavailable.append(engine)
    if unavailable:
        print("UNAVAILABLE: " + ", ".join(unavailable))
        return 0
    with tempfile.TemporaryDirectory(prefix="voidnote-smoke-") as temporary:
        root = pathlib.Path(temporary)
        audio = root / "tones.wav"
        synthetic_wave(audio)
        transcription = worker_request("transcribe", "basic-pitch", {"path": str(audio), "outputDirectory": str(root / "pitch")}, {"mode": "auto"})
        notes = transcription.get("outputs", {}).get("notes", [])
        pitches = {round(item["pitch"]) for item in notes}
        if not transcription.get("success") or not ({69, 72} & pitches):
            raise RuntimeError(f"Basic Pitch did not recognize a plausible synthetic note set: {pitches}")
        print(f"PASS: Basic Pitch produced {len(notes)} note events ({sorted(pitches)}).")
        if not allow_demucs_download:
            print("SKIPPED: Demucs separation requires --allow-demucs-model-download because Demucs may fetch its model cache.")
            return 0
        separation = worker_request("separate", "demucs", {"path": str(audio), "outputDirectory": str(root / "demucs"), "durationSeconds": 2}, {"model": "htdemucs", "device": "cpu"})
        stems = separation.get("outputs", {}).get("stems", [])
        expected = {"Vocals", "Bass", "Drums", "Other"}
        actual = {item.get("type") for item in stems if pathlib.Path(item.get("path", "")).is_file()}
        if not separation.get("success") or actual != expected:
            raise RuntimeError(f"Demucs stem validation failed: {actual}")
        print("PASS: Demucs produced vocals, bass, drums and other stems.")
    return 0


def ffmpeg_smoke() -> int:
    required = ["ffmpeg", "ffplay"]
    missing = [name for name in required if shutil.which(name) is None]
    if missing:
        print("UNAVAILABLE: " + ", ".join(missing))
        return 0
    fixtures = ROOT / "tests" / "VoidNote.Audio.Tests" / "Fixtures"
    for extension in ("mp3", "flac"):
        source = fixtures / f"synthetic-sine.{extension}"
        completed = subprocess.run(["ffmpeg", "-v", "error", "-nostdin", "-i", str(source), "-f", "null", "-"], capture_output=True, check=False)
        if completed.returncode:
            raise RuntimeError(f"FFmpeg {extension.upper()} decode failed: {completed.stderr.decode(errors='replace')[-1000:]}")
        print(f"PASS: FFmpeg decoded {extension.upper()}.")
    completed = subprocess.run(["ffplay", "-version"], capture_output=True, check=False)
    if completed.returncode:
        raise RuntimeError("FFplay capability probe failed.")
    print("PASS: FFplay executable capability available (no audio was played).")
    return 0


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("suite", choices=("engines", "ffmpeg", "all"))
    parser.add_argument("--allow-demucs-model-download", action="store_true")
    args = parser.parse_args()
    if args.suite in ("engines", "all"):
        engine_smoke(args.allow_demucs_model_download)
    if args.suite in ("ffmpeg", "all"):
        ffmpeg_smoke()
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
