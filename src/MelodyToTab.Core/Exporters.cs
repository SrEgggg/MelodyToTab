using System.Globalization;
using System.Text;

namespace MelodyToTab.Core;

public static class MidiExporter
{
    public static void Write(string path, Session s)
    {
        Music.Validate(s);
        using var track = new MemoryStream();
        void Variable(int n) { var bytes = new List<byte> { (byte)(n & 127) }; while ((n >>= 7) > 0) bytes.Insert(0, (byte)((n & 127) | 128)); track.Write(bytes.ToArray()); }
        int tempo = 60_000_000 / s.Tempo;
        track.Write([0, 0xFF, 0x51, 3, (byte)(tempo >> 16), (byte)(tempo >> 8), (byte)tempo]);
        track.Write([0, 0xFF, 0x58, 4, 4, 2, 24, 8]);
        var events = s.Notes.SelectMany(n => new[] { (Tick: (int)Math.Round(n.Start * 480 * s.Tempo / 60), On: true, n.Midi), (Tick: (int)Math.Round((n.Start + n.Duration) * 480 * s.Tempo / 60), On: false, n.Midi) }).OrderBy(e => e.Tick).ThenBy(e => e.On).ToList();
        int last = 0;
        foreach (var e in events) { Variable(e.Tick - last); track.Write([(byte)(e.On ? 0x90 : 0x80), (byte)e.Midi, (byte)(e.On ? 90 : 0)]); last = e.Tick; }
        track.Write([0, 255, 47, 0]);
        using var output = File.Create(path);
        output.Write("MThd"u8); output.Write([0, 0, 0, 6, 0, 0, 0, 1, 1, 224]); output.Write("MTrk"u8);
        int size = (int)track.Length; output.Write([(byte)(size >> 24), (byte)(size >> 16), (byte)(size >> 8), (byte)size]); track.Position = 0; track.CopyTo(output);
    }
}

public static class TabText
{
    // Fixed-width event columns include exact timing, retaining short notes and rests.
    public static List<string> Lines(Session s, Harmonization? harmony)
    {
        var lines = new List<string> { "MELODY / TAB", $"{s.Tempo} BPM | 4/4 guide | {Music.Names[s.KeyRoot]} {(s.Minor ? "minor" : "major")}", "Suggested harmony only. Timing is in seconds; string 1 is high E.", "! = outside E2-C6 / fret 0-20. Transpose or edit before playing.", "" };
        double barLength = 240.0 / s.Tempo;
        int count = (int)Math.Ceiling(s.Notes.Select(n => n.Start + n.Duration).DefaultIfEmpty(0).Max() / barLength);
        for (int bar = 0; bar < count; bar++)
        {
            var notes = s.Notes.Where(n => n.Start >= bar * barLength && n.Start < (bar + 1) * barLength).ToList();
            string chord = harmony?.Bars.ElementAtOrDefault(bar)?.Chord.Name ?? "--";
            lines.Add($"Measure {bar + 1} | {chord} | starts {(bar * barLength).ToString("0.00", CultureInfo.InvariantCulture)}s");
            if (notes.Count == 0) { lines.Add("  Rest / sustain from previous measure"); lines.Add(""); continue; }
            foreach (var group in notes.Chunk(7))
            {
                string Cell(string text) => text.PadLeft(9);
                lines.Add("Time " + string.Concat(group.Select(n => Cell(n.Start.ToString("0.00", CultureInfo.InvariantCulture)))));
                lines.Add("Dur. " + string.Concat(group.Select(n => Cell(n.Duration.ToString("0.00", CultureInfo.InvariantCulture)))));
                lines.Add("Note " + string.Concat(group.Select(n => Cell(n.Name + (n.String == 0 ? "!" : "")))));
                for (int st = 1; st <= 6; st++) lines.Add(new[] { "e", "B", "G", "D", "A", "E" }[st - 1] + " |  " + string.Concat(group.Select(n => (n.String == st ? n.Fret.ToString() : "-").PadLeft(9, '-'))) + "|");
                lines.Add("");
            }
        }
        return lines;
    }
}

public static class PdfExporter
{
    // Small dependency-free PDF 1.4 writer: searchable Courier text, paginated A4.
    public static void Write(string path, Session session, Harmonization? harmony)
    {
        Music.Validate(session);
        // Keep each six-string system together across page breaks.
        var pages = new List<List<string>>();
        var pageLines = new List<string>(); var block = new List<string>();
        void FlushBlock()
        {
            if (pageLines.Count + block.Count > 52 && pageLines.Count > 0) { pages.Add(pageLines); pageLines = []; }
            pageLines.AddRange(block); block.Clear();
        }
        foreach (var line in TabText.Lines(session, harmony)) { block.Add(line); if (line.Length == 0) FlushBlock(); }
        FlushBlock(); if (pageLines.Count > 0) pages.Add(pageLines);
        var objects = new List<string> { "", "", "<< /Type /Font /Subtype /Type1 /BaseFont /Courier >>" };
        var pageIds = new List<int>();
        foreach (var page in pages)
        {
            int pageId = objects.Count + 1, streamId = pageId + 1; pageIds.Add(pageId);
            objects.Add($"<< /Type /Page /Parent 2 0 R /MediaBox [0 0 595 842] /Resources << /Font << /F1 3 0 R >> >> /Contents {streamId} 0 R >>");
            var content = new StringBuilder("BT /F1 10 Tf 14 TL 38 800 Td\n");
            foreach (string line in page) content.Append('(').Append(line.Replace("\\", "\\\\").Replace("(", "\\(").Replace(")", "\\)")).Append(") Tj T*\n");
            content.Append("ET\n"); string text = content.ToString(); objects.Add($"<< /Length {Encoding.ASCII.GetByteCount(text)} >>\nstream\n{text}endstream");
        }
        objects[0] = "<< /Type /Catalog /Pages 2 0 R >>";
        objects[1] = $"<< /Type /Pages /Count {pageIds.Count} /Kids [{string.Join(' ', pageIds.Select(i => $"{i} 0 R"))}] >>";
        using var output = File.Create(path);
        void WriteText(string text) => output.Write(Encoding.ASCII.GetBytes(text));
        WriteText("%PDF-1.4\n"); var offsets = new List<long> { 0 };
        for (int i = 0; i < objects.Count; i++) { offsets.Add(output.Position); WriteText($"{i + 1} 0 obj\n{objects[i]}\nendobj\n"); }
        long xref = output.Position; WriteText($"xref\n0 {offsets.Count}\n0000000000 65535 f \n");
        foreach (long offset in offsets.Skip(1)) WriteText(offset.ToString("D10", CultureInfo.InvariantCulture) + " 00000 n \n");
        WriteText($"trailer\n<< /Size {offsets.Count} /Root 1 0 R >>\nstartxref\n{xref}\n%%EOF\n");
    }
}
