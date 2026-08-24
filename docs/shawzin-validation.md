# Reale Shawzin-Mappingvalidierung

## Automatischer 12-Noten-Test

Im Shawzin Studio Instrument und Skala wählen, das Validierungswerkzeug öffnen und **Testsequenz erzeugen** anklicken. VoidNote zeigt für alle zwölf Positionen Index, MIDI-Pitch, Notenname, Saite, Fret und Codesymbol. Gleichzeitig entsteht ein Songcode mit 0,5 Sekunden Abstand zwischen den Positionen; er wird in das Codefeld übernommen. Diese langsamere Geschwindigkeit ist die bevorzugte manuelle Fixture, während automatische Codec-Tests weiterhin kürzere Zeitwerte verwenden dürfen.

Für Chromatic lautet die kanonische manuelle Fixture:

```text
3BAACAIEAQJAYKAgMAoRAwSA4UBAhBIiBQkBY
```

## Manueller Warframe-Ablauf

1. In Warframe exakt dieselbe Shawzin und Skala auswählen.
2. Den generierten Code über **Load Song To Memory** laden.
3. Auto Play aktivieren und die zwölf Töne langsam anhören oder extern mit einem Tuner prüfen.
4. Kontrollieren, dass die Folge in der angezeigten Positionsreihenfolge Open 1–3, Sky 1–3, Earth 1–3, Water 1–3 erklingt.
5. In VoidNote die Validierung als bestätigt oder nicht bestätigt lokal speichern und eine Abweichung mit Position, erwartetem Ton und gehörtem Ton notieren.

Chromatic muss C4 bis B4 lückenlos aufsteigend spielen. Bei den anderen Skalen ist die im Werkzeug angezeigte reale Oktavfolge maßgeblich. Der Test verwendet nur Songcode-Import und normale Shawzin-Wiedergabe; es findet keine Prozess-, Speicher-, Datei- oder Netzwerkanalyse von Warframe statt.

## Aufsteigende Kontrollfolge

Der Validierungscode durchläuft die physische Positionsreihenfolge. Diese ist bei allen eingebauten Skalen zugleich aufsteigend. Die Golden Fixtures prüfen Profilpitch, String/Fret, Codesymbol, Encode/Decode und Rückrekonstruktion offline, bevor eine manuelle Bestätigung gespeichert werden kann.

## Dynamischer Zwei-Skalen-Test

Nach erfolgreichem Fixed-Test eine synthetische MIDI-Fixture mit zwei deutlich getrennten Pitchabschnitten laden, **Dynamic Scale Playback** wählen und analysieren. Im Dry Run zuerst die geforderte Initialskala manuell in Warframe einstellen. Prüfen, dass die angezeigte Zahl der Scale-Select-Tastendrücke exakt im Pausenfenster erfolgt, danach die zweite Phrase in der Zielskala erklingt und die synthetische Dynamic Preview dieselben resultierenden Pitches besitzt. Ein Fixed-Fallback-Code darf kopierbar sein, sobald wieder **Share Code Mode** gewählt wird; für den Dynamic-Plan selbst existiert kein Songcode.
