# Optional AI setup

VoidNote does not install Python, packages or models. Use an isolated environment and review package/model licenses before installation.

## Windows

1. Install Python 3.10 or newer from python.org or a trusted package manager.
2. Create an environment: `py -m venv .venv`.
3. Activate it: `.venv\Scripts\Activate.ps1`.
4. Upgrade packaging tools: `python -m pip install --upgrade pip`.
5. Install the optional engines explicitly: `python -m pip install demucs==4.1.0 basic-pitch==0.4.0`.
6. In VoidNote Settings, set Python to `.venv\Scripts\python.exe` and Worker to `workers\python\voidnote_ai_worker.py`.
7. Save settings, restart, and run Dependency Center diagnostics.

## Linux

1. Install Python 3.10+ and its `venv` package through your distribution.
2. Run `python3 -m venv .venv` and `. .venv/bin/activate`.
3. Run `python -m pip install --upgrade pip`.
4. Explicitly run `python -m pip install demucs==4.1.0 basic-pitch==0.4.0`.
5. Configure `.venv/bin/python` and the repository worker path in VoidNote, save, restart and recheck.

## Test the worker

Discovery and real local smoke tests are opt-in:

```text
python scripts/smoke-tests.py engines
python scripts/smoke-tests.py engines --allow-demucs-model-download
```

The second command explicitly permits Demucs to use its normal model-cache behavior, which may download a large model if it is absent. Basic Pitch is checked with a generated two-tone WAV. Demucs must produce vocals, bass, drums and other stem files. A missing dependency is reported as `UNAVAILABLE`; it is not a normal CI failure.

The worker can also be tested at the protocol level by sending one JSON request line as documented in [AI Worker Protocol](ai-worker-protocol.md). Audio remains local. If an engine fails, inspect Diagnostics and the local rotated logs; do not paste private project paths into public reports without reviewing them.
