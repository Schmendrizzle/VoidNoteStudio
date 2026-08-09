# VoidNs Pflichtenheft – Version 1.0

**Projektname:** VoidNote Studio
**Produkttyp:** Desktop-Anwendung / Warframe Music Creator Suite
**Zielplattformen:** Windows und Linux
**Technologiebasis:** C# / .NET
**UI-Framework:** Avalonia UI
**Projektstatus:** Konzept / Entwicklungsgruntmotto:** *Create. Convert. Perform.*

---

# 1. Produktvision

VoidNote Studio soll eine umfassende Desktop-Anwendung zur Erstellung, Bearbeitung, Konvertierung und Wiedergabe von Musik für die musikalischen Systeme von Warframe werden.

Der Kern der Anwendung besteht nicht lediglich aus einem Shawzin-Autoplayer. VoidNote Studio soll eine vollständige Musikproduktionsumgebung bereitstellen, in der Musik aus unterschiedlichen Quellen importiert, analysiert, in einzelne Bestandteile zerlegt, als MIDI bearbeitet, für Shawzins arrangiert und für die Verwendung innerhalb von Warframe exportiert werden kann.

Zusätzlich soll das Mandachord von Octavia in einem eigenständigen Modul unterstützt werden.

Die Software soll insbesondere Content Creatorn ermöglichen, komplexe Musikstücke auf mehrere virtuelle bzw. reale Ingame-Instrumente aufzuteilen und diese anschließend getrennt in Warframe aufzunehmen.

Ein typischer Creator-Workflow kann dadurch beispielsweise folgendermaßen aussehen:

```text
MP3 / FLAC / WAV
        │
        ▼
Audioanalyse
        │
        ▼
Stem Separation
        │
 ┌──────┼────────┬─────────┐
 ▼      ▼        ▼         ▼
Vocals Bass     Drums     Other
        │
        ▼
Audio → MIDI
        │
        ▼
MIDI Editor
        │
        ▼
Arrangement
        │
 ┌──────┼──────────┬──────────┐
 ▼      ▼          ▼          ▼
Shawzin A     Shawzin B   Shawzin C   Mandachord
 │             │           │            │
 ▼             ▼           ▼            ▼
Code A        Code B      Code C       Pattern
 │             │           │
 ▼             ▼           ▼
Ingame Take 1 Take 2     Take 3
        │
        ▼
externer Videoschnitt
        │
        ▼
fertiges Musikvideo
```

---

# 2. Hauptziele

VoidNote Studio soll folgende übergeordnete Aufgaben erfüllen:

1. Shawzin-Songs erstellen und bearbeiten.
2. bestehende Warframe-Shawzin-Codes importieren.
3. gültige Shawzin-Codes exportieren.
4. MIDI-Dateien importieren und analysieren.
5. MIDI-Dateien erzeugen und exportieren.
6. Shawzin-Eingaben als MIDI aufzeichnen.
7. MIDI-Geräte als Eingabegeräte verwenden.
8. Songs über Shawzin-kompatible Eingaben in Warframe spielen.
9. komplexe MIDI-Stücke automatisch auf mehrere Shawzins verteilen.
10. MP3-, FLAC- und WAV-Dateien importieren.
11. Audiodateien in musikalische Stems zerlegen.
12. einzelne Stems in MIDI transkribieren.
13. erkannte MIDI-Daten manuell korrigierbar machen.
14. mehrere Shawzin-Spuren innerhalb eines Projektes verwalten.
15. Creator-Aufnahmesitzungen synchronisieren.
16. Mandachord-Patterns aus Musikstücken bzw. MIDI erzeugen.
17. alle Konvertierungsschritte zerstörungsfrei gestalten.
18. Windows und Linux unterstützen.

---

# 3. Nicht-Ziele und Sicherheitsgrenzen

VoidNote Studio darf Warframe nicht manipulieren.

Insbesondere sind ausgeschlossen:

* DLL-Injection
* Code Injection
* Memory Reading
* Memory Writing
* Manipulation von Warframe-Dateien
* Packet-Manipulation
* Umgehung von Anti-Cheat-Systemen
* Prozess-Hooking zur Manipulation des Spiels
* automatisiertes Gameplay außerhalb der Musikinstrument-Funktion
* AFK-Spielmechaniken
* automatisierte Kampfhandlungen
* automatisierte Ressourcen- oder Missionsabläufe

Die Ingame-Bridge darf ausschließlich abstrahierte normale Benutzereingaben erzeugen, soweit dies auf dem jeweiligen Betriebssystem möglich ist.

Digital Extremes weist ausdrücklich darauf hin, dass externe Software zusammen mit Warframe grundsätzlich auf eigenes Risiko verwendet wird. Außerdem unterscheidet DE zwischen unterstützenden Makros und Automatisierung, die menschliche Interaktion aus dem eigentlichen Gameplay entfernt. VoidNote Studio muss deshalb in der Anwendung einen entsprechenden Hinweis anzeigen und darf keine Behauptung aufstellen, die Benutzung sei von Digital Extremes ausdrücklich erlaubt oder „ban-safe“.

---

# 4. Zielgruppen

## 4.1 Shawzin-Spieler

Spieler, die eigene Stücke komponieren, bestehende Codes bearbeiten oder MIDI-Dateien auf der Shawzin wiedergeben möchten.

## 4.2 Musiker

Benutzer mit musikalischer Erfahrung, die MIDI-Keyboards, Piano-Roll-Editing und Arrangement-Funktionen verwenden möchten.

