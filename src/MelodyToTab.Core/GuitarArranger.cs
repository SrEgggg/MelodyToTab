namespace MelodyToTab.Core;

public static class GuitarArranger
{
    public static List<(int String, int Fret)> Positions(int midi) => Music.Tuning.Select((pitch, i) => (String: i + 1, Fret: midi - pitch)).Where(p => p.Fret is >= 0 and <= 20).ToList();
    public static void Assign(List<MelodyNote> notes)
    {
        // Dynamic programming minimizes total movement, rather than making greedy local choices.
        int start = 0;
        while (start < notes.Count)
        {
            if (Positions(notes[start].Midi).Count == 0) { notes[start].String = 0; notes[start].Fret = 0; notes[start].Locked = false; start++; continue; }
            int end = start;
            while (end < notes.Count && Positions(notes[end].Midi).Count > 0) end++;
            var candidates = notes.Skip(start).Take(end - start).Select(n => n.Locked ? new List<(int String, int Fret)> { (n.String, n.Fret) } : Positions(n.Midi)).ToList();
            var costs = candidates.Select(c => new double[c.Count]).ToArray();
            var previous = candidates.Select(c => new int[c.Count]).ToArray();
            for (int i = 0; i < candidates.Count; i++) for (int j = 0; j < candidates[i].Count; j++)
            {
                var p = candidates[i][j]; double local = p.Fret * .12;
                if (i == 0) { costs[i][j] = local; continue; }
                costs[i][j] = double.PositiveInfinity;
                for (int k = 0; k < candidates[i - 1].Count; k++)
                {
                    var q = candidates[i - 1][k];
                    double travel = (p.Fret == 0 || q.Fret == 0 ? .6 : Math.Abs(p.Fret - q.Fret)) + Math.Abs(p.String - q.String) * .7;
                    double cost = costs[i - 1][k] + travel + local;
                    if (cost < costs[i][j]) { costs[i][j] = cost; previous[i][j] = k; }
                }
            }
            int chosen = Array.IndexOf(costs[^1], costs[^1].Min());
            for (int i = candidates.Count - 1; i >= 0; i--) { (notes[start + i].String, notes[start + i].Fret) = candidates[i][chosen]; chosen = previous[i][chosen]; }
            start = end;
        }
    }
    public static void Transpose(Session session, int semitones)
    {
        if (session.Notes.Any(n => n.Midi + semitones is < 0 or > 127)) throw new InvalidOperationException("Transposition exceeds the MIDI pitch range.");
        foreach (var note in session.Notes) { note.Midi += semitones; note.Locked = false; }
        session.KeyRoot = (session.KeyRoot + semitones % 12 + 12) % 12;
        Assign(session.Notes);
    }
}

public static class HarmonyEngine
{
    // Diagram order is low E to high E; -1 = muted.
    private static readonly Dictionary<string, int[]> OpenShapes = new()
    {
        ["C"] = [-1, 3, 2, 0, 1, 0],
        ["D"] = [-1, -1, 0, 2, 3, 2],
        ["E"] = [0, 2, 2, 1, 0, 0],
        ["G"] = [3, 2, 0, 0, 0, 3],
        ["A"] = [-1, 0, 2, 2, 2, 0],
        ["Am"] = [-1, 0, 2, 2, 1, 0],
        ["Dm"] = [-1, -1, 0, 2, 3, 1],
        ["Em"] = [0, 2, 2, 0, 0, 0]
    };
    public static Chord MakeChord(int root, bool minor)
    {
        root = (root + 12) % 12; string name = Music.Names[root] + (minor ? "m" : "");
        int fret = (root - 4 + 12) % 12;
        int[] shape = OpenShapes.TryGetValue(name, out var open) ? open : [fret, fret + 2, fret + 2, fret + (minor ? 0 : 1), fret, fret];
        return new(name, root, minor, shape);
    }
    public static List<Harmonization> Suggest(Session s)
    {
        if (s.Notes.Count == 0) return [];
        double barDuration = 240.0 / s.Tempo;
        int count = (int)Math.Ceiling(s.Notes.Max(n => n.Start + n.Duration) / barDuration);
        int[] scale = s.Minor ? [0, 2, 3, 5, 7, 8, 10] : [0, 2, 4, 5, 7, 9, 11];
        bool[] minor = s.Minor ? [true, false, false, true, true, false, false] : [false, true, true, false, false, true, false];
        var diatonic = Enumerable.Range(0, 7).Where(i => i != (s.Minor ? 1 : 6)).Select(i => MakeChord(s.KeyRoot + scale[i], minor[i])).ToArray();
        var results = new List<Harmonization>();
        for (int variant = 0; variant < 3; variant++)
        {
            var pool = variant == 2 ? OpenShapes.Keys.Select(name => MakeChord(Array.IndexOf(Music.Names, name.TrimEnd('m')), name.EndsWith('m'))).ToArray() : diatonic;
            var bars = new List<ChordBar>();
            for (int bar = 0; bar < count; bar++)
            {
                double start = bar * barDuration;
                var weighted = s.Notes.Select(n => (Note: n, Weight: Math.Max(0, Math.Min(n.Start + n.Duration, start + barDuration) - Math.Max(n.Start, start)))).Where(x => x.Weight > 0).ToList();
                double Score(Chord c)
                {
                    double score = weighted.Sum(x => x.Weight * (c.PitchClasses.Contains(x.Note.Midi % 12) ? 2 : -.8));
                    if (c.Root == s.KeyRoot) score += .12;
                    if (bars.Count > 0 && bars[^1].Chord.Name == c.Name) score += variant == 0 ? .15 : -.3;
                    if (variant == 1 && results[0].Bars[bar].Chord.Name == c.Name) score -= .9;
                    return score;
                }
                var chosen = pool.OrderByDescending(Score).First();
                double total = weighted.Sum(x => x.Weight), fit = weighted.Where(x => chosen.PitchClasses.Contains(x.Note.Midi % 12)).Sum(x => x.Weight);
                string reason = total == 0 ? "Rest measure: a harmonic continuation." : $"{fit / total:P0} of sounding melody time uses {chosen.Name} chord tones.";
                if (variant == 2) reason += " Open-position shape; may borrow outside the key.";
                else reason += " Drawn from the selected key.";
                bars.Add(new(bar + 1, start, barDuration, chosen, reason));
            }
            results.Add(new(variant switch { 0 => "Closest fit", 1 => "Alternative color", _ => "Beginner · open chords" }, bars));
        }
        return results;
    }
}
