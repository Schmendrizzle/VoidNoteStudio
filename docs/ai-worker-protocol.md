# AI Worker Protocol v1

Transport ist lokales Standard Input/Output mit genau einer JSON-Request-Zeile und versionierten JSONL-Nachrichten. Diagnoseausgabe gehört auf Standard Error. Unversionierte Console-Texte gelten nie als Resultat.

Request:

```json
{"protocolVersion":1,"jobId":"uuid","operation":"separate","engine":"demucs","input":{},"settings":{}}
```

Progress:

```json
{"kind":"progress","protocolVersion":1,"jobId":"uuid","progress":0.5,"stage":"processing","message":"Separating audio"}
```

Result:

```json
{"kind":"result","protocolVersion":1,"jobId":"uuid","success":true,"outputs":{},"metrics":{},"errors":[]}
```

Stufen: Preparing, LoadingModel, Processing, WritingStems, ImportingResults, Completed, Failed, Cancelled. Worker-Progress deckt die Enginephasen ab; ImportingResults/Completed werden vom C#-Workflow gemeldet.

Validierung umfasst ProtocolVersion, JobId, Nachrichtenart, Pflichtoutputs, Notenbereiche, Confidence/Dauer und vier Standardstems. Fehlercodes umfassen mindestens `worker_missing`, `worker_start_failed`, `dependency_missing`, `model_missing`, `worker_crash`, `out_of_memory`, `unsupported_audio`, `invalid_result`, `timeout` und `worker_failed`. Bei Cancellation, Timeout oder Fehler wird der gesamte Prozessbaum beendet.
