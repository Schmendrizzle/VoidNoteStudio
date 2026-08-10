# GameBridge und Ingame-Shawzin-Playback

## Zweck und harte Sicherheitsgrenze

Die optionale GameBridge bildet bereits arrangierte `ShawzinEvent`-Anschläge auf normale, vom Betriebssystem erzeugte Tastaturereignisse ab. Sie öffnet, liest, schreibt, scannt, patcht oder injiziert keinen Warframe-Prozess. Es gibt keine DLL-/Code-/Process-Injection, kein Memory Reading/Writing, keine Datei- oder Packet-Manipulation, keine Hooks in Warframe und keine Combat-, Movement-, AFK- oder Missionsautomatisierung.

Die GameBridge ist vollständig optional. Ist ein Backend nicht verfügbar oder wird die Bridge nicht ausdrücklich armed, bleiben Komposition, MIDI, Arrangement, Preview und Songcode unverändert nutzbar.

## Architektur

```text
ShawzinTrack
  → ShawzinPlaybackEngine (ein absoluter monotonic-clock-Anker)
  → GameBridgePlaybackOutput (Fokus, Timing, Release-All, Diagnostik)
  → ShawzinInputMapper (Event → portable Action)
  → IGameInputBridge (portable Taste → Plattformadapter)
```

`IGameInputBridge` bietet Press, Release, Tap, mehrere gleichzeitig gehaltene Tasten, Cancellation, Capability und Release-All. Seine `GameInputKey`-Werte sind portable Namen und keine Warframe- oder Betriebssystem-Keycodes. Domain und Shawzin kennen keine nativen APIs.

## Plattformen

### Windows

`WindowsGameInputBridge` verwendet ausschließlich die dokumentierte Win32-Funktion `SendInput` für normale Keyboard-Events. Eine Ziel-Fokusprüfung liest nur den Titel des aktuell im Vordergrund befindlichen Fensters. Weder Prozesshandles noch Speicherzugriffe werden verwendet.

### Linux

Unter einer klassischen X11-Sitzung verwendet `LinuxGameInputBridge` Xlib plus die dokumentierte XTest-Erweiterung. Die Bibliotheken und `DISPLAY` müssen in der normalen Benutzersitzung verfügbar sein; Root-Rechte sind nicht erforderlich. Der fokussierte X11-Fenstertitel kann vor jedem Event geprüft werden.

Unter Wayland gibt es absichtlich keinen globalen synthetischen Input-Workaround. Das Backend meldet transparent „nicht verfügbar“. Compositor-spezifische Remote-Desktop-Portale oder privilegierte `uinput`-Lösungen werden nicht automatisch verwendet. Headless-Sitzungen, fehlendes XTest und nicht zugängliche Displays sind ebenfalls nicht verfügbar.

## Keybind-Profile und Validierung

Profile besitzen stabile ID, Name und Bindungen für String 1–3, Fret Left/Middle/Right sowie optional Neutral. Das mitgelieferte Profil heißt ausdrücklich „Default Warframe layout“; es ist nur ein bearbeitbarer Ausgangspunkt. Profile können geladen, gespeichert, dupliziert, aktualisiert und gelöscht werden. Die Datei `gamebridge-profiles.json` ist lokal, versioniert und wird atomar ersetzt.

Vor Playback werden Profilname, ID, alle Pflichtbindungen, unterstützte portable Tastennamen und Konflikte geprüft. Es gibt keine stillen Ersatzbelegungen. Derselbe Key darf nicht zwei gleichzeitig benötigte Aktionen repräsentieren.

## Timing

Sichere Defaults sind `KeyDownLead = 5 ms`, `HoldDuration = 25 ms` und `ReleaseDelay = 5 ms`. Sie liegen zentral in `GameBridgeTimingOptions` beziehungsweise den globalen Settings. Der bestehende Shawzin-Scheduler berechnet jedes Event weiterhin unabhängig gegen den ursprünglichen absoluten Anker. Hold-/Release-Verarbeitung erzeugt deshalb keine fortlaufend addierte Sleep-Zeit; bei Überlastung erscheint die Abweichung in den lokalen Diagnosen.

## Arm/Disarm, Fokus und Stop

Der Startzustand ist `DISARMED`. Vor dem ersten Arm muss der Benutzer den Drittsoftware-Hinweis bestätigen. Erst ein ausdrückliches Arm erlaubt reales Playback. Normaler Stop, Playback-Ende, Fokusverlust, Inputfehler, Emergency Stop und Anwendungsschließen geben alle von VoidNote gehaltenen Tasten frei und wechseln zurück zu `DISARMED`.

Standardmäßig muss der konfigurierte Zielfenstertitel im Vordergrund sein. Ist eine zuverlässige Prüfung nicht verfügbar, startet realer Input bei aktivierter Fokuspflicht nicht. Das persistierte Verhalten ist `Abort` (sicherer Default) oder `Ignore`; eine automatische Shawzin-Modus-Erkennung gibt es nicht. Die Anwendung bietet einen jederzeit sichtbaren Emergency-Stop. Ein systemweiter globaler Hotkey ist noch eine offene plattformspezifische Entscheidung.

## Diagnostic Mode und Dry Run

`DiagnosticGameInputBridge` sendet niemals reale Eingaben. Es protokolliert Taste, KeyDown/KeyUp, lokalen Timestamp und Event-ID. Dry Run validiert das Profil und führt denselben Mapping-/Playbackpfad gegen diese Bridge aus. Ergebnisdaten umfassen Eventzahl, Inputzahl, Mappingfehler, ungeklärte Bindungen, geplante Zeit, tatsächliche Dispatch-Zeit, Abweichung, abgebrochene Events, Fokusverluste und Emergency Stops. Es gibt keine Telemetrie und keinen Upload.

## Fehler und Shutdown

Jeder Input- oder Fokusfehler beendet Playback, versucht unabhängig vom ursprünglichen Cancellation-Token `ReleaseAllAsync`, setzt den Transport zurück und disarmed. Technische Fehler werden über das bestehende lokale Logging erfassbar; im UI erscheint eine verständliche Meldung. Shutdown ruft denselben Stop-/Release-Pfad auf.

## Drittsoftware-Hinweis

Digital Extremes erklärt in der aktuellen offiziellen Support-Richtlinie [„Third-Party Software and You“](https://support.warframe.com/hc/en-us/articles/360030014351-Third-Party-Software-and-You), dass externe Software zusammen mit Warframe auf eigenes Risiko verwendet wird und keine dauerhaft verlässliche Positivliste existiert. Daraus folgt **keine** Freigabe, Empfehlung oder Risikofreiheit für VoidNote. VoidNote ist ein unabhängiges Community-Projekt und weder mit Digital Extremes verbunden noch von Digital Extremes unterstützt.

Die Richtlinie kann sich ändern. Nutzer müssen die aktuelle Fassung und die für ihr Konto geltenden Bedingungen selbst prüfen. Die vollständig risikovermeidende Option hinsichtlich Drittsoftware ist, reales Ingame-Playback nicht zu verwenden und bei Diagnostic Mode, Preview oder Songcode-Export zu bleiben.
