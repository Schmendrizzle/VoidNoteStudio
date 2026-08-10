# Audio Lab Core

## Umfang und Grenzen

Milestone G implementiert Import, Projektverwaltung, Waveforms und Wiedergabe für WAV, FLAC und MP3. Milestone H ergänzt darauf nicht-destruktive Stem Separation, Audio-to-MIDI, Engine Discovery, Worker-/Temp-Lifecycle und eine leichte Stem-Mix-Vorschau. Pitch-/Beat-/BPM-Erkennung außerhalb der gekapselten Transkriptionsengine und Instrumentklassifikation bleiben offen.

## Datenmodell

- `AudioSource`: stabile ID, Name, ursprünglicher Quellpfad, eingebettete/relative/absolute Projektdateireferenz, Dateiidentität und `AudioFormatInfo`.
- `AudioFormatInfo`: Container, Codec, Sample Rate, Channel Count, optionale Bit Depth/Bitrate, Duration, Channel-Beschreibungen sowie optionale Title-/Artist-Tags.
- `AudioTrack`: projektweite Spur mit Gain, Mute, Solo und Active.
- `AudioClip`: stabile ID, Source-ID, Start als `MusicalTime` auf der Master-Timeline, nicht-destruktives Trim-In, Duration, Gain und Active.
- `AudioRegion`: Auswahlstart/-ende, berechnete Duration und Loop-Flag in präziser `AbsoluteTime`.
- `StemSet`/`Stem`: abgeleitete AudioSources mit Engine-, Settings-, Offset- und Provenienzmetadaten.
- `AudioTranscriptionReport`: Confidence-Verteilung, verworfene Noten, Dichte, Pitchbereich, Laufzeit und jede Cleanup-Änderung.

Die Domain kennt weder FFmpeg-Typen noch Avalonia oder ein konkretes Audio-Backend. Originaldateien werden nur lesend geöffnet und nie verändert.

## Import und unterstützte Formate

```text
Datei → Extension-/Zugriffsvalidierung → IAudioDecoder.ProbeAsync
      → AudioSource → AudioTrack + AudioClip → VoidNoteProject
```

`IAudioImportService` unterstützt Cancellation und stufenweisen Progress. WAV, FLAC und MP3 werden akzeptiert; andere Extensions, fehlende/unlesbare Dateien, ungültiges Audio und nicht unterstützte Codecs erzeugen strukturierte Fehler. Derselbe normalisierte Quellpfad kann nicht still ein zweites Mal importiert werden.

Der eingebaute `WaveAudioDecoder` liest RIFF/WAVE PCM mit 8/16/24/32 Bit und IEEE-Float mit 32 Bit chunkweise. MP3 und FLAC laufen über `FfmpegAudioDecoder`, der `ffprobe` für Metadaten und `ffmpeg` für interleaved Float-PCM verwendet. Library- oder Prozessdetails verlassen `VoidNote.Audio` nicht.

## Externe Dependency

Referenz und getestete Zielversion ist **FFmpeg 8.1.2 „Hoare“** mit den Programmen `ffmpeg`, `ffprobe` und `ffplay`. FFmpeg ist überwiegend **LGPL 2.1 oder neuer**; konkrete Builds können durch aktivierte optionale Komponenten unter **GPL 2 oder neuer** fallen. Vor Distribution muss deshalb die Lizenz des tatsächlich ausgelieferten Builds geprüft werden. VoidNote bündelt Milestone G keinen FFmpeg-Build und linkt nicht gegen FFmpeg-Bibliotheken, sondern verwendet einen vollständig gekapselten lokalen Prozessadapter.

Gewählt wurde FFmpeg wegen der stabilen plattformübergreifenden MP3-/FLAC-/WAV-Unterstützung, der Streaming-Ausgabe und der breiten Verfügbarkeit unter Windows und Linux. Executable-Pfade können in den Audio-Settings (`FfmpegExecutablePath`, `FfprobeExecutablePath`, `FfplayExecutablePath`) festgelegt werden; sonst gelten die normalen `PATH`-Namen. Fehlen Executables, startet die Anwendung normal: Der eingebaute WAV-Pfad bleibt verfügbar; MP3/FLAC-Probe sowie FFplay-Ausgabe melden `DecoderUnavailable` beziehungsweise eine nicht verfügbare Device-Capability.

Offizielle Referenzen: <https://ffmpeg.org/download.html> und <https://ffmpeg.org/legal.html>.

## `.vns`-Integration und Quellenauflösung

`ProjectPathKind` unterscheidet:

- `Embedded`: unveränderte Datei unter `audio/<stable-id>.<ext>` im ZIP-Container;
- `Relative`: externe Referenz relativ zum Speicherort des Projekts;
- `Absolute`: ausdrücklich nicht portable externe Referenz.

Beim Speichern wird eine fehlende eingebettete Quelle als Fehler gemeldet, bevor die Hauptdatei ersetzt wird. Beim Laden wird eingebettetes Audio in einen lokalen temporären Arbeitsbereich extrahiert und als Runtime-Pfad aufgelöst. `AudioSourceDiagnostics` unterscheidet verfügbar, fehlend, verändert, eingebettet und ungültig. Eine Relink-UI ist noch nicht Bestandteil von Milestone G.

## Waveform und Cache

```text
IAudioDecoder (bounded PCM chunks)
  → 256 Frames je Min/Max-Peak und Kanal
  → paarweise gröbere Peak-Stufen
  → versionierter lokaler `.vnwf`-Cache
  → Avalonia WaveformControl
```