## 4.3 Content Creator

Benutzer, die komplexe Musikstücke in mehrere Shawzin-Spuren zerlegen und diese als getrennte Video-/Audio-Takes aufnehmen möchten.

## 4.4 Gelegenheitsspieler

Benutzer ohne tiefere MIDI- oder Musiktheoriekenntnisse.

Dafür muss VoidNote einen einfachen Workflow anbieten:

```text
Song auswählen
→ automatisch analysieren
→ Shawzin auswählen
→ Vorschlag erzeugen
→ Vorschau
→ Code kopieren
```

Ein Expert-Modus darf zusätzliche Einstellungen freischalten.

---

# 5. Technologische Grundlage

## 5.1 Programmiersprache

C# auf einer aktuellen stabilen .NET-Version.

Die konkrete .NET-Version soll beim Entwicklungsbeginn als zentrale Build-Konfiguration festgelegt und nicht über einzelne Projekte verteilt definiert werden.

## 5.2 Benutzeroberfläche

Avalonia UI.

Avalonia unterstützt Windows und Desktop-Linux und erlaubt die gemeinsame Verwendung von Views, ViewModels und Businesslogik über die Plattformen hinweg.

## 5.3 Architektur

Die Anwendung verwendet:

**MVVM + Clean Architecture + modulare Services.**

Die Benutzeroberfläche darf niemals direkt Warframe-Eingaben, MIDI-Dateien oder AI-Prozesse steuern.

Abhängigkeiten erfolgen über Interfaces.

Beispiel:

```text
UI
│
▼
Application Layer
│
▼
Domain
▲
│
Infrastructure
```

---

# 6. Solution-Struktur

Empfohlene Projektstruktur:

```text
VoidNoteStudio.sln

src/
│
├─ VoidNote.App
│   ├─ Views
│   ├─ ViewModels
│   ├─ Controls
│   └─ Themes
│
├─ VoidNote.Domain
│   ├─ Projects
│   ├─ Music
│   ├─ Midi
│   ├─ Shawzin
│   └─ Mandachord
│
├─ VoidNote.Application
│   ├─ Commands
│   ├─ Services
│   ├─ Workflows
│   └─ Validation
│
├─ VoidNote.Audio
│   ├─ Playback
│   ├─ Analysis
│   ├─ Separation
│   └─ Transcription
│
├─ VoidNote.Midi
│   ├─ Import
│   ├─ Export
│   ├─ Recording
│   └─ Devices
│
├─ VoidNote.Shawzin
│   ├─ Codec
│   ├─ Arrangement
│   ├─ Playback
│   └─ Mapping
│
├─ VoidNote.Mandachord
│   ├─ Analysis
│   ├─ Arrangement
│   └─ Preview
│
├─ VoidNote.GameBridge
│   ├─ Abstractions
│   ├─ Windows
│   └─ Linux
│
├─ VoidNote.Infrastructure
│   ├─ Files
│   ├─ Settings
│   ├─ Logging
│   └─ Processes
│
└─ VoidNote.PluginContracts

tests/
│
├─ VoidNote.Domain.Tests
├─ VoidNote.Midi.Tests
├─ VoidNote.Shawzin.Tests
├─ VoidNote.Audio.Tests
├─ VoidNote.Mandachord.Tests
└─ VoidNote.IntegrationTests
```

Kein einzelnes Modul darf von der GUI abhängig sein.

---

# 7. Zentrales Projektformat

VoidNote benötigt ein eigenes Projektformat.

Dateiendung:

```text
.vns
```

Alternativ intern:

```text
VoidNote Project
```

Das Projektformat soll vorzugsweise ein ZIP-basiertes Containerformat sein.

Beispiel:

```text
MySong.vns
│
├─ project.json
├─ audio/
│   └─ source.flac
│
├─ stems/
│   ├─ vocals.flac
│   ├─ bass.flac
│   ├─ drums.flac
│   └─ other.flac
│
├─ midi/
│   ├─ vocals.mid
│   ├─ bass.mid
│   └─ lead.mid
│
├─ shawzin/
│   ├─ lead.json
│   ├─ harmony.json
│   └─ bass.json
│
├─ mandachord/
│   └─ arrangement.json
│
└─ cache/
```

Große Dateien dürfen optional auch außerhalb des Projektes referenziert werden.

---

# 8. Gemeinsames Zeitmodell

Alle Medien müssen dieselbe Master-Timeline verwenden.

Interne Zeitwerte dürfen nicht primär als gerundete Millisekunden gespeichert werden.

Vorgesehen wird eine hochpräzise Zeitrepräsentation.

Beispiel:

```csharp
MusicalTime
AbsoluteTime
MidiTicks
BeatPosition
```

Folgende Konvertierungen müssen möglich sein:

```text
Seconds
↕
Beats
↕
Bars
↕
MIDI Ticks
```

Tempoänderungen innerhalb einer MIDI-Datei müssen berücksichtigt werden.

---

# 9. Zentrales Musikdatenmodell

```text
VoidNoteProject
│
├─ Metadata
├─ Timeline
├─ AudioSources[]
├─ Stems[]
├─ MidiTracks[]
├─ ShawzinTracks[]
├─ MandachordTracks[]
└─ CreatorSessions[]
```

Ein generisches musikalisches Event enthält mindestens:

```text
ID
StartTime
Duration
Pitch
Velocity
Source
Confidence
```

Wichtig ist die Eigenschaft:

```text
Source
```

Beispiele:

```text
ImportedMidi
AudioTranscription
Manual
ShawzinRecording
Generated
```

