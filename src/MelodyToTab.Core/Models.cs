namespace MelodyToTab.Core;

public sealed record AudioClip(float[] Samples, int SampleRate)
{
    public double Duration => (double)Samples.Length / SampleRate;
}

public sealed class MelodyNote
{
    public int Midi { get; set; }
    public double Start { get; set; }
    public double Duration { get; set; }
    public double Confidence { get; set; } = 1;
    // String 1 is high E. Zero means out of range/unassigned.
    public int String { get; set; }
    public int Fret { get; set; }
    public bool Locked { get; set; }
    public string Name => Music.Name(Midi);
    public MelodyNote Copy() => (MelodyNote)MemberwiseClone();
}

public sealed class Session
{
    public int Version { get; set; } = 1;
    public string Title { get; set; } = "Untitled melody";
    public List<MelodyNote> Notes { get; set; } = [];
    public int Tempo { get; set; } = 100;
    public int KeyRoot { get; set; }
    public bool Minor { get; set; }
    public int Harmony { get; set; }
}

public sealed record AnalysisResult(List<MelodyNote> Notes, int Tempo, int KeyRoot, bool Minor, string Message);
public sealed record Chord(string Name, int Root, bool Minor, int[] Frets)
{
    public int[] PitchClasses => [Root, (Root + (Minor ? 3 : 4)) % 12, (Root + 7) % 12];
}
public sealed record ChordBar(int Measure, double Start, double Duration, Chord Chord, string Reason);
public sealed record Harmonization(string Name, List<ChordBar> Bars);

public static class Music
{
    public static readonly string[] Names = ["C", "C#", "D", "D#", "E", "F", "F#", "G", "G#", "A", "A#", "B"];
    public static readonly int[] Tuning = [64, 59, 55, 50, 45, 40];
    public static string Name(int midi) => Names[((midi % 12) + 12) % 12] + (midi / 12 - 1);
    public static double Frequency(int midi) => 440 * Math.Pow(2, (midi - 69) / 12.0);
    public static int Parse(string value)
    {
        for (int i = 0; i <= 127; i++) if (Name(i).Equals(value.Trim(), StringComparison.OrdinalIgnoreCase)) return i;
        throw new FormatException("Use a note name such as C4, F#3 or A4 (sharps, octave required).");
    }
    public static void Validate(Session session)
    {
        if (session.Title is null || session.Title.Length > 200 || session.Notes is null || session.Version != 1 || session.Tempo is < 40 or > 240 || session.KeyRoot is < 0 or > 11 || session.Harmony is < 0 or > 2 || session.Notes.Count > 2000)
            throw new InvalidDataException("Unsupported or invalid project settings.");
        double end = 0;
        foreach (var n in session.Notes.OrderBy(n => n.Start))
        {
            if (n.Midi is < 0 or > 127 || !double.IsFinite(n.Start) || !double.IsFinite(n.Duration) || n.Start < 0 || n.Duration < .04 || n.Start + n.Duration > 95 || n.Start < end - .001 || !double.IsFinite(n.Confidence) || n.Confidence is < 0 or > 1)
                throw new InvalidDataException("Notes must be non-overlapping, at least 0.04 seconds long, and within 95 seconds.");
            if (n.Locked && (n.String is < 1 or > 6 || n.Fret is < 0 or > 20 || Music.Tuning[n.String - 1] + n.Fret != n.Midi))
                throw new InvalidDataException("Locked fingering does not match its pitch.");
            end = n.Start + n.Duration;
        }
    }
}
