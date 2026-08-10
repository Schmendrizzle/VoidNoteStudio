# Audio fixtures

`synthetic-sine.flac` and `synthetic-sine.mp3` contain only a 100 ms, 440 Hz synthetic sine at 22.05 kHz. They were generated locally with FFmpeg and contain no music or third-party recording.

Mono/stereo WAV, silence, impulse, alternate sample-rate and long-file fixtures are generated programmatically by `AudioFixtureFactory` for each offline test run. Compressed-format import tests use the valid FLAC/MP3 files with a deterministic decoder test double so CI never needs FFmpeg or a physical audio device. The production FFmpeg adapter remains independently optional.