Dadurch können automatisch erkannte und manuell gesetzte Noten unterschieden werden.

---

# 10. Hauptoberfläche

VoidNote Studio verwendet eine klassische DAW-artige Oberfläche.

```text
┌──────────────────────────────────────────────────────┐
│ VOIDNOTE STUDIO                              Project │
├─────────────┬────────────────────────────────────────┤
│             │                                        │
│ PROJECT     │             TIMELINE                   │
│             │                                        │
│ Audio       │ ────────────────────────────────────── │
│ Stems       │                                        │
│ MIDI        │ Track 1  █████   █████                 │
│ Shawzins    │ Track 2     █████      ███             │
│ Mandachord  │ Track 3  ███   █████                   │
│             │                                        │
├─────────────┴────────────────────────────────────────┤
│ ▶ ■ ●      01:24.381 / 04:12.000      BPM 128       │
└──────────────────────────────────────────────────────┘
```

Workspaces:

```text
PROJECT
AUDIO LAB
MIDI WORKSHOP
SHAWZIN STUDIO
MANDACHORD STUDIO
CREATOR MODE
```

---

# 11. Audio Lab

## 11.1 Import

Unterstützte Eingabeformate mindestens:

```text
WAV
FLAC
MP3
```

Optional später:

```text
OGG
M4A
AAC
```

Die ursprüngliche Datei wird niemals verändert.

---

# 12. Waveform-Darstellung

Audiodateien erhalten eine zoombare Waveform.

Funktionen:

* Zoom
* Scroll
* Auswahl
* Loop
* Marker
* Start-/Endpunkt
* Playback
* Lautstärke
* Mute
* Solo

---

# 13. Stem Separation

VoidNote stellt eine abstrakte Schnittstelle bereit:

```csharp
IAudioSeparationEngine
```

Die konkrete Separation Engine darf austauschbar sein.

Beispiel:

```text
Audio
 ↓
IAudioSeparationEngine
 ↓
StemSet
```

Mindestens folgende Stem-Kategorien sollen unterstützt werden:

```text
Vocals
Bass
Drums
Other
```

Erweiterbare Kategorien:

```text
Piano
Guitar
Strings
Lead
Backing Vocals
```

Demucs kann als erste optionale Engine bzw. Referenz dienen; das ursprüngliche Meta/Facebook-Repository ist allerdings seit Januar 2025 archiviert. Daher darf keine Kernarchitektur direkt von Demucs abhängig sein.

---

# 14. Audio-to-MIDI

Schnittstelle:

```csharp
IAudioTranscriptionEngine
```

Workflow:

```text
Audio / Stem
     ↓
Pitch Detection
     ↓
Note Detection
     ↓
Timing Detection
     ↓
Confidence
     ↓
MidiTrack
```

Spotify Basic Pitch ist eine mögliche erste Engine und stellt automatische Audio-to-MIDI-Transkription inklusive polyphoner Erkennung bereit.

AI-/Python-Komponenten dürfen als separater Workerprozess betrieben werden.

Die C#-Anwendung muss mit ihnen über eine klar definierte IPC-Schnittstelle kommunizieren.

Der Core darf keine Python-Objekte kennen.

---

# 15. Confidence-System

Automatisch erkannte Noten erhalten einen Confidence-Wert.

Beispiel:

```text
NOTE                  CONFIDENCE

A4                    98 %
C5                    92 %
F#4                   51 %
B5                    23 %
```

Darstellung:

```text
High confidence
Medium confidence
Low confidence
```

Der Anwender kann nach unsicheren Noten filtern.

---

# 16. MIDI Workshop

## 16.1 MIDI Import

Import von Standard MIDI Files.

Mehrspurige MIDI-Dateien müssen erhalten bleiben.

Informationen:

```text
Tracks
Tempo
Time Signature
Notes
Velocity
Program Changes
Markers
```

DryWetMIDI ist hierfür als primäre gekapselte .NET-Bibliothek vorgesehen; die Bibliothek unterstützt u. a. Lesen, Schreiben und Erzeugen von Standard-MIDI-Dateien.

---

# 17. MIDI Export

Jede geeignete Spur kann als `.mid` exportiert werden.

Mögliche Quellen:

```text
Audio Transcription
Shawzin Track
Live Recording
Mandachord Arrangement
Manually Created Track
```

Exportdialog:

```text
MIDI EXPORT

☑ Preserve timing
☑ Tempo map
☑ Velocity
☐ Quantize

Quantization:
1/16

[ EXPORT ]
```

---

# 18. MIDI Recording

VoidNote soll MIDI-Geräte erkennen können.

Workflow:

```text
MIDI Keyboard
      │
      ├─────► MIDI Recorder
      │
      └─────► Shawzin Mapper
                    │
                    ▼
                 Warframe
```

Aufnahmefunktionen:

```text
Record
Pause
Stop
Count-In
Metronome
Loop
Quantize after recording
```

---

# 19. Piano Roll Editor

Zentrale Bearbeitungsoberfläche.

Funktionen:

* Note erstellen
* Note löschen
* verschieben
* kopieren
* einfügen
* verlängern
* verkürzen
* Transposition
* Oktavverschiebung
* Velocity
* Quantisierung
* Mehrfachauswahl
* Undo/Redo
* Snap-to-Grid
* Zoom
* Loop
* Solo
* Mute

---

# 20. Shawzin Studio

Shawzin Studio ist ein eigenständiger Workspace.

Es verwendet das gemeinsame Musikdatenmodell.

