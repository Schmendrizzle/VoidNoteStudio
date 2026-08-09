# VoidNote Studio – Architekturstand Milestone A

## Geltungsbereich

Dieses Dokument beschreibt die tatsächlich implementierte Architektur nach **Milestone A – Foundation**. Der Stand enthält bewusst keine Funktionslogik für MIDI, Audio, Shawzin, Mandachord, GameBridge oder Creator Mode. Die entsprechenden Projekte sind ausschließlich unabhängige Modulgrenzen für spätere Milestones.

## Technische Basis

- .NET 10 LTS, zentral in `Directory.Build.props` und `global.json` festgelegt
- C# 14 mit aktivierten Nullable Reference Types
- Avalonia 12.1 für die gemeinsame Windows-/Linux-Desktopanwendung
- Microsoft Extensions für Dependency Injection und Logging
- xUnit für Foundation- und Integrationstests
- zentrale Paketversionen in `Directory.Packages.props`

## Projekte und Verantwortlichkeiten

| Projekt | Verantwortung in Milestone A | Direkte Projektabhängigkeiten |
| --- | --- | --- |
| `VoidNote.Domain` | Normalisiertes Projekt- und Musikmodell, Master-Timeline, Domain-Invarianten | keine |
| `VoidNote.Application` | Ports für Settings und Projektpersistenz, UI-unabhängige Undo/Redo-Steuerung | Domain |
| `VoidNote.Infrastructure` | JSON-Settings, ZIP-basiertes `.vns`-Format, lokale JSON-Logs, Anwendungspfade | Application, Domain |
| `VoidNote.App` | Avalonia-Host, Composition Root, DI-Registrierung, MVVM-Shell | Application, Infrastructure |
| `VoidNote.Audio` | reservierte Modulgrenze; keine Audiofunktion implementiert | keine |
| `VoidNote.Midi` | reservierte Modulgrenze; kein MIDI-Import/-Export implementiert | keine |
| `VoidNote.Shawzin` | reservierte Modulgrenze; kein Codec oder Playback implementiert | keine |
| `VoidNote.Mandachord` | reservierte Modulgrenze; keine Arrangementlogik implementiert | keine |
| `VoidNote.GameBridge` | reservierte Modulgrenze; keine Eingabesimulation implementiert | keine |
| `VoidNote.PluginContracts` | reservierte Assembly-Grenze; kein öffentliches Plugin-System implementiert | keine |

Die sechs im Pflichtenheft vorgesehenen Testprojekte wurden angelegt. `VoidNote.Domain.Tests` prüft die Foundation-Domainlogik. `VoidNote.IntegrationTests` prüft Application- und Infrastructure-Zusammenspiel. Die übrigen modulspezifischen Testprojekte bleiben bis zum jeweiligen Milestone ohne Featuretests.

## Abhängigkeitsrichtung

```text
VoidNote.App
    ├──> VoidNote.Application ───> VoidNote.Domain
    └──> VoidNote.Infrastructure ─> VoidNote.Application
                                └─> VoidNote.Domain

spätere Featuremodule ───> derzeit keine Abhängigkeiten
VoidNote.Domain ─────────> keine externen oder UI-/Plattformabhängigkeiten
```

Die UI kennt konkrete Infrastructure-Typen nur im Composition Root. Views enthalten ausschließlich Darstellung. ViewModels und Application-Services enthalten keine Avalonia-Typen. Betriebssystemnahe Pfadermittlung und Dateizugriffe liegen in Infrastructure.

## Zentrales Projektmodell

`VoidNoteProject` ist die versionierte Wurzel des normalisierten Modells (`formatVersion = 1`). Es enthält Metadaten, eine gemeinsame Timeline sowie typisierte Sammlungen für Audioquellen, Stems, MIDI-, Shawzin- und Mandachord-Spuren und Creator-Sessions. Diese Typen stellen nur die gemeinsame erweiterbare Datenbasis dar; sie implementieren keine späteren Workflows.

`ProjectTimeline` verwendet ganzzahlige Ticks mit projektweiter Auflösung und eine geordnete Tempomap. Konvertierungen zu `AbsoluteTime` rechnen mit Dezimalsekunden und berücksichtigen Tempoänderungen. Gerundete Millisekunden sind nicht die primäre Speicherung.

Musikalische Events tragen stabile IDs, Start, Dauer, Pitch, Velocity, Herkunft und Confidence. Die Herkunft bleibt dadurch über spätere automatische und manuelle Transformationen nachvollziehbar.

## Persistenz und Datenintegrität

- `.vns` ist ein ZIP-Container mit `project.json` als versioniertem Manifest.
- Speichervorgänge schreiben zuerst eine temporäre Datei und ersetzen das Ziel erst nach erfolgreicher Serialisierung.
- Settings liegen versioniert als lokales JSON vor und werden ebenfalls atomar ersetzt.
- Relative und absolute Projektpfade werden im Domain-Modell ausdrücklich unterschieden.
- Migration, Autosave und Crash Recovery sind noch nicht implementiert; unbekannte Format- oder Settings-Versionen werden abgelehnt.

## Logging

Die Anwendung nutzt `Microsoft.Extensions.Logging`. Der Composition Root registriert Konsolenlogging und einen lokalen JSON-Lines-Provider mit den standardisierten Logstufen Trace bis Critical. Es gibt keine Telemetrie und keinen Netzwerktransport. Die Logdateien werden unter dem vom Betriebssystem gelieferten lokalen Anwendungsdatenpfad gespeichert.

## Undo/Redo

`IUndoableCommand` und `IUndoRedoService` bilden eine UI-unabhängige lineare Command-Historie. Neue Befehle löschen den Redo-Zweig. Ein Befehl wird erst nach erfolgreicher Ausführung beziehungsweise Rücknahme zwischen den Stacks verschoben, sodass fehlgeschlagene Operationen die Historie nicht verfälschen.

## Dependency Injection

`CompositionRoot` in `VoidNote.App` ist der einzige Ort, an dem konkrete Infrastructure-Implementierungen den Application-Interfaces zugeordnet werden. Registriert sind Settings, Projektpersistenz, Undo/Redo, Pfade, Logging und die initiale ViewModel-Erzeugung.

## Bewusst offene Punkte nach Milestone A

- Noch keine Migration älterer `.vns`-Versionen; Version 1 ist das einzige unterstützte Format.
- Noch keine Autosave-/Recovery-Strategie.
- Noch keine Lokalisierungsinfrastruktur; sichtbare Texte liegen jedoch bereits in XAML-Ressourcen und nicht im C#-Code.
- Linux wird über Avalonia Desktop und plattformneutrale .NET-APIs unterstützt; eine native Linux-Ausführung ist Bestandteil der künftigen CI-/Releaseumgebung.
- Die CI-Matrix baut und testet die Solution auf Windows und Ubuntu; die lokale Abschlussprüfung dieses Milestones erfolgte auf Windows.
- Die reservierten Featureassemblies enthalten absichtlich noch keine fachlichen Schnittstellen oder Implementierungen, da diese erst mit ihrem jeweiligen Milestone konkretisiert werden.