Der Cache-Key enthält vollständigen Pfad, Dateigröße, UTC-Änderungszeit, Decoder-ID, Codec, Sample Rate, Kanäle und Bit Depth. Ein Key-Wechsel invalidiert den Eintrag. Header-, Versions-, Key- und Strukturfehler führen zu lokalem Warning-Logging, Entfernen des beschädigten Eintrags und Neuberechnung. Der Cache kann aus dem Audio Lab gelöscht werden und wird nie an externe Dienste übertragen.

Die UI wählt anhand der sichtbaren Pixelbreite eine geeignete Peak-Stufe. Zoom ändert die virtuelle Breite; horizontales Scrolling kommt vom Avalonia-ScrollViewer. Klick/Drag setzt Playhead und Auswahl, der Playhead bleibt sichtbar, und alle Berechnungen laufen als abbrechbare Background Jobs.

## Playback und Geräte

`AudioPlaybackEngine` arbeitet auf `VoidNoteProject.Timeline`. Der Clipstart wird über `ProjectTimeline.ToAbsoluteTime` projiziert; Seek rechnet vom Masterzeitpunkt auf Source-Trim-In um. Jeder Lauf besitzt einen festen monotonic-clock-Anker. Startoffsets werden als absolute Ziele gegen diesen Anker gewartet und nicht als fortlaufende Delays addiert.

Transport: Play, Pause, Stop, Seek, Current Position, Duration, Gain, Mute, Solo und Cancellation. Pause stoppt den aktuellen Stream und Resume dekodiert ab der erfassten Masterposition neu. Stop kehrt zu null beziehungsweise zum Loopstart zurück. Eine Auswahl kann als Loopregion geladen werden.

`IAudioOutputDevice` und `IAudioDeviceProvider` kapseln konkrete Ausgabe. `FfplayAudioDevice` unterstützt unter Windows und Linux das System-Defaultgerät. FFplay bietet in dieser ersten Adapterstufe keine portable Geräteenumeration; `SupportsDeviceEnumeration=false` macht dies transparent. `DiagnosticAudioOutputDevice` konsumiert PCM ausschließlich im Speicher und wird für Offline-Tests verwendet.

## Timingpräzision

Masterpositionen werden mit `decimal`-Sekunden und der bestehenden Tempo Map berechnet. Der Transport verwendet `Stopwatch` als monotone Uhr; dadurch entsteht keine kumulative Drift aus nacheinander addierten Sleeps. Offline-Tests decken Start 0, Offset, Seek, Pause/Resume, Stop/Restart und weit auseinanderliegende absolute Ziele ab.

Die Genauigkeit ist für Preview- und Creator-Vorbereitung ausgelegt, nicht als sample-genaue DAW-Synchronisation garantiert. FFplay-Pufferung, Betriebssystem-Scheduling und Hardwarelatenz sind nicht clock-synchronisiert oder kompensiert.

## Memory-Verhalten

WAV und FFmpeg liefern standardmäßig Blöcke von etwa 4096 Frames. Vollständige MP3-/FLAC-Dateien werden nicht als PCM im RAM gehalten. Waveforms speichern auf der feinsten Stufe einen Peak je 256 Frames und gröbere Zweier-Stufen; damit wächst der Speicher mit Peakanzahl statt PCM-Sampleanzahl. Der Offline-Langdateitest verarbeitet eine synthetische Minute und prüft diese Reduktion.

## Jobs, Fehler und Diagnostik

`BackgroundJobManager` führt Probe/Import sowie Waveform-/Cache-Generierung mit Status, Progress, Cancellation und Fehlerweitergabe aus. Decoder, Probe, Playback Start/Stop und Cachefehler verwenden das bestehende lokale `Microsoft.Extensions.Logging`-System. Es gibt keine Telemetrie.

Behandelte Fehlerklassen: Datei fehlt/unlesbar, ungültiges Audio, unsupported codec, fehlender Decoder, Decodefehler, fehlendes Audio-Gerät, Playbackfehler und beschädigter Cache. Normale Fehler stoppen nur die betroffene Operation, nicht die Anwendung.

## Bekannte Einschränkungen

- FFmpeg/ffprobe/ffplay müssen für MP3, FLAC und Live-Ausgabe separat installiert oder konfiguriert werden.
- Die erste FFplay-Integration verwendet nur das System-Defaultgerät.
- Kein sample-genauer Mix mehrerer gleichzeitig klingender Audio-Tracks; Milestone G spielt den ausgewählten Track und berücksichtigt projektweite Solo-Zustände.
- Eingebettete Quellen werden zum Dekodieren lokal extrahiert; ein Lifecycle-/Größenmanager für diese Arbeitskopien folgt später.
- Trim-Out wird durch Clip-Duration repräsentiert; es gibt noch keinen separaten Trim-Out-Wert im UI.
- Mehrere Stem-Previewprozesse teilen eine monotone Uhr, aber FFplay-/Hardwarepuffer sind nicht samplegenau gekoppelt.
- Gemeinsame Audio/MIDI-Synth-Vorschau ist noch kein synchroner DAW-Mix; die Einschränkung wird im Audio-Intelligence-Bereich transparent gehalten.
- AI-Dependencies und Modelle sind optional und werden niemals automatisch installiert oder heruntergeladen.
