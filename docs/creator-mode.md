# Creator Mode

## Zweck und Grenzen

Creator Mode plant getrennte Shawzin-, Audio- und MIDI-Aufnahmen für den späteren externen Videoschnitt. Er nimmt kein Video auf, integriert keine OBS-API und implementiert weder Mandachord noch eine zweite GameBridge oder Audio Engine. `FutureMandachord` ist ausschließlich ein vorbereiteter Source-Type.

## Modell

`CreatorSession` gehört über `ProjectId` zu genau einem `.vns`-Projekt und hält dessen `ProjectTimeline`, Erstellungs-/Änderungszeit, Takes, Sections, Sync-/Count-In-Einstellungen, Notizen und Songdauer. Mehrere Sessions können in `VoidNoteProject.CreatorSessions` liegen.

`CreatorTake` bewahrt Quellen-ID/-Typ/-name, Audio-Intelligence-Provenienz, Instrument und Shawzin-Definition, Skala, Transposition, Arrangementstrategie, optionalen Songcode, Range, Status, Notizen, Versuchszahl, Timingoffset, Sync-Metadaten, erwartete Eventzahl, GameBridge-Bedarf und Checkliste. Statuswechsel landen zusätzlich in `StatusHistory` mit alter/neuer Ausprägung, Zeitpunkt und Grund.

## Wizard und Retakes

Der Ensemble-Wizard zeigt alle vorhandenen Stimmen zur Auswahl. Übernommen werden Trackname und sämtliche Arrangement-/Code-Metadaten. Jeder erste Take erhält eine eigene Retake-Gruppe. Ein neuer Versuch wird als neues Objekt mit der nächsten Versuchszahl angelegt; ältere Status-, Notiz- und Checklistendaten werden nicht überschrieben.

## Sections und Partial Takes

`CreatorSection` speichert Name, Start und Ende als präzise `AbsoluteTime`. Ein Take kann den ganzen Song, eine Section oder einen Custom-Bereich verwenden. `SourceStart` markiert den Offset in der Quelle. Der absolute `MusicStart` bleibt für alle Takes gleich, während `MusicEnd` aus der jeweiligen Range-Dauer folgt.

## Count-In, Sync und Rolls

Unterstützt werden vier Beats, ein Takt, zwei Takte und eine benutzerdefinierte Beatanzahl. Taktmodi verwenden die Taktart der Master Timeline; die Zeitprojektion verwendet deren Tempo Map.

```text
Session Start → Pre-Roll → Count-In → Sync-Klickfolge → finales Sync-Signal → Music Start → Music End → Post-Roll End
```

Die synthetische WAV-Spur ist mono, 16-Bit-PCM und enthält selbst erzeugte Sinuston-Klicks, ein tiefes finales Signal und optional einen kurzen Music-Start-Marker. Es werden keine geschützten Samples verwendet.

## Playback, Dry Run und Sicherheit

`CreatorPlaybackWorkflow` stellt Prepare, Count-In, Sync Signal, Playing, Post-Roll, Complete und Stop als Zustände bereit. Quellenwiedergabe liegt hinter `ICreatorTakePlayer`, damit vorhandene Shawzin-, MIDI- und Audioausgaben adaptiert werden können. Ein Dry Run verändert den Take nicht und zeigt Dauer, Count-In, Marker, Quelle, Code, GameBridge-Bedarf, Eventzahl, Timing sowie offene Pflichtpunkte.

Shawzin-Diagnostik verwendet `CreatorGameBridgeDiagnostic` und damit ausschließlich `GameBridgePlaybackSession.DryRunAsync`; reale Eingaben werden dabei nie gesendet. Reales Creator-Playback muss denselben bestehenden GameBridge-Arm-/Fokus-/Release-/Emergency-Stop-Pfad verwenden. Eine alternative Stoplogik existiert nicht.

## Export und Framekonvertierung

Generisches JSON und CSV enthalten Take, Versuch, Pre-Roll, Count-In Start, Sync Point, Music Start, Music End, Post-Roll End und Source Start. Optional werden Frames für 24, 25, 30, 50 oder 60 FPS ausgegeben:

```text
frame = round(seconds × fps, MidpointRounding.AwayFromZero)
```

Diese Exporte dienen zugleich als neutrale Editor-Marker. Spätere editor-spezifische Exporter können hinter derselben Grenze ergänzt werden.

## Persistenz und Undo/Redo

Projektformat 3 persistiert alle Creator-Daten in `project.json`. v1/v2 werden in-memory um leere Creator Sessions ergänzt; der bestehende Speichervorgang erstellt vor dem ersten Überschreiben eine versionsbezogene Sicherung. Undo/Redo-Kommandos decken Session/Take/Section hinzufügen und entfernen, Status, Notizen und Track Assignment ab. Retake-Historien werden nicht in-place ersetzt.

## Content-Creator-Workflow

```text
Polyphonic MIDI → Multi-Shawzin → unabhängige Ensemble-Tracks/Codes
→ Tracks im Wizard auswählen → Creator Session → Dry Run je Take
→ externe Aufnahme → Status/Notizen/Retakes → JSON/CSV/WAV in Videoschnitt
```
