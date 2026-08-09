# AGENTS.md — VoidNote Studio

## Projektgrundlage

Lies vor jeder größeren Implementierung zuerst `VoidNOTE-Spec.md`.

Das Pflichtenheft ist die verbindliche Produkt- und Architekturgrundlage für VoidNote Studio.

Wenn eine Aufgabe dem Pflichtenheft widerspricht, weise darauf hin, bevor du die Architektur eigenständig änderst.

## Technologie

* C#
* .NET
* Avalonia UI
* Windows und Linux
* MVVM
* modulare Clean Architecture

## Architekturregeln

* Keine Businesslogik in Views.
* Das Domain-Projekt darf nicht von Avalonia abhängen.
* Plattformabhängiger Code muss isoliert bleiben.
* Windows- und Linux-spezifische Implementierungen gehören hinter Interfaces.
* Audio-, MIDI-, Shawzin-, Mandachord- und GameBridge-Module müssen voneinander getrennt bleiben.
* Gemeinsame Musikdaten werden über das zentrale VoidNote-Datenmodell ausgetauscht.
* Keine unnötigen direkten Abhängigkeiten zwischen Modulen.
* Dependency Injection verwenden, wo dies sinnvoll ist.
* Keine riesigen God Classes.
* Keine Methoden wie `DoEverythingAsync`.
* Kleine, klar verantwortliche Klassen bevorzugen.

## Warframe-Sicherheitsregeln

Unter keinen Umständen:

* DLL Injection
* Memory Reading
* Memory Writing
* Process Injection
* Manipulation des Warframe-Prozesses
* Manipulation von Spieldateien
* Anti-Cheat-Umgehung
* Netzwerk-/Packet-Manipulation
* automatisiertes Gameplay außerhalb der vorgesehenen Musikfunktionen

Die GameBridge darf ausschließlich über abstrahierte normale Benutzereingaben arbeiten.

## Entwicklungsreihenfolge

Arbeite die Milestones aus `VoidNOTE-Spec.md` grundsätzlich in der dort definierten Reihenfolge ab.

Implementiere keine Features späterer Milestones nur deshalb, weil sie bereits im Pflichtenheft beschrieben sind.

Aktueller erster Entwicklungsabschnitt:

**Milestone A — Foundation**

## Qualität

Vor Abschluss einer Aufgabe:

1. Projekt kompilieren.
2. vorhandene Tests ausführen.
3. Compiler-Warnungen prüfen.
4. keine bekannten Fehler verschweigen.
5. Änderungen kurz dokumentieren.

Für wichtige Musiktransformationen und Codecs müssen Unit Tests erstellt werden.

## Datenintegrität

* Keine stillen Datenverluste.
* Automatische Transformationen müssen nachvollziehbar sein.
* Bearbeitungen sollen nach Möglichkeit Undo/Redo unterstützen.
* Projektdateien müssen versionierbar sein.
* Migrationen dürfen vorhandene Projekte nicht ohne Sicherung überschreiben.

## Externe Abhängigkeiten

Externe Bibliotheken nicht unnötig hinzufügen.

Vor Aufnahme einer größeren Dependency:

* Zweck prüfen
* Lizenz berücksichtigen
* Wartungsstatus berücksichtigen
* Abhängigkeit hinter einer eigenen Abstraktion kapseln, wenn sie für die Architektur relevant ist

Optionale AI-/Audio-Komponenten dürfen den normalen Programmstart nicht verhindern.

## Arbeitsweise

Bei größeren Aufgaben:

1. relevante bestehende Dateien untersuchen
2. kurze technische Umsetzung planen
3. implementieren
4. testen
5. Ergebnis und offene Punkte zusammenfassen

Bestehende Architektur nicht ohne triftigen Grund vollständig ersetzen.

Wenn eine grundlegende Architekturänderung sinnvoll erscheint, zuerst begründen und bestehende Auswirkungen untersuchen.
