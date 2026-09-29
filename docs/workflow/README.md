# Application workflow — EW AI Flowchart

`melody-to-tab.efd.json` is an EFD 2.1 AS-IS model of the implemented Melody / Tab user journey. It was authored with the EW AI Flowchart skill. Open/import the JSON in a compatible EW AI Flowchart installation. `melody-to-tab.mmd` is a Mermaid projection of the same node and edge definitions, embedded in the main README for GitHub rendering.

Sources: the current desktop workflow in `src/MelodyToTab.Desktop/MainForm.cs`, the core analyzer/arranger/export code, and the project README and validation record. This is software workflow documentation, not a corporate approval process.

The model includes new recording/WAV input, saved project loading, invalid-input recovery, pitch analysis/cancellation, silence/poor-recording retries, note review, tab and harmony generation, optional transposition, comparison playback, correction loops, and the selected local save/export operation. Actual UI navigation remains flexible; the graph is a recommended path through those actions.

## Responsibility and pending metadata

The schema's organization units are explicitly labeled **functional lanes** for musician interaction and the local application. They do not assert that real departments exist. The only human participant is the musician/application user; automated execution belongs to the local application system actor. No AI worker or paid service is involved in the audio pipeline.

Responsible (`R`) participants follow the implementation. Formal organizational accountability (`A`) is **not specified** by the project, so it is intentionally absent rather than invented. The bundled validator therefore emits accountability warnings for executable tasks. There are no pending software-flow decisions; formal accountability would need confirmation only if this were reused as an organizational governance model.

## Validation

See `validation.json` for the bundled validator's complete result. The model must have zero errors. Non-blocking warnings concerning absent Accountable roles are retained and explained above. Schema checks cover IDs, references, node types, branch labels and layout/mapping references. Additional documentation checks verify reachability, complete layout/mapping coverage and local README links.

The project includes the model and its validation result, not a copy of the plugin. With EW AI Flowchart installed, run its `scripts/validate_efd_json.py` against `docs/workflow/melody-to-tab.efd.json` using the plugin's bundled schema. No external executables are needed to view the Mermaid graph on GitHub.
