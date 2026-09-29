namespace MelodyToTab.Core;

public static class PitchAnalyzer
{
    private const int Rate = 16000, Hop = 160, Window = 1024, MaxLag = 246;
    private sealed record Frame(double Time, int Midi, double Confidence);
    public static AnalysisResult Analyze(AudioClip clip, IProgress<int>? progress = null, CancellationToken cancellation = default)
    {
        // A short low-pass FIR before sample-rate conversion avoids folding high harmonics.
        var samples = Resample(clip, cancellation);
        double rms = Math.Sqrt(samples.Select(x => (double)x * x).DefaultIfEmpty().Average());
        if (rms < .002) return new([], 100, 0, false, "No clear voice detected. Move closer to the microphone and try again.");
        var frames = new List<Frame>();
        var difference = new double[MaxLag + 1];
        for (int pos = 0; pos + Window <= samples.Length; pos += Hop)
        {
            cancellation.ThrowIfCancellationRequested();
            double energy = 0;
            for (int j = 0; j < Window; j++) energy += samples[pos + j] * samples[pos + j];
            if (Math.Sqrt(energy / Window) < Math.Max(.003, rms * .12)) { frames.Add(new((pos + Window / 2.0) / Rate, -1, 0)); continue; }
            double running = 0;
            difference[0] = 1;
            for (int lag = 1; lag <= MaxLag; lag++)
            {
                double d = 0;
                for (int j = 0; j < Window / 2; j++) { double delta = samples[pos + j] - samples[pos + j + lag]; d += delta * delta; }
                running += d;
                difference[lag] = running > 0 ? d * lag / running : 1;
            }
            int chosen = -1;
            for (int lag = 16; lag < MaxLag; lag++)
                if (difference[lag] < .18) { while (lag + 1 <= MaxLag && difference[lag + 1] < difference[lag]) lag++; chosen = lag; break; }
            if (chosen < 0) frames.Add(new((pos + Window / 2.0) / Rate, -1, 0));
            else
            {
                double refined = chosen;
                if (chosen > 1 && chosen < MaxLag)
                {
                    double a = difference[chosen - 1], b = difference[chosen], c = difference[chosen + 1];
                    double denom = a - 2 * b + c;
                    if (Math.Abs(denom) > 1e-12) refined += Math.Clamp(.5 * (a - c) / denom, -.5, .5);
                }
                double pitch = 69 + 12 * Math.Log2(Rate / refined / 440);
                double confidence = (1 - difference[chosen]) * (1 - .35 * Math.Abs(pitch - Math.Round(pitch)));
                frames.Add(new((pos + Window / 2.0) / Rate, (int)Math.Round(pitch), confidence));
            }
            if (pos % (Hop * 20) == 0) progress?.Report((int)(100.0 * pos / samples.Length));
        }
        // Median filtering removes brief semitone flicker; unvoiced gaps remain breaks.
        var smooth = frames.Select((frame, index) =>
        {
            if (frame.Midi < 0) return frame;
            int first = Math.Max(0, index - 2), last = Math.Min(frames.Count - 1, index + 2);
            var neighbors = frames.Skip(first).Take(last - first + 1).Where(f => f.Midi >= 0).Select(f => f.Midi).Order().ToArray();
            return frame with { Midi = neighbors[neighbors.Length / 2] };
        }).ToList();
        var notes = new List<MelodyNote>();
        for (int i = 0; i < smooth.Count;)
        {
            int j = i + 1;
            while (j < smooth.Count && smooth[j].Midi == smooth[i].Midi) j++;
            double duration = (j - i) * Hop / (double)Rate;
            if (smooth[i].Midi >= 0 && duration >= .08)
            {
                double start = Math.Max(0, smooth[i].Time - .02);
                var note = new MelodyNote { Midi = smooth[i].Midi, Start = start, Duration = duration, Confidence = smooth.Skip(i).Take(j - i).Average(f => f.Confidence) };
                if (notes.Count > 0 && notes[^1].Midi == note.Midi && note.Start - (notes[^1].Start + notes[^1].Duration) < .045)
                { notes[^1].Duration = note.Start + note.Duration - notes[^1].Start; notes[^1].Confidence = Math.Min(notes[^1].Confidence, note.Confidence); }
                else notes.Add(note);
            }
            i = j;
        }
        var key = EstimateKey(notes);
        int tempo = EstimateTempo(notes);
        double clipped = clip.Samples.Count(x => Math.Abs(x) > .99) / (double)Math.Max(1, clip.Samples.Length);
        string message = notes.Count == 0 ? "No stable pitches found. Hum one clear note at a time in a quiet room." : $"{notes.Count} notes detected. Key and tempo are estimates; review timing before export.";
        if (clipped > .005) message += " Recording clips: reduce microphone gain.";
        if (frames.Count > 0 && frames.Count(f => f.Midi >= 0) < frames.Count * .25) message += " Low voiced coverage: background noise or breathiness may affect accuracy.";
        progress?.Report(100);
        return new(notes, tempo, key.Root, key.Minor, message);
    }
    private static float[] Resample(AudioClip clip, CancellationToken ct)
    {
        var result = new float[(int)(clip.Duration * Rate)];
        double cutoff = Math.Min(1, (double)Rate / clip.SampleRate) * .9;
        for (int i = 0; i < result.Length; i++)
        {
            if (i % 16000 == 0) ct.ThrowIfCancellationRequested();
            double p = (double)i * clip.SampleRate / Rate, sum = 0, weights = 0;
            int center = (int)p;
            for (int k = -16; k <= 16; k++)
            {
                int index = center + k; if (index < 0 || index >= clip.Samples.Length) continue;
                double d = index - p;
                double weight = (Math.Abs(d) < 1e-9 ? cutoff : Math.Sin(Math.PI * d * cutoff) / (Math.PI * d)) * (.5 + .5 * Math.Cos(Math.PI * d / 17));
                sum += clip.Samples[index] * weight; weights += weight;
            }
            result[i] = (float)(sum / weights);
        }
        return result;
    }
    public static (int Root, bool Minor) EstimateKey(List<MelodyNote> notes)
    {
        double[] major = [6.35, 2.23, 3.48, 2.33, 4.38, 4.09, 2.52, 5.19, 2.39, 3.66, 2.29, 2.88];
        double[] minor = [6.33, 2.68, 3.52, 5.38, 2.6, 3.53, 2.54, 4.75, 3.98, 2.69, 3.34, 3.17];
        double best = double.NegativeInfinity; (int, bool) key = (0, false);
        foreach (bool min in new[] { false, true }) for (int root = 0; root < 12; root++)
        {
            var profile = min ? minor : major;
            double score = notes.Sum(n => n.Duration * profile[(n.Midi % 12 - root + 12) % 12]);
            if (score > best) { best = score; key = (root, min); }
        }
        return key;
    }
    private static int EstimateTempo(List<MelodyNote> notes)
    {
        var intervals = notes.Zip(notes.Skip(1), (a, b) => b.Start - a.Start).Where(t => t >= .2 && t <= 1.5).Order().ToArray();
        if (intervals.Length < 2) return 100;
        double beat = intervals[intervals.Length / 2];
        double bpm = 60 / beat; while (bpm < 70) bpm *= 2; while (bpm > 160) bpm /= 2;
        return (int)Math.Round(bpm);
    }
}
