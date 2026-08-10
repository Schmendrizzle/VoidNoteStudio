"""VoidNote local AI worker protocol v1. Dependencies are installed only by explicit user action."""

from __future__ import annotations

import contextlib
import importlib.metadata
import json
import os
import pathlib
import shutil
import subprocess
import sys
import time
import traceback

PROTOCOL_VERSION = 1


def emit(kind: str, **values: object) -> None:
    print(json.dumps({"kind": kind, **values}, separators=(",", ":")), flush=True)


def progress(job_id: str, value: float, stage: str, message: str) -> None:
    emit("progress", protocolVersion=PROTOCOL_VERSION, jobId=job_id, progress=value, stage=stage, message=message)


def result(job_id: str, success: bool, outputs: dict | None = None, metrics: dict | None = None, errors: list | None = None) -> None:
    emit("result", protocolVersion=PROTOCOL_VERSION, jobId=job_id, success=success,
         outputs=outputs or {}, metrics=metrics or {}, errors=errors or [])


def package_version(name: str) -> str | None:
    try:
        return importlib.metadata.version(name)
    except importlib.metadata.PackageNotFoundError:
        return None


def gpu_available() -> bool:
    try:
        import torch
        return bool(torch.cuda.is_available())
    except (ImportError, RuntimeError):
        return False


def discover(job_id: str, engine: str) -> None:
    package = "basic-pitch" if engine == "basic-pitch" else engine
    version = package_version(package)
    result(job_id, True, {
        "installed": version is not None,
        "version": version,
        "modelAvailable": version is not None,
        "gpuAvailable": gpu_available(),
        "executablePath": sys.executable,
        "message": f"{package} {version} is available" if version else f"{package} is not installed",
    })


def prepare_input(job_id: str, source: dict, directory: pathlib.Path) -> pathlib.Path:
    path = pathlib.Path(source["path"]).resolve(strict=True)
    start = float(source.get("startSeconds", 0))
    duration = float(source.get("durationSeconds", 0))
    if start <= 0:
        return path
    ffmpeg = shutil.which("ffmpeg")
    if not ffmpeg:
        raise RuntimeError("ffmpeg is required to process an AudioRegion")
    target = directory / "region.wav"
    command = [ffmpeg, "-nostdin", "-y", "-ss", str(start), "-i", str(path)]
    if duration > 0:
        command.extend(["-t", str(duration)])
    command.extend(["-vn", "-c:a", "pcm_f32le", str(target)])
    completed = subprocess.run(command, capture_output=True, text=True, check=False)
    if completed.returncode:
        raise RuntimeError(f"ffmpeg region extraction failed: {completed.stderr[-1000:]}")
    progress(job_id, 0.12, "Preparing", "Prepared selected audio region")
    return target


def separate(job_id: str, source: dict, settings: dict) -> None:
    version = package_version("demucs")
    if not version:
        raise ModuleNotFoundError("demucs is not installed")
    output = pathlib.Path(source["outputDirectory"]).resolve()
    output.mkdir(parents=True, exist_ok=True)
    input_path = prepare_input(job_id, source, output)
    model = settings.get("model", "htdemucs")
    device = settings.get("device", "auto")
    if device == "gpu" and not gpu_available():
        raise RuntimeError("GPU was requested but is unavailable")
    actual_device = "cuda" if device in ("gpu", "auto") and gpu_available() else "cpu"
    progress(job_id, 0.18, "LoadingModel", f"Loading Demucs model {model} on {actual_device}")
    from demucs.separate import main as demucs_main
    arguments = ["-n", model, "-d", actual_device, "-o", str(output), str(input_path)]
    progress(job_id, 0.25, "Processing", "Separating audio")
    with contextlib.redirect_stdout(sys.stderr):
        demucs_main(arguments)
    result_directory = output / model / input_path.stem
    stems = []
    for stem_type in ("vocals", "bass", "drums", "other"):
        path = result_directory / f"{stem_type}.wav"
        if not path.exists():
            raise RuntimeError(f"Demucs output is missing {stem_type}.wav")
        stems.append({"type": stem_type.title(), "name": stem_type.title(), "path": str(path),
                      "durationSeconds": float(source.get("durationSeconds", 0))})
    progress(job_id, 0.95, "WritingStems", "Validated separated stems")
    result(job_id, True, {"version": version, "stems": stems}, {"model": model, "device": actual_device})


def transcribe(job_id: str, source: dict, settings: dict) -> None:
    version = package_version("basic-pitch")
    if not version:
        raise ModuleNotFoundError("basic-pitch is not installed")
    output = pathlib.Path(source["outputDirectory"]).resolve()
    output.mkdir(parents=True, exist_ok=True)
    input_path = prepare_input(job_id, source, output)
    progress(job_id, 0.2, "LoadingModel", "Loading Basic Pitch model")
    from basic_pitch.inference import predict
    progress(job_id, 0.3, "Processing", "Detecting pitched notes")
    started = time.monotonic()
    with contextlib.redirect_stdout(sys.stderr):
        _, _, note_events = predict(str(input_path))
    notes = []
    for note in note_events:
        start, end, pitch, amplitude = note[:4]
        notes.append({"pitch": int(pitch), "startSeconds": float(start), "durationSeconds": float(end - start),
                      "velocity": float(max(0.0, min(1.0, amplitude))), "confidence": float(max(0.0, min(1.0, amplitude)))})
    result(job_id, True, {"version": version, "notes": notes}, {"processingSeconds": time.monotonic() - started,
                                                                    "mode": settings.get("mode", "auto")})


def main() -> int:
    line = sys.stdin.readline()
    if not line:
        return 2
    request = json.loads(line)
    job_id = request.get("jobId", "")
    try:
        if request.get("protocolVersion") != PROTOCOL_VERSION:
            raise ValueError("incompatible protocol version")
        operation = str(request.get("operation", "")).lower()
        engine = request.get("engine", "")
        if operation == "discover":
            discover(job_id, engine)
        elif operation == "separate" and engine == "demucs":
            separate(job_id, request["input"], request["settings"])
        elif operation == "transcribe" and engine == "basic-pitch":
            transcribe(job_id, request["input"], request["settings"])
        else:
            raise ValueError(f"unsupported operation/engine: {operation}/{engine}")
        return 0
    except MemoryError as exception:
        result(job_id, False, errors=[{"code": "out_of_memory", "message": str(exception)}])
    except ModuleNotFoundError as exception:
        result(job_id, False, errors=[{"code": "dependency_missing", "message": str(exception)}])
    except Exception as exception:
        traceback.print_exc(file=sys.stderr)
        result(job_id, False, errors=[{"code": "worker_failed", "message": str(exception)}])
    return 1


if __name__ == "__main__":
    raise SystemExit(main())
