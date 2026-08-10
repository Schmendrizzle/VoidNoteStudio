# VoidNote Studio Quick Start

## MIDI → Shawzin → code

1. Open **Project**, choose **New Project**, then save a `.vns` file.
2. Open **Shawzin Studio** and select **Open MIDI**.
3. Select a MIDI track, instrument, scale and arrangement strategy.
4. Choose **Analyze**, review compatibility and conflicts, then choose **Arrange**.
5. Save or listen to the synthetic preview.
6. Copy the generated code and run **Decode / Validate / Re-Encode** before using it.
7. Save the project. Automatic changes remain visible in the arrangement report.

## MP3/FLAC → stems → MIDI → Shawzin

1. Configure FFmpeg and the optional Python worker in **Settings**, then run **Refresh / Recheck**.
2. In **Audio Lab**, import MP3 or FLAC. WAV works without FFmpeg.
3. Select a region if desired and run **Separate**. Demucs is optional and must be installed manually.
4. Select a stem and run **Transcribe to MIDI**. Review confidence and timing; Basic Pitch results are suggestions.
5. Correct uncertain notes before sending the normalized MIDI track to a Shawzin workflow.
6. Analyze, arrange, preview and validate the Shawzin code as in the first workflow.

Missing optional dependencies should produce an unavailable state, not prevent project, MIDI, Shawzin or Mandachord use.
