using System.Text;
using System.Text.Json;
using MelodyToTab.Core;

var output = Path.GetFullPath(args.FirstOrDefault() ?? "artifacts"); Directory.CreateDirectory(output);
int passed = 0;
void Check(bool condition, string message) { if (!condition) throw new Exception("FAIL: " + message); passed++; Console.WriteLine("PASS: " + message); }
void Reject(Action action, string message) { try { action(); } catch (Exception e) when (e is InvalidDataException or OperationCanceledException) { Check(true, message); return; } throw new Exception("FAIL: " + message); }
AudioClip Fixture(int[] pitches, int rate = 22050, bool vocal = false, double noise = 0)
{
    var random = new Random(71); var data = new float[(int)((.25 + pitches.Length * .7) * rate)];
    for (int n = 0; n < pitches.Length; n++)
    {
        double phase = 0; int start = (int)((.2 + n * .7) * rate);
        for (int i = 0; i < (int)(.55 * rate); i++)
        {
            double t = (double)i / rate;
            double cents = vocal ? 23 * Math.Sin(2 * Math.PI * 5 * t) + 12 * Math.Exp(-t * 30) : 0;
            phase += 2 * Math.PI * Music.Frequency(pitches[n]) * Math.Pow(2, cents / 1200) / rate;
            double envelope = Math.Min(1, t / .025) * Math.Min(1, (.55 - t) / .04);
            double sample = Math.Sin(phase) + (vocal ? .45 * Math.Sin(2 * phase) + .24 * Math.Sin(3 * phase) + .12 * Math.Sin(4 * phase) : 0);
            data[start + i] = (float)(.32 * sample * envelope + noise * (random.NextDouble() * 2 - 1));
        }
    }
    return new(data, rate);
}
var pitches = new[] { 60, 62, 64, 67, 69, 67, 64, 60 };
var clean = Fixture(pitches);
using (var wav = File.Create(Path.Combine(output, "known-pitches.wav"))) WavCodec.Write(wav, clean);
var detected = PitchAnalyzer.Analyze(clean);
Console.WriteLine("Detected clean: " + string.Join(", ", detected.Notes.Select(n => $"{n.Name}@{n.Start:0.00}/{n.Duration:0.00}")));
Check(detected.Notes.Select(n => n.Midi).SequenceEqual(pitches), "known-pitch sequence recovered exactly");
Check(detected.Notes.Select((n, i) => Math.Abs(n.Start - (.2 + i * .7)) < .065).All(x => x), "clean onset error below 65 ms");
Check(detected.Notes.All(n => Math.Abs(n.Duration - .55) < .085), "clean duration error below 85 ms");
foreach (int rate in new[] { 8000, 16000, 44100, 48000 }) Check(PitchAnalyzer.Analyze(Fixture([45, 57, 69, 81], rate)).Notes.Select(n => n.Midi).SequenceEqual(new[] { 45, 57, 69, 81 }), $"sample-rate conversion and octaves at {rate} Hz");
var vocal = Fixture(pitches, 44100, true, .008);
using (var wav = File.Create(Path.Combine(output, "synthetic-vocal-like.wav"))) WavCodec.Write(wav, vocal);
var vocalNotes = PitchAnalyzer.Analyze(vocal).Notes;
Check(vocalNotes.Select(n => n.Midi).SequenceEqual(pitches), "synthetic harmonics + 23-cent vibrato + noise do not fragment notes");
Check(PitchAnalyzer.Analyze(Fixture([60, 60, 60])).Notes.Count == 3, "same-pitch notes separated by silence remain separate");
Check(PitchAnalyzer.Analyze(new(new float[32000], 16000)).Notes.Count == 0, "silence returns helpful empty transcription");
var rng = new Random(42); var noiseClip = new AudioClip(Enumerable.Range(0, 32000).Select(_ => (float)(rng.NextDouble() * .2 - .1)).ToArray(), 16000);
Check(PitchAnalyzer.Analyze(noiseClip).Notes.Count == 0, "white noise is rejected");
var ct = new CancellationToken(true); Reject(() => PitchAnalyzer.Analyze(clean, null, ct), "analysis cancellation observed");
using (var wav = new MemoryStream()) { WavCodec.Write(wav, clean); wav.Position = 0; var read = WavCodec.Read(wav); Check(read.Samples.Length == clean.Samples.Length && read.SampleRate == clean.SampleRate, "PCM16 WAV round trip"); Check(read.Samples.Zip(clean.Samples, (a, b) => Math.Abs(a - b)).Max() < .00007, "WAV quantization stays within tolerance"); }
Reject(() => WavCodec.Read(new MemoryStream("garbage"u8.ToArray())), "invalid WAV rejected");
var s = new Session { Title = "Known-pitch validation", Notes = detected.Notes, Tempo = 100, KeyRoot = 0 };
GuitarArranger.Assign(s.Notes);
Check(s.Notes.All(n => n.String is >= 1 and <= 6 && Music.Tuning[n.String - 1] + n.Fret == n.Midi), "all fingerings reproduce melody pitch");
var locked = s.Notes[0]; locked.String = 2; locked.Fret = 1; locked.Locked = true; GuitarArranger.Assign(s.Notes); Check(locked.String == 2 && locked.Fret == 1, "manual fingering survives regeneration");
var outside = new List<MelodyNote> { new() { Midi = 36, Start = 0, Duration = 1 }, new() { Midi = 88, Start = 1, Duration = 1 } }; GuitarArranger.Assign(outside); Check(outside.All(n => n.String == 0), "out-of-range notes remain visibly unassigned");
var transposed = new Session { Notes = [new() { Midi = 36, Start = 0, Duration = 1 }] }; GuitarArranger.Transpose(transposed, 12); Check(transposed.Notes[0].Midi == 48 && transposed.Notes[0].String > 0, "octave transposition produces playable note");
var options = HarmonyEngine.Suggest(s); Check(options.Count == 3 && options.All(h => h.Bars.Count == 3), "three harmonizations cover all 4/4 measures");
var cMajor = new Session { Tempo = 120, Notes = [new() { Midi = 60, Start = 0, Duration = .5 }, new() { Midi = 64, Start = .5, Duration = .5 }, new() { Midi = 67, Start = 1, Duration = 1 }] };
Check(HarmonyEngine.Suggest(cMajor)[0].Bars[0].Chord.Name == "C", "C major triad harmonizes as C");
Check(options[2].Bars.All(b => new[] { "C", "D", "E", "G", "A", "Am", "Dm", "Em" }.Contains(b.Chord.Name)), "beginner variant uses open shapes");
foreach (var option in options) foreach (var bar in option.Bars) Check(bar.Chord.Frets.Select((f, i) => (Fret: f, Midi: Music.Tuning[5 - i] + f)).Where(x => x.Fret >= 0).All(x => bar.Chord.PitchClasses.Contains(x.Midi % 12)), "diagram matches " + bar.Chord.Name);
var rendered = Synthesizer.Render(s, options[0]); Check(rendered.Samples.All(float.IsFinite) && rendered.Samples.Max(Math.Abs) <= .951, "accompanied synthesis finite and peak-limited");
using (var wav = File.Create(Path.Combine(output, "synthesized-with-chords.wav"))) WavCodec.Write(wav, rendered);
string project = Path.Combine(output, "known-pitches.mtab"); ProjectStore.Save(project, s, clean); var loaded = ProjectStore.Load(project);
Check(loaded.Session.Notes.Count == s.Notes.Count && loaded.Session.Notes[0].Locked && loaded.Audio?.Samples.Length == clean.Samples.Length, "project round trip preserves edits, fingering and audio");
Reject(() => Music.Validate(new() { Notes = [new() { Midi = 60, Start = 0, Duration = 1 }, new() { Midi = 62, Start = .5, Duration = 1 }] }), "overlapping edits rejected");
Reject(() => Music.Validate(new() { Notes = [new() { Midi = 60, Start = double.NaN, Duration = 1 }] }), "nonfinite note timing rejected");
string midiPath = Path.Combine(output, "known-pitches.mid"); MidiExporter.Write(midiPath, s); var midi = File.ReadAllBytes(midiPath);
Check(Encoding.ASCII.GetString(midi, 0, 4) == "MThd" && midi[^3] == 255 && midi[^2] == 47, "MIDI header and end marker valid");
// Independently parse delta-time events to verify pitch, timing, tempo and release.
int at = 22, ticks = 0, noteOns = 0, noteOffs = 0; var starts = new List<double>();
int Var() { int v = 0, b; do { b = midi[at++]; v = (v << 7) | (b & 127); } while ((b & 128) != 0); return v; }
while (at < midi.Length)
{
    ticks += Var(); int type = midi[at++];
    if (type == 255) { int meta = midi[at++], size = Var(); if (meta == 81) Check(((midi[at] << 16) | (midi[at + 1] << 8) | midi[at + 2]) == 600000, "MIDI tempo metadata correct"); at += size; continue; }
    int pitch = midi[at++]; at++;
    if (type == 0x90) { Check(pitch == s.Notes[noteOns].Midi, "MIDI pitch matches note " + noteOns); starts.Add(ticks * 60.0 / (480 * s.Tempo)); noteOns++; } else if (type == 0x80) noteOffs++;
}
Check(noteOns == s.Notes.Count && noteOffs == noteOns, "MIDI contains balanced note-on/off events"); Check(starts.Select((t, i) => Math.Abs(t - s.Notes[i].Start) < .002).All(x => x), "MIDI preserves original seconds within tick precision");
PdfExporter.Write(Path.Combine(output, "known-pitches.pdf"), s, options[0]); Check(File.ReadAllText(Path.Combine(output, "known-pitches.pdf")).Contains("xref"), "PDF emitted with cross-reference table");
foreach (string file in args.Skip(1))
{
    using var stream = File.OpenRead(file); var human = WavCodec.Read(stream); var watch = System.Diagnostics.Stopwatch.StartNew(); var result = PitchAnalyzer.Analyze(human); watch.Stop();
    var report = new { Source = Path.GetFileName(file), human.Duration, AnalysisSeconds = watch.Elapsed.TotalSeconds, result.Tempo, Key = Music.Names[result.KeyRoot] + (result.Minor ? " minor" : " major"), result.Message, Notes = result.Notes };
    File.WriteAllText(Path.Combine(output, Path.GetFileNameWithoutExtension(file) + "-analysis.json"), JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }));
    Check(result.Notes.Count > 0, "human recording yields stable pitches: " + Path.GetFileName(file));
    var humanSession = new Session { Title = Path.GetFileNameWithoutExtension(file), Notes = result.Notes, Tempo = result.Tempo, KeyRoot = result.KeyRoot, Minor = result.Minor }; GuitarArranger.Assign(humanSession.Notes); Music.Validate(humanSession);
    ProjectStore.Save(Path.Combine(output, Path.GetFileNameWithoutExtension(file) + ".mtab"), humanSession, human);
    PdfExporter.Write(Path.Combine(output, Path.GetFileNameWithoutExtension(file) + ".pdf"), humanSession, HarmonyEngine.Suggest(humanSession)[0]);
    Console.WriteLine($"HUMAN: {human.Duration:0.00}s, {result.Notes.Count} notes, analysis {watch.Elapsed.TotalSeconds:0.00}s. No ground-truth pitch accuracy asserted.");
}
Console.WriteLine($"\n{passed} checks passed. Outputs: {output}");