---

# 21. Shawzin-Instrumentprofile

Instrumenteigenschaften werden nicht hart im Programmcode verteilt.

Stattdessen:

```text
ShawzinDefinition
```

enthält beispielsweise:

```text
ID
Name
AvailableNotes
ScaleDefinitions
InputMapping
Tuning
PreviewInstrument
Capabilities
```

Neue Shawzins sollen später ohne Änderung der Kernlogik ergänzt werden können.

---

# 22. Shawzin-Code Import

VoidNote muss bestehende Warframe-Shawzin-Codes einlesen können.

```text
Song Code
   ↓
Decoder
   ↓
ShawzinSong
```

Der Decoder muss fehlerhafte Eingaben erkennen.

Fehlermeldungen müssen verständlich sein.

Beispiel:

```text
Der Code konnte nicht vollständig gelesen werden.

Fehlerposition: 128
Grund: ungültiges Timing-Symbol
```

Bereits existierende Open-Source-Projekte demonstrieren sowohl Shawzin-Code-Konvertierung als auch MIDI→Shawzin-Konvertierung und können als Referenzfixtures für Tests verwendet werden.

---

# 23. Shawzin-Code Export

Jede kompatible Shawzin-Spur kann in einen Warframe-kompatiblen Songcode umgewandelt werden.

```text
ShawzinTrack
     ↓
Encoder
     ↓
Song Code
```

Exportansicht:

```text
VOIDNOTE SHAWZIN EXPORT

Track:
Lead Guitar

Notes:
428

Duration:
02:41.220

Instrument:
[Selected Shawzin]

Compatibility:
100 %

──────────────────────────

5BAA...

[COPY CODE]
[VALIDATE]
[SAVE]
```

---

# 24. Roundtrip-Validierung

Jeder erzeugte Code muss automatisch überprüfbar sein.

```text
Original Track
      ↓
Encoder
      ↓
Code
      ↓
Decoder
      ↓
Recovered Track
      ↓
Comparator
```

Resultat:

```text
ROUNDTRIP VALIDATION

Notes:
428 / 428

Pitch mismatch:
0

Timing mismatch:
0

Result:
VALID
```

Der Shawzin-Codec benötigt eine umfangreiche Sammlung bekannter gültiger Codes als Test-Fixtures.

---

# 25. Shawzin Arranger

Der Arranger übersetzt beliebige MIDI-Daten in spielbare Shawzin-Daten.

Probleme, die erkannt werden müssen:

```text
Pitch outside range
Unsupported note
Impossible chord
Excessive polyphony
Input collision
Timing collision
Excessive note density
```

---

# 26. Automatische Optimierung

Mögliche Strategien:

```text
Closest Pitch
Preserve Melody
Preserve Harmony
Octave Shift
Drop Lowest
Drop Highest
Arpeggiate
Simplify
```

Jede automatische Veränderung muss rückgängig gemacht werden können.

---

# 27. Compatibility Score

Eine MIDI-Spur erhält vor der Konvertierung eine Analyse:

```text
SHAWZIN COMPATIBILITY

Directly playable       83 %
Octave fixable           9 %
Timing conflicts         3 %
Unsupported notes        5 %

Overall:
91 / 100
```

---

# 28. Multi-Shawzin Splitter

Eines der Kernfeatures von VoidNote.

Eine komplexe Spur kann automatisch auf mehrere Shawzins verteilt werden.

Beispiel:

```text
Piano.mid

↓ SPLIT TO 3 SHAWZINS

Shawzin 1
Lead Melody

Shawzin 2
Upper Harmony

Shawzin 3
Bass / Lower Harmony
```

Der Benutzer kann wählen:

```text
2 Shawzins
3 Shawzins
4 Shawzins
Custom
```

---

# 29. Split-Strategien

Presets:

```text
Melody + Harmony
Melody + Bass
Full Ensemble
Minimal Note Loss
Maximum Recognition
Creator Multitrack
```

Der Algorithmus soll musikalische Stimmen nach Möglichkeit zusammenhängend halten.

---

# 30. Shawzin Playback Engine

Die Playback Engine verwendet eine absolute Master-Timeline.

Nicht zulässig:

```text
Play note
sleep(100)
Play next note
sleep(100)
```

Vorgesehen:

```text
MasterClock
     ↓
ScheduledEventQueue
     ↓
Output Scheduler
```

Jedes Event besitzt einen absoluten Zielzeitpunkt.

Dadurch darf sich ein Timingfehler nicht über den gesamten Song aufsummieren.

---

# 31. Virtuelle Shawzin

VoidNote soll Shawzin-Musik auch außerhalb von Warframe wiedergeben können.

Möglichkeiten:

```text
PC Keyboard
MIDI Keyboard
Mouse
Piano Roll
Song Playback
```

Die virtuelle Shawzin dient gleichzeitig als Testumgebung für Arrangements.

---

# 32. Ingame Bridge

Abstraktion:

```csharp
IGameInputBridge
```

Implementierungen:

```text
WindowsGameInputBridge
LinuxGameInputBridge
```

Die Shawzin-Engine darf niemals direkt Betriebssystemfunktionen aufrufen.

---

# 33. Keybind Editor

Keine Shawzin-Taste wird fest einprogrammiert.

Der Benutzer konfiguriert sein Warframe-Layout.

Beispiel:

```text
String 1      [1]
String 2      [2]
String 3      [3]

Fret Left     [←]
Fret Middle   [↓]
Fret Right    [→]
```

Profile können gespeichert werden.

---

