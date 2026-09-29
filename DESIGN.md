# Interface decisions

Applied Designly composition guidance and Taste's brief-first design routing to a native desktop workspace. Web-framework defaults do not apply to the requested Windows Forms stack.

- Primary anchor: Record. Secondary: the waveform and pitch contour. Supporting controls: transcription/tab switch and harmony sidebar.
- Charcoal canvas `#16191C`, surface `#1F2327`, raised controls `#2B3136`; text `#EDF0EE`, secondary text `#AEBBBE`, teal `#65DBBC`, uncertainty amber `#F7C16B`.
- Segoe UI for familiar readable Windows typography; Consolas for fret numbers. Normal labels are 10pt with a restrained larger app title. Common labels and buttons remain native, keyboard-focusable controls.
- Fixed workflow rows above a resizing two-pane work area; tablature and chord areas scroll. Minimum window size 1050×800 logical units. PerMonitorV2 high-DPI configuration and AutoScaleMode.Dpi are enabled.
- Custom graphics are double-buffered. Transcription provides a text equivalent of pitch, timing and fingering; chord cards expose accessible names and explanations. Uncertainty uses both color and a `?`, range failures use explicit text. A screen-reader audit and mixed-DPI monitor test remain required.
- Empty-state instructions describe a useful first recording. No decorative artwork or placeholder score. Native file dialogs are intentional; custom dark navigation replaces default bright tabs.

Composition state: modular grid; approximately 72/28 work-area split; 24px outer padding; low motion (only recording/playback state); moderate information density. Initial rendered inspection identified a clipped heading and native tab border, both corrected. Loaded note rows, waveform, tab systems and chord diagrams were inspected in the running application.
