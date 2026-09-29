namespace MelodyToTab.Core;

public static class Synthesizer
{
    public static AudioClip Render(Session session, Harmonization? harmony = null, CancellationToken cancellation = default)
    {
        const int rate = 22050;
        double end = session.Notes.Select(n => n.Start + n.Duration).DefaultIfEmpty(0).Max();
        if (harmony is not null) end = Math.Max(end, harmony.Bars.Select(b => b.Start + b.Duration).DefaultIfEmpty(0).Max());
        var data = new float[(int)((end + .25) * rate)];
        void Tone(int midi, double start, double duration, double volume, bool pluck)
        {
            int first = (int)(start * rate), length = (int)(duration * rate);
            double hz = Music.Frequency(midi);
            for (int i = 0; i < length && first + i < data.Length; i++)
            {
                if (i % rate == 0) cancellation.ThrowIfCancellationRequested();
                double t = (double)i / rate;
                double envelope = Math.Min(1, t / .012) * Math.Min(1, (duration - t) / .04) * (pluck ? Math.Exp(-2.5 * t) : 1);
                double tone = Math.Sin(2 * Math.PI * hz * t) + .22 * Math.Sin(4 * Math.PI * hz * t) + .08 * Math.Sin(6 * Math.PI * hz * t);
                data[first + i] += (float)(tone * envelope * volume);
            }
        }
        foreach (var n in session.Notes) Tone(n.Midi, n.Start, n.Duration, .32, false);
        if (harmony is not null) foreach (var bar in harmony.Bars) for (int i = 0; i < 6; i++)
            if (bar.Chord.Frets[i] >= 0) Tone(Music.Tuning[5 - i] + bar.Chord.Frets[i], bar.Start + i * .012, Math.Max(.04, bar.Duration - i * .012), .045, true);
        float peak = data.Select(Math.Abs).DefaultIfEmpty(1).Max();
        if (peak > .95) for (int i = 0; i < data.Length; i++) data[i] *= .95f / peak;
        return new(data, rate);
    }
}