# 34. Plattformunterschiede

Windows- und Linux-Eingabesimulation wird getrennt implementiert.

Unter Linux müssen unterschiedliche Desktop- und Display-Umgebungen berücksichtigt werden.

Falls Eingabesimulation auf einem System nicht zuverlässig möglich ist, bleibt:

```text
Composition
MIDI
Audio
Songcode Export
Mandachord
```

weiter vollständig nutzbar.

VoidNote darf deshalb nicht voraussetzen:

```text
GameBridge == verfügbar
```

sondern:

```text
GameBridgeCapability
```

---

# 35. Recording von Shawzin-Eingaben

Der Benutzer kann seine eigenen Shawzin-Eingaben aufnehmen.

```text
Keyboard Input
      ↓
Input Mapper
      ↓
Shawzin Event
      ↓
Recorder
      ↓
ShawzinTrack
```

Dieser Track kann anschließend:

```text
bearbeitet
als Songcode exportiert
als MIDI exportiert
quantisiert
wieder abgespielt
```

werden.

---

# 36. Raw vs. Quantized Recording

Recording-Modi:

**Raw Performance**

Bewahrt tatsächliche menschliche Timingabweichungen.

**Quantized**

Korrigiert beispielsweise auf:

```text
1/4
1/8
1/16
1/32
Triplets
```

---

# 37. Creator Mode

Creator Mode ist speziell für Multitrack-Aufnahmen vorgesehen.

Beispielprojekt:

```text
Track 1 – Lead Shawzin
Track 2 – Harmony Shawzin
Track 3 – Bass Shawzin
Track 4 – Mandachord
```

---

# 38. Synchronisationssignal

Alle einzelnen Ingame-Aufnahmen müssen leicht synchronisiert werden können.

VoidNote erzeugt deshalb vor jedem Take optional:

```text
3
2
1
SYNC
```

und zusätzlich ein akustisches Synchronisationssignal.

Beispielsweise:

```text
CLICK
CLICK
CLICK
CLAP
```

Dadurch lassen sich die Takes im Videoschnitt framegenau übereinanderlegen.

---

# 39. Take Manager

```text
CREATOR SESSION

Song: Example

TAKE 01
Lead Shawzin
✓ Recorded

TAKE 02
Harmony
✓ Recorded

TAKE 03
Bass
○ Pending

TAKE 04
Mandachord
○ Pending
```

Optional können Notizen gespeichert werden:

```text
Take 03:
Timingfehler bei 01:42
erneut aufnehmen
```

---

# 40. Count-In

Vor Playback:

```text
3
2
1
GO
```

Konfigurierbar:

```text
1 bar
2 bars
4 beats
Custom
```

---

# 41. Section Recording

Ein Song muss nicht zwingend vollständig abgespielt werden.

Der Benutzer kann Bereiche definieren:

```text
Intro
Verse
Chorus
Solo
Outro
```

und nur einzelne Sektionen aufnehmen.

---

# 42. Mandachord Studio

Mandachord wird als eigenes Modul entwickelt.

Es verwendet dieselbe Timeline sowie Audio- und MIDI-Daten des Projektes.

Keine Mandachord-spezifische Logik darf in Shawzin-Klassen eingebaut werden.

---

# 43. Mandachord Arrangement

Eingabequellen:

```text
Original Audio
Stem
MIDI Track
Shawzin Track
Manual Input
```

Ausgabe:

```text
Percussion
Bass
Melody
```

---

# 44. Mandachord Generator

Workflow:

```text
Selected Music
      ↓
Rhythm Analysis
      ↓
Pitch Analysis
      ↓
Mandachord Reduction
      ↓
Candidate Generation
      ↓
Preview
```

---

# 45. Mandachord-Presets

Mindestens:

**Faithful**

Versucht maximale Ähnlichkeit zum Ausgangsmaterial.

**Recognizable**

Priorisiert Hook und Wiedererkennungswert.

**Gameplay**

Priorisiert ein praktikables Pattern für Octavia.

**Rhythm Focus**

Priorisiert Groove und Percussion.

**Melody Focus**

Priorisiert die charakteristische Melodie.

---

# 46. Mandachord-Vorschläge

VoidNote soll nicht nur genau ein Ergebnis erzeugen.

Beispiel:

```text
MANDACHORD CANDIDATES

A
Similarity 89 %

B
Similarity 84 %

C
Similarity 77 %

[PREVIEW A]
[PREVIEW B]
[PREVIEW C]
```

---

# 47. Mandachord Instrument Sets

Die Datenstruktur muss unterschiedliche Klangsets unterstützen.

Pattern und Klangdefinition werden getrennt gespeichert.

```text
MandachordPattern
≠
MandachordSoundSet
```

Dadurch kann ein Pattern mit mehreren Klangsets vorgehört werden.

---

# 48. Gemeinsame Routing-Funktion

Tracks können zwischen Workspaces übertragen werden.

Kontextmenü:

```text
Send to...

→ MIDI Workshop
→ Shawzin Arranger
→ Mandachord Melody
→ Mandachord Bass
→ Creator Session
```

Beispielsweise:

```text
Bass Stem
→ Transcribe
→ Send to Mandachord Bass
```

---

# 49. Preview-System

Die Anwendung benötigt ein gemeinsames Preview-System.

Abspielbar:

```text
Original
Stem
MIDI
Shawzin
Mandachord
Combined Mix
```

---

# 50. A/B-Vergleich

Beispiel:

```text
[A] Original

[B] Shawzin Arrangement

[SPACE] switch
```

