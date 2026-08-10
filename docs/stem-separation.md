# Stem Separation

## Engine-Entscheidung

Der erste Adapter zielt auf **Demucs 4.1.x**. Das ursprüngliche Meta-Repository ist archiviert, die offizielle PyPI-Veröffentlichung 4.1.0 vom Juli 2026 wird jedoch wieder gepflegt, verlangt Python 3.10+ und steht unter MIT. Der C#-Adapter prüft die Laufzeitversion zur Discovery und koppelt weder Protokoll noch Domain an eine konkrete Demucs-API-Version. Quellen: <https://pypi.org/project/demucs/> und <https://github.com/facebookresearch/demucs>.

Die Alternative `python-audio-separator` unterstützt mehr UVR-/MDX-/Demucs-Modelle, bringt aber eine größere Modell-/Lizenzmatrix. Deshalb bleibt sie eine spätere Adapteroption, nicht die erste Referenz.

## Daten und Workflow

```text
AudioSource oder AudioRegion
  → SeparationRequest
  → Demucs Worker
  → Vocals/Bass/Drums/Other WAV
  → Probe durch bestehenden Decoder
  → AudioSource + AudioTrack je Stem
  → StemSet im Projekt
```

`StemType.Custom` plus `CustomType` erlaubt Guitar, Piano, Strings, Backing Vocals und künftige Enginekategorien. `StemSet` speichert Quelle, Engine/Version, Zeitpunkt, Settings und ProcessingMetadata. Jeder `Stem` speichert eigene ID, AudioSource, Rolle, Dauer, Master-Offset und vollständige Provenienz.

Regionen werden vor der Engine lokal mit FFmpeg ausgeschnitten. SourceOffset (Datei) und StartOffset (Master-Timeline) sind getrennt; das erzeugte Stem-Clip beginnt wieder am korrekten Masterzeitpunkt. Originale werden nur gelesen.

## Preview und Wiederholung

Stems sind normale AudioTracks und erben Gain, Active, Solo und Mute. A wählt das Original, B den ausgewählten Stem. Die leichte Mix-Vorschau startet alle hörbaren Stems gegen dieselbe monotone Uhr. Sie ist für Kontrolle, nicht für samplegenaues DAW-Mixing gedacht. Ein neuer Lauf erzeugt einen neuen StemSet; vorhandene Resultate und Originale werden nicht still ersetzt.
