using System.Drawing.Drawing2D;
using System.ComponentModel;
using MelodyToTab.Core;

namespace MelodyToTab.Desktop;

internal sealed class WaveView : Control
{
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)] public AudioClip? Audio { get; set; }
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)] public List<MelodyNote> Notes { get; set; } = [];
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)] public double Playhead { get; set; } = -1;
    public WaveView() { DoubleBuffered = true; BackColor = Theme.Surface; AccessibleName = "Waveform and detected note timeline"; ResizeRedraw = true; }
    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e); var g = e.Graphics; g.SmoothingMode = SmoothingMode.AntiAlias;
        using var muted = new SolidBrush(Theme.Muted); using var accent = new Pen(Theme.Accent, 1); using var line = new Pen(Theme.Line);
        int left = 18, right = Math.Max(19, Width - 18), mid = Height / 3;
        using var heading = new Font("Segoe UI", 18); using var noteFont = new Font("Segoe UI", 8);
        if ((Audio is null || Audio.Samples.Length == 0) && Notes.Count == 0) { g.DrawString("Your melody starts here", heading, muted, 24, 35); g.DrawString("Record 5–30 seconds of clean humming, or import a WAV file.\nOne voice, one note at a time. Leave a short pause between phrases.", Font, muted, 24, 78); return; }
        double duration = Math.Max(.1, Math.Max(Audio?.Duration ?? 0, Notes.Select(n => n.Start + n.Duration).DefaultIfEmpty(0).Max()));
        var samples = Audio?.Samples ?? [];
        g.DrawLine(line, left, mid, right, mid);
        for (int x = left; x < right; x++)
        {
            int a = (int)((double)(x - left) * duration * (Audio?.SampleRate ?? 1) / (right - left)), b = Math.Max(a + 1, (int)((double)(x - left + 1) * duration * (Audio?.SampleRate ?? 1) / (right - left)));
            float max = 0; for (int i = a; i < Math.Min(b, samples.Length); i++) max = Math.Max(max, Math.Abs(samples[i]));
            float h = Math.Max(1, max * (mid - 16)); g.DrawLine(accent, x, mid - h, x, mid + h);
        }
        for (int t = 0; t <= duration; t += Math.Max(1, (int)(duration / 12))) { float x = left + (float)(t / duration * (right - left)); g.DrawString(t + "s", Font, muted, x, Height - 23); }
        int minimum = Notes.Select(n => n.Midi).DefaultIfEmpty(48).Min(), maximum = Notes.Select(n => n.Midi).DefaultIfEmpty(72).Max();
        foreach (var n in Notes)
        {
            float x = left + (float)(n.Start / duration * (right - left)), w = Math.Max(3, (float)(n.Duration / duration * (right - left)));
            float y = Height - 48 - (n.Midi - minimum) * Math.Min(3, 35f / Math.Max(1, maximum - minimum));
            bool active = Playhead >= n.Start && Playhead < n.Start + n.Duration;
            using var brush = new SolidBrush(active ? Color.White : n.Confidence < .85 ? Theme.Warning : Theme.Accent);
            g.FillRectangle(brush, x, y, w, 10); if (w > 27) g.DrawString(n.Name, noteFont, muted, x, y - 17);
        }
        if (Playhead >= 0) { float x = left + (float)(Playhead / duration * (right - left)); using var pen = new Pen(Color.White, 2); g.DrawLine(pen, x, 8, x, Height - 25); }
    }
}