Alternativ gleichzeitig:

```text
Original 50 %
Arrangement 50 %
```

---

# 51. Undo/Redo

Alle zerstörenden Bearbeitungsschritte müssen Undo/Redo unterstützen.

Beispiele:

```text
Note deletion
Quantization
Transpose
Track split
Automatic Shawzin optimization
Mandachord generation
```

AI-Separation selbst muss nicht rückgängig gemacht werden; das Entfernen oder Ersetzen des Ergebnisses hingegen schon.

---

# 52. Autosave

Automatische Projektsicherung.

Konfigurierbar:

```text
1 Minute
5 Minuten
10 Minuten
Aus
```

Autosaves dürfen die Hauptdatei nicht überschreiben.

---

# 53. Crash Recovery

Nach einem Absturz:

```text
VoidNote Studio detected a recovery file.

Project:
MySong

Last autosave:
00:14

[RECOVER]
[DISCARD]
```

---

# 54. Einstellungen

Globale Bereiche:

```text
General
Audio
MIDI
Shawzin
Game Bridge
AI Engines
Storage
Creator
Appearance
Advanced
```

---

# 55. Logging

Strukturiertes Logging.

Stufen:

```text
Trace
Debug
Information
Warning
Error
Critical
```

Logs dürfen keine Audiodateien oder andere Nutzerdaten ungefragt hochladen.

---

# 56. Offline-First

VoidNote Studio soll grundsätzlich lokal funktionieren.

Kein Konto erforderlich.

Keine Cloudpflicht.

Keine Telemetrie ohne ausdrückliches Opt-in.

Audio und MIDI werden standardmäßig lokal verarbeitet.

---

# 57. Externe AI Engines

Falls externe Prozesse verwendet werden, müssen diese über Adapter angebunden sein.

```text
VoidNote
    │
    ▼
IAudioTranscriptionEngine
    │
    ├─ BasicPitchAdapter
    ├─ FutureEngineAdapter
    └─ DisabledAdapter
```

Gleiches gilt für Separation.

---

# 58. Dependency Management

Externe Tools müssen zentral registriert werden.

Beispiel:

```text
Dependency
Version
License
Installed
Available
ExecutablePath
Capabilities
```

VoidNote soll beim Start nicht aufgrund einer fehlenden optionalen AI-Komponente komplett ausfallen.

---

# 59. Plugin-Vorbereitung

Version 1.0 muss noch kein öffentliches Plugin-System besitzen.

Die Architektur soll dieses jedoch ermöglichen.

Mögliche zukünftige Plugins:

```text
Audio transcription engines
Stem separation engines
Music analyzers
Export formats
Shawzin definitions
Mandachord algorithms
```

---

# 60. Performance

Die UI darf während folgender Operationen nicht blockieren:

```text
Stem separation
Audio analysis
Audio-to-MIDI
Large MIDI import
Waveform generation
Song optimization
Export
```

Lange Operationen verwenden:

```text
async
CancellationToken
Progress reporting
```

---

# 61. Background Jobs

VoidNote benötigt einen Job Manager.

```text
JOBS

Stem Separation
██████████████░░ 83 %

Audio → MIDI
Waiting

Waveform Cache
Complete
```

Jobs können abgebrochen werden.

---

# 62. Cache

Zwischenergebnisse wie:

```text
Waveform data
Spectrogram
Separated stems
Pitch analysis
Preview audio
```

sollen gecacht werden.

Der Cache muss löschbar sein.

---

# 63. Dateipfade

Projekte müssen verschiebbar bleiben.

Interne Pfade bevorzugt relativ.

Absolute Dateireferenzen müssen als solche gekennzeichnet werden.

Fehlende Dateien können neu zugeordnet werden:

```text
Source audio missing.

old:
/home/user/music/song.flac

[LOCATE FILE]
```

---

# 64. Fehlerbehandlung

Keine technischen Stacktraces im normalen UI.

Beispiel:

Nicht:

```text
NullReferenceException at Track.cs:182
```

sondern:

```text
Der MIDI-Track konnte nicht geladen werden.

Details anzeigen
```

Details dürfen technische Informationen enthalten.

---

# 65. Accessibility

Mindestens:

```text
Keyboard navigation
Scalable UI
High contrast compatibility
Tooltips
Customizable shortcuts
```

---

# 66. Lokalisierung

Die Anwendung wird von Beginn an lokalisierbar entwickelt.

Keine sichtbaren Texte direkt im C#-Code.

Initial:

```text
Deutsch
Englisch
```

---

# 67. Keyboard Shortcuts

Beispiele:

```text
Space     Play/Pause
Ctrl+S    Save
Ctrl+Z    Undo
Ctrl+Y    Redo
R         Record
Delete    Delete selection
```

Alle relevanten Shortcuts sollen konfigurierbar sein.

---

# 68. Themes

Mindestens:

```text
Dark
Light
System
```

Optional soll ein Warframe-inspiriertes VoidNote-Theme entstehen, ohne geschützte Warframe-Assets direkt zu kopieren.

---

# 69. Copyright und Nutzerinhalte

VoidNote stellt technische Werkzeuge bereit.

Es liefert keine Sammlung urheberrechtlich geschützter Songs mit.

Der Benutzer ist selbst für importierte Audio- und MIDI-Dateien verantwortlich.

---

# 70. Warframe-Markenschutz

VoidNote Studio darf nicht den Eindruck einer offiziellen Digital-Extremes-Anwendung erwecken.

Im About-Dialog:

