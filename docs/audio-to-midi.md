# Audio to MIDI

## Erste Engine

Der erste Adapter verwendet **Spotify Basic Pitch 0.4.0**. Das offizielle Projekt bietet lokale, polyphone Audio-to-MIDI-Erkennung, Windows/Linux-Unterstützung und mehrere Runtimeformate; Lizenz ist Apache 2.0. Die letzte stabile Referenzveröffentlichung stammt vom August 2024. Die vergleichsweise alte Releasefrequenz ist ein Grund für die strikte Adapterkapselung. Quelle: <https://github.com/spotify/basic-pitch>.

Basic Pitch ist keine perfekte Transkription und kein Drum-to-MIDI-Modell. Drum-Stems werden ausdrücklich als nicht unterstützt abgelehnt. `Auto`, `Monophonic`, `Polyphonic` sind Domainmodi. Monophonic reduziert überlappende Detektionen transparent anhand höherer Confidence; ein spezialisiertes monophones Modell kann später denselben Port implementieren.

## Confidence und Provenienz

Roh-Confidence bleibt `0..1`. Defaults:

- High: `>= 0.85`
- Medium: `>= 0.60`
- Low: `< 0.60`

Filtermodi sind Keep All, Hide Low, Remove Low und Minimum Threshold. Hide Low entfernt keine Daten. Jede tatsächliche Entfernung erzeugt einen `TranscriptionChange`. `MusicalEvent.AudioProvenance` speichert SourceAudio/Stem, Engine/Version, Rohwert, Klasse, EditStatus sowie OriginalStart/-Duration. Manuelles Editieren kann den Status auf `UserModified`/`UserConfirmed` setzen, ohne Herkunft zu vernichten.

## Timing und Cleanup

Engine-Sekunden werden relativ zum Sourcefenster auf den Master-Offset addiert und erst dann über die bestehende Tempo Map in Ticks konvertiert. Optionale Raster: keine, 1/4, 1/8, 1/16, 1/32, Achtel- und Sechzehnteltriolen. Quantisierung verändert die Rohwerte in der Provenienz nicht.

Optionale, konservative Schritte entfernen sehr kurze Ghost Notes, deduplizieren identische Pitch/Start-Erkennungen, verbinden gleiche aufeinanderfolgende Pitches innerhalb eines kleinen Gap und markieren Ausreißer über zwei Oktaven vom Median. Markieren verwirft nichts. Der Report enthält Counts, Confidence, Dauer, Pitchrange, Dichte, Engine, Laufzeit, Settings und jede Änderung.

Das Resultat ist ein normaler `MidiTrack`. Damit funktionieren Piano-Roll-Projektion, Compatibility Analyzer, Shawzin Arranger und Multi-Shawzin Splitter ohne AI-spezifischen Sonderpfad.
