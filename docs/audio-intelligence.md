# Audio Intelligence

## Modulgrenzen

Milestone H erweitert `VoidNote.Audio`; `VoidNote.Domain` bleibt frei von Python-, ML-, Avalonia- und Engine-Libraries. Öffentliche Ports verwenden Dateipfade und VoidNote-DTOs:

```text
Audio Lab / BackgroundJobManager
  → IAudioIntelligenceWorkflow
  → IAiResourceGate
  → IAudioSeparationEngine | IAudioTranscriptionEngine
  → IAudioWorkerClient
  → lokaler externer Worker
```

`IAudioAnalysisEngine` ist als gemeinsame Discovery-/Analysegrenze vorbereitet. Fehlende Worker, Python-Umgebung oder Modelle werden als Capability gemeldet und verhindern den normalen Start nicht.

## Discovery und Settings

`AppSettings.AudioIntelligence` speichert optional Python-/Workerpfad, maximal parallele AI-Jobs und Timeout. Discovery unterscheidet `Installed`, `Missing`, `IncompatibleVersion`, `WorkerStartFailed` und `ModelMissing`; Capability meldet CPU, GPU und Modi. Default ist ein paralleler Modelljob. Installationen oder Downloads finden nie still statt.

Manuelle Referenzinstallation in einer isolierten Python-Umgebung:

```text
python -m venv <environment>
<environment-python> -m pip install demucs==4.1.0 basic-pitch==0.4.0
```

Der Benutzer trägt danach Interpreter und Workerpfad in den Settings ein. Vor Distribution sind Package-, Modell- und transitive Lizenzen erneut zu prüfen.

## Ressourcen und Sicherheit

- Worker erhalten nur Input-, Joboutput- und Settingspfade der Operation.
- Keine Netzwerk-API, Uploads oder Telemetrie.
- `Auto`, `CPU` und `GPU`; GPU wird nie vorausgesetzt. Demucs fällt in Auto auf CPU zurück. Basic Pitch meldet nur CPU/Auto und weist eine explizite GPU-Anforderung verständlich zurück.
- Cancellation/Timeout/Fehler killen den Prozessbaum.
- Jeder Job besitzt einen markierten VoidNote-Temp-Unterordner; Cleanup läuft im `finally`-Pfad.
- Orphan-Cleanup entfernt nur markierte, ausreichend alte Jobordner unter dem konfigurierten Root.
- Abgeleitete Projektassets liegen außerhalb des Job-Tempbereichs und werden beim Speichern unter `stems/` eingebettet.

## Qualitätsgrenzen

Separation und Transkription sind Vorschläge. Artefakte, Oktavfehler, fehlende/zusätzliche Noten und ungenaue Onsets sind erwartbar. Der Workflow lautet immer Automate → Preview → Edit → Validate. Roh-Confidence und Roh-Timing bleiben erhalten.
