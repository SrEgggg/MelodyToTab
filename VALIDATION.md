# Validation record — 2026-09-29

Environment: Windows 11 (10.0.26200), .NET SDK 10.0.400, Release configuration. Compilation treats warnings as errors.

## Automated results

The checks executable passes **48 deterministic assertions**, plus **2 human-recording workflow assertions** when the optional samples are supplied. Test output includes generated audio and exports under `artifacts/`.

| Case | Observed result |
| --- | --- |
| C4, D4, E4, G4, A4, G4, E4, C4; 550 ms tones with 150 ms gaps | All eight pitches recovered, in order |
| Reference tone onset / duration | Onsets 0.192 + 0.7n seconds versus 0.2 + 0.7n reference; 560 ms detected length versus 550 ms reference |
| MIDI 45, 57, 69, 81 at 8/16/44.1/48 kHz | Correct octave and pitch at every tested sample rate |
| Synthetic vocal-like waveform | Harmonics, 23-cent 5 Hz vibrato, onset pitch drift and low additive noise retained exactly eight notes; this is synthetic, not a human recording |
| Three equal pitches with intervening silence | Three notes retained |
| Silence and seeded white noise | Zero notes |
| Cancellation | Operation cancelled as expected |
| PCM16 WAV round trip | Rate and sample count preserved; max sample error below 0.00007 |
| Invalid WAV | Rejected |
| Tab assignments | Every assigned string/fret reproduced its note pitch; locked position preserved |
| Out-of-range notes | Retained as unassigned; octave transposition made a low note playable |
| Harmony | Three variants cover every measure; C triad selects C; beginner uses listed open shapes; generated shapes reproduce chord pitch classes |
| Synthesis | Finite samples, peak below 0.951; accompanied WAV generated |
| Project round trip | Audio length, notes and locked fingering retained |
| Invalid edits | Overlap and nonfinite start times rejected |
| MIDI | Independently parsed note-on/off events; exact pitches, matching tempo, balanced releases, onset rounding below 2 ms |
| PDF | All three generated files parse with pypdf in strict mode; known-pitch output is one page, human outputs two pages each |

## Human recordings

Two unaccompanied query WAVs from the public `shigq/QueryBySingingHumming` repository were tested. Its README attributes the WAV queries to Jang's dataset. Provenance and links are in `THIRD_PARTY_NOTICES.md`.

| Input | Duration | Retained notes | Analysis wall time, representative rerun |
| --- | ---: | ---: | ---: |
| `00013.wav` | 8.00 s | 23 | 0.21 s |
| `00014.wav` | 8.00 s | 21 | 0.20 s |

Each completed transcription, fingering assignment, harmony generation, validated project saving and PDF generation. Full note reports are `artifacts/00013-analysis.json` and `artifacts/00014-analysis.json`. These samples demonstrate processing of human voices; **no pitch/onset accuracy score is asserted**, because no aligned note-level ground truth was obtained and no listening adjudication was performed. This two-clip sample is not a broad population benchmark.

SHA-256 for reproducibility:

```text
00013.wav  8ED093622155346D8EDF73D77B2E1103EE4C8E1515ACB154D2AE505D970AF0ED
00014.wav  A7A62FB921A3DE91BC3B0AADBAD04D5A0F836AD50E66DDB3C1D8FEFCC5A348D0
```

The WAVs and derived artifacts remain ignored local validation data and are not distributed with the application.

## Visual / desktop checks actually performed

- Launched the native application and inspected the empty state, native microphone enumeration, keyboard-accessible control tree and dark layout.
- Opened a native WAV selection dialog; file-dialog interaction was not completed through the automation tool.
- Reopened the saved known-pitch project through the application's command-line argument and inspected all eight note rows, waveform, timing, locked fingering and chord cards.
- Switched to the tablature view; inspected string labels, fret numbers, timing guide and scrollable systems.
- Started **With chords** playback through the UI. The application entered playback without an output-device exception; the waveform playhead, current note/fret and chord card visibly highlighted. This confirms playback state and device initialization, not subjective listening quality.
- Rendered and visually inspected the known-pitch PDF using Poppler. All text and tab systems fit on one A4 page. Poppler reported missing fallback Symbol/ArialUnicode display fonts, but the actual ASCII Courier content rendered correctly. Independently parsed all generated PDFs with pypdf strict mode.
- Corrected the initially clipped harmony heading and default bright tab border after screenshot review. Native Windows dialog/scrollbar styling remains.

## Still requires manual verification

- Record your own humming with a microphone; stop, replay, retry and change devices. Permission denial and unplugging during capture/playback are handled in code but were not induced on this machine.
- Listen to original, synthesized melody and accompaniment through headphones; compare pitch, perceived timing and clipping on multiple voices.
- Edit pitch, start and length through the dialog; lock an alternate valid fingering; test an invalid overlap; use undo after transposition. Core invariants are automated, but every dialog action has not been UI-automated.
- Save from the UI, reopen, choose alternate harmonies, export through native save dialogs and open the MIDI in a DAW. The underlying persistence/export paths are automated.
- Resize to the minimum, maximize and move across 100%, 150% and 200% DPI monitors; check keyboard-only navigation and screen readers. DPI support is configured, not certified.
- Run 90-second recordings, repeated device start/stop cycles, real room noise, expressive slides, large vibrato and repeated notes without silence. Investigate octave errors and short-note fragmentation before claiming production transcription accuracy.

No microphone recording of the user's surroundings was taken during development.