```text
VoidNote Studio is an independent community project
and is not affiliated with or endorsed by Digital Extremes.
```

Die finale Formulierung ist vor Veröffentlichung rechtlich bzw. anhand aktueller Fan-Content-Richtlinien zu prüfen.

---

# 71. Teststrategie

Tests werden nicht erst nach Fertigstellung ergänzt.

Jedes Kernmodul erhält Unit Tests.

Besonders kritisch:

```text
Songcode Encoding
Songcode Decoding
Timing conversion
MIDI import/export
Quantization
Shawzin mapping
Track splitting
Roundtrip conversion
Project serialization
```

---

# 72. Golden Files

Für Shawzin-Codes werden bekannte gültige Testfälle gespeichert.

```text
fixtures/
shawzin/
│
├─ simple_note.json
├─ chord.json
├─ timing_test.json
├─ long_song.json
└─ known_warframe_codes.json
```

Tests:

```text
Code → Decode → Expected Song
Song → Encode → Expected Code
Code → Decode → Encode
```

---

# 73. MIDI Roundtrip Tests

```text
MIDI
 ↓
Import
 ↓
VoidNote Model
 ↓
Export
 ↓
MIDI
```

Musikalisch relevante Daten müssen erhalten bleiben.

---

# 74. Projektmigration

Das `.vns`-Format besitzt eine Versionsnummer.

```json
{
  "formatVersion": 1
}
```

Spätere Versionen müssen ältere Projekte migrieren können.

Kein stilles Überschreiben eines Projektes nach Migration ohne Sicherung.

---

# 75. Continuous Integration

CI muss mindestens Builds und Tests für folgende Systeme durchführen:

```text
Windows x64
Linux x64
```

Optional:

```text
Linux ARM64
Windows ARM64
```

---

# 76. Release-Formate

Windows:

```text
Installer
Portable ZIP
```

Linux:

mindestens ein einfach distributierbares Paket plus portable Variante.

Die konkrete Distributionstechnik wird vor Release anhand der dann aktuellen Avalonia/.NET-Packaging-Möglichkeiten festgelegt.

---

# 77. Modularer Entwicklungsplan

Obwohl alle Funktionen zu **VoidNote Studio 1.0** gehören, erfolgt die Entwicklung nicht gleichzeitig.

## Milestone A – Foundation

```text
Solution
Avalonia
MVVM
Project format
Timeline
Settings
Logging
Undo/Redo
```

## Milestone B – MIDI Core

```text
MIDI import
MIDI export
Piano Roll
Playback
Recording model
```

## Milestone C – Shawzin Codec

```text
Shawzin model
Code decoder
Code encoder
Roundtrip validation
Known-code fixtures
```

## Milestone D – Shawzin Composer

```text
Virtual Shawzin
MIDI mapping
Compatibility analyzer
Arranger
Preview
```

## Milestone E – Game Bridge

```text
Keybind profiles
Windows bridge
Linux bridge
Scheduler
Timing tests
Safety warning
```

## Milestone F – Multi-Shawzin

```text
Voice separation
Track splitting
Arrangement presets
Creator track management
```

## Milestone G – Audio Lab

```text
MP3
FLAC
WAV
Waveform
Playback
Analysis
```

## Milestone H – AI Audio

```text
Stem separation adapter
Audio-to-MIDI adapter
Confidence system
Job management
```

## Milestone I – Creator Mode

```text
Take Manager
Count-in
Sync markers
Section recording
Multitrack workflow
```

## Milestone J – Mandachord

```text
Mandachord data model
MIDI reduction
Beat analysis
Melody analysis
Bass analysis
Candidate generation
Preview
```

## Milestone K – Polish

```text
Localization
Recovery
Packaging
Performance
Accessibility
Documentation
```

Only after all milestones meet their acceptance criteria is the product labeled:

```text
VOIDNOTE STUDIO 1.0
```

---

# 78. Definition of Done – Shawzin

Shawzin-Unterstützung gilt als fertig, wenn:

```text
✓ bekannte Songcodes importiert werden
✓ erzeugte Codes wieder eingelesen werden können
✓ MIDI in Shawzin-Spuren konvertiert werden kann
✓ unspielbare Noten erkannt werden
✓ automatische Optimierung funktioniert
✓ mehrere Shawzin-Spuren unterstützt werden
✓ Songcodes kopiert/exportiert werden
✓ virtuelle Wiedergabe funktioniert
✓ Timing stabil ist
```

---

# 79. Definition of Done – Audio

```text
✓ MP3 importierbar
✓ WAV importierbar
✓ FLAC importierbar
✓ Waveform verfügbar
✓ Stems erzeugbar
✓ Stems einzeln abspielbar
✓ Stem → MIDI möglich
✓ erkannte Noten editierbar
✓ Confidence sichtbar
```

---

# 80. Definition of Done – MIDI

```text
✓ Import
✓ Export
✓ Multitrack
✓ Editing
✓ Recording
✓ Quantization
✓ Shawzin conversion
✓ Audio transcription integration
```

---

# 81. Definition of Done – Creator Mode

```text
✓ mehrere Instrumentenspuren
✓ einzelne Takes
✓ gemeinsamer Startpunkt
✓ Count-in
✓ Sync-Signal
✓ Sektionen
✓ Take Notes
✓ getrennte Shawzin-Codes
```

---

# 82. Definition of Done – Mandachord

```text
✓ Musikquelle auswählbar
✓ Melody-Vorschlag
✓ Bass-Vorschlag
✓ Percussion-Vorschlag
✓ mehrere Kandidaten
✓ Preview
✓ manuelle Bearbeitung
✓ gemeinsame Projekt-Timeline
```

