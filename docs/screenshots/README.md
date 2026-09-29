# Screenshot provenance

Captured from the running Windows Forms application on 2026-09-29 using Windows Computer Use. These are unretouched app screenshots, not generated concept art.

| Image | Visible state |
| --- | --- |
| `transcription.jpg` | Restored reference project, waveform, eight analyzed notes, fingering and chord suggestions |
| `tablature.jpg` | Six-string tablature with onset/duration labels and chord diagrams |
| `playback.jpg` | With-chords playback, active playhead, note/fret and chord highlighting |

Input: the generated clean sequence C4, D4, E4, G4, A4, G4, E4, C4 from `tests/MelodyToTab.Checks/Program.cs`. The production analyzer creates the notes; the test round trip locks the first fingering to string 2, fret 1. The saved verification project uses C major and 100 BPM for consistent arrangement checks. These settings are not claimed as automatic key/tempo ground truth.

To reproduce: run the checks executable, open `artifacts/known-pitches.mtab`, maximize the application, capture Transcription, switch to Guitar tablature, and start With chords playback. Use only generated audio or recordings you have permission to publish. The screenshots make no claim about live microphone validation or human-voice accuracy.