internal sealed class TabView : ScrollableControl
{
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)] public Session Session { get; set; } = new();
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)] public Harmonization? Harmony { get; set; }
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)] public double Playhead { get; set; } = -1;
    public TabView() { DoubleBuffered = true; AutoScroll = true; BackColor = Theme.Surface; ResizeRedraw = true; AccessibleName = "Guitar tablature, standard tuning, six strings"; }
    public void Follow(MelodyNote current)
    {
        double barDuration = 240.0 / Session.Tempo; int targetBar = (int)(current.Start / barDuration), system = 0;
        int columns = Math.Max(1, (Math.Max(400, ClientSize.Width - 52) - 40) / 76);
        for (int bar = 0; bar < targetBar; bar++) system += Math.Max(1, (int)Math.Ceiling(Session.Notes.Count(n => n.Start >= bar * barDuration && n.Start < (bar + 1) * barDuration) / (double)columns));
        system += Session.Notes.Count(n => n.Start >= targetBar * barDuration && n.Start < current.Start) / columns;
        int y = 20 + 216 * system; if (y < -AutoScrollPosition.Y || y + 205 > -AutoScrollPosition.Y + ClientSize.Height) AutoScrollPosition = new Point(0, Math.Max(0, y - 10));
    }
    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e); var g = e.Graphics; g.TranslateTransform(AutoScrollPosition.X, AutoScrollPosition.Y);
        using var text = new SolidBrush(Theme.Text); using var muted = new SolidBrush(Theme.Muted); using var line = new Pen(Theme.Line);
        using var mono = new Font("Consolas", 11, FontStyle.Bold); using var small = new Font("Segoe UI", 9);
        using var heading = new Font("Segoe UI", 18);
        if (Session.Notes.Count == 0) { AutoScrollMinSize = Size.Empty; g.DrawString("A playable path for your melody", heading, text, 24, 28); g.DrawString("Analyze a recording to see string and fret positions here.\nReview notes in the transcription tab; amber notes need attention.", Font, muted, 24, 75); return; }
        double barDuration = 240.0 / Session.Tempo;
        int bars = (int)Math.Ceiling(Session.Notes.Max(n => n.Start + n.Duration) / barDuration);
        int y = 20; int width = Math.Max(400, ClientSize.Width - 52);
        for (int bar = 0; bar < bars; bar++)
        {
            var notes = Session.Notes.Where(n => n.Start >= bar * barDuration && n.Start < (bar + 1) * barDuration).ToArray();
            int columns = Math.Max(1, (width - 40) / 76);
            var chunks = notes.Length == 0 ? new[] { Array.Empty<MelodyNote>() } : notes.Chunk(columns).ToArray();
            foreach (var chunk in chunks)
            {
                string chord = Harmony?.Bars.ElementAtOrDefault(bar)?.Chord.Name ?? "";
                g.DrawString($"{bar + 1:00}   {chord}   /   {bar * barDuration:0.00}s" + (chunks.Length > 1 ? " · continued event columns" : ""), Font, text, 20, y);
                for (int st = 0; st < 6; st++) { int sy = y + 50 + st * 22; g.DrawString(new[] { "e", "B", "G", "D", "A", "E" }[st], small, muted, 20, sy - 8); g.DrawLine(line, 46, sy, width + 20, sy); }
                for (int i = 0; i < chunk.Length; i++)
                {
                    var n = chunk[i]; int x = 65 + i * 76; bool active = Playhead >= n.Start && Playhead < n.Start + n.Duration;
                    using var brush = new SolidBrush(active ? Color.White : n.String == 0 || n.Confidence < .85 ? Theme.Warning : Theme.Accent);
                    g.DrawString($"{n.Start:0.00}s", small, muted, x - 9, y + 25);
                    if (n.String == 0) g.DrawString("! " + n.Name, mono, brush, x, y + 85);
                    else { int ny = y + 50 + (n.String - 1) * 22; using var bg = new SolidBrush(Theme.Surface); g.FillRectangle(bg, x - 3, ny - 10, 28, 21); g.DrawString(n.Fret.ToString(), mono, brush, x, ny - 9); }
                    g.DrawString($"{n.Duration:0.00}s", small, muted, x - 9, y + 173);
                }
                if (chunk.Length == 0) g.DrawString("Rest / sustain", small, muted, 70, y + 85);
                y += 216;
            }
        }
        AutoScrollMinSize = new Size(0, y + 10);
    }
}

internal sealed class ChordView : Control
{
    public ChordBar Bar { get; }
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)] public bool Active { get; set; }
    public ChordView(ChordBar bar) { Bar = bar; DoubleBuffered = true; Height = 180; Width = 270; BackColor = Theme.Surface; Margin = new Padding(0, 0, 0, 10); AccessibleName = $"Measure {bar.Measure}, {bar.Chord.Name}. {bar.Reason}"; }
    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics; g.SmoothingMode = SmoothingMode.AntiAlias;
        using var text = new SolidBrush(Theme.Text); using var muted = new SolidBrush(Theme.Muted); using var accent = new SolidBrush(Active ? Color.White : Theme.Accent); using var pen = new Pen(Theme.Line);
        using var title = new Font("Segoe UI", 17, FontStyle.Bold); using var small = new Font("Segoe UI", 9);
        g.DrawString(Bar.Chord.Name, title, accent, 10, 8); g.DrawString($"MEASURE {Bar.Measure:00}", small, muted, 96, 17);
        int[] frets = Bar.Chord.Frets; int min = frets.Where(f => f > 0).DefaultIfEmpty(1).Min(); int first = frets.Max() > 4 ? min : 1;
        const int left = 24, top = 62, step = 17;
        for (int s = 0; s < 6; s++) g.DrawLine(pen, left + s * step, top, left + s * step, top + 64);
        for (int f = 0; f <= 4; f++) g.DrawLine(pen, left, top + f * 16, left + 5 * step, top + f * 16);
        g.DrawString(first + "fr", small, muted, 120, 67);
        for (int s = 0; s < 6; s++)
        {
            int f = frets[s]; if (f <= 0) g.DrawString(f == 0 ? "o" : "x", small, muted, left + s * step - 4, top - 19);
            else g.FillEllipse(accent, left + s * step - 5, top + (f - first) * 16 + 3, 10, 10);
        }
        g.DrawString("E  A  D  G  B  e", small, muted, 16, 130);
        g.DrawString(Bar.Reason, small, text, new RectangleF(151, 48, Math.Max(105, Width - 162), 115));
        if (first > 1 || frets.Count(f => f == 1) > 2) g.DrawString("Barre shape", small, muted, 16, 151);
    }
}
