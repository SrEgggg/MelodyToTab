# Third-party dependencies and validation data

Application dependency: [NAudio 2.2.1](https://github.com/naudio/NAudio), by Mark Heath and contributors, MIT licensed. Restore its NuGet package and accompanying transitive NAudio packages. Its license is available at https://github.com/naudio/NAudio/blob/master/license.txt . Windows Forms and .NET are provided by Microsoft under their respective runtime/SDK licenses. No third-party DSP source code is copied into this project.

Two real-recording validation WAVs were retrieved from the [QueryBySingingHumming repository](https://github.com/shigq/QueryBySingingHumming/tree/master/WAVqueries), whose README identifies them as queries from Jang's dataset. They are only local test inputs under ignored `artifacts/human`, not application assets and not part of the distribution. The repository's code license does not establish independent redistribution rights for the recordings; obtain dataset permission before distributing them.

- `00013.wav`: https://raw.githubusercontent.com/shigq/QueryBySingingHumming/master/WAVqueries/00013.wav
- `00014.wav`: https://raw.githubusercontent.com/shigq/QueryBySingingHumming/master/WAVqueries/00014.wav

No audio, transcription or other data is sent to these sources by the application. Internet access was used during development to restore packages and download validation inputs only.