---

# 83. Qualitätsziel

VoidNote Studio darf niemals davon ausgehen, dass eine automatische Musiktranskription perfekt ist.

Das Grundprinzip lautet:

```text
AUTOMATE
     ↓
PREVIEW
     ↓
EDIT
     ↓
VALIDATE
     ↓
EXPORT
```

Automatik unterstützt den Benutzer.

Sie ersetzt nicht die Möglichkeit zur manuellen Bearbeitung.

---

# 84. Zentrales Architekturprinzip

Das wichtigste technische Prinzip des gesamten Projekts:

```text
IMPORT FORMAT
      ↓
NORMALIZED VOIDNOTE MODEL
      ↓
OUTPUT FORMAT
```

Nicht:

```text
MIDI → Shawzin-specific hack

MP3 → different hack

Shawzin Code → third hack
```

sondern:

```text
              Audio
                │
MIDI ───────────┤
                ▼
        VOIDNOTE MUSIC MODEL
                │
Shawzin Code ───┤
                │
                ├────► MIDI
                ├────► Shawzin
                ├────► Shawzin Code
                ├────► Game Bridge
                └────► Mandachord
```

Dadurch bleibt die Software langfristig erweiterbar.

---

# 85. Codex-Entwicklungsregeln

Codex soll bei der Entwicklung folgende Regeln zwingend beachten:

1. Keine riesigen Klassen.
2. Keine Businesslogik in Views.
3. Keine Game-Input-Logik im Shawzin-Modell.
4. Keine direkte Abhängigkeit des Domain-Projekts von Avalonia.
5. Keine direkte Abhängigkeit des Domain-Projekts von AI/Python.
6. Interfaces an Modulgrenzen verwenden.
7. Dateiformate versionieren.
8. öffentliche APIs dokumentieren.
9. Unit Tests für Musiktransformationen schreiben.
10. keine Features implementieren, indem bestehende Architektur umgangen wird.
11. keine Warframe-Prozessmanipulation.
12. keine fest einprogrammierten Benutzer-Keybinds.
13. keine stillen Datenverluste.
14. automatische Transformationen müssen nachvollziehbar und möglichst rückgängig machbar sein.
15. Plattformcode strikt isolieren.
16. Fehler verständlich anzeigen.
17. alle langen Operationen abbrechbar machen.
18. keine unnötige Netzwerkabhängigkeit.
19. optionale Komponenten dürfen den Programmstart nicht verhindern.
20. vor größeren Architekturänderungen bestehende Tests ausführen.

---

# 86. Langfristige Erweiterbarkeit

Die Architektur soll spätere Funktionen ermöglichen, ohne dass diese Bestandteil von 1.0 sein müssen.

Denkbare Erweiterungen:

```text
Community Song Library
Arrangement Sharing
Custom Shawzin Profiles
Additional transcription engines
Advanced source separation
Chord recognition
Automatic key detection
Sheet music export
MusicXML
DAW integration
VST/MIDI bridge
Collaborative projects
Video sync export
OBS integration
Additional Warframe music systems
```

---

# 87. Erfolgskriterium von VoidNote Studio

VoidNote Studio 1.0 erfüllt seine Produktvision, wenn ein Benutzer folgenden vollständigen Workflow innerhalb eines Projektes durchführen kann:

```text
song.flac
   ↓
VoidNote Studio
   ↓
Stem Separation
   ↓
Lead / Bass / Drums / Other
   ↓
Audio-to-MIDI
   ↓
Manual Cleanup
   ↓
Arrangement
   ↓
┌────────────┬────────────┬────────────┬────────────┐
│ Shawzin A  │ Shawzin B  │ Shawzin C  │ Mandachord│
└─────┬──────┴─────┬──────┴─────┬──────┴─────┬──────┘
      ↓            ↓            ↓            ↓
   Code A        Code B        Code C       Pattern
      ↓            ↓            ↓
 Ingame Take   Ingame Take   Ingame Take
      │            │            │
      └────────────┼────────────┘
                   ↓
             Creator Export
                   ↓
              Videoschnitt
```

Gleichzeitig muss auch der wesentlich einfachere Workflow möglich sein:

```text
MIDI laden
   ↓
Shawzin auswählen
   ↓
Auto Arrange
   ↓
Preview
   ↓
Copy Song Code
```

sowie:

```text
Shawzin spielen
   ↓
Record
   ↓
Edit
   ↓
Export MIDI
```

und:

```text
fremden Shawzin-Code einfügen
   ↓
Preview
   ↓
Edit
   ↓
MIDI exportieren
```

Damit dient VoidNote Studio sowohl Einsteigern als auch Musikern und Content Creatorn.

---

# 88. Produktdefinition

**VoidNote Studio ist eine plattformübergreifende Warframe Music Creator Suite zur Konvertierung, Bearbeitung, Komposition und Produktion von Musik für Shawzin und Mandachord.**

Der Schwerpunkt liegt auf einem gemeinsamen, formatunabhängigen Musikmodell, aus dem Audio-, MIDI-, Shawzin- und Mandachord-Workflows kombiniert werden können.

VoidNote Studio soll bestehende Einzweck-Tools nicht lediglich ersetzen.

Es soll ihre Funktionen in einer modernen, modularen und langfristig erweiterbaren Produktionsumgebung zusammenführen.

**Create. Convert. Perform.**

**VoidNote Studio.**
