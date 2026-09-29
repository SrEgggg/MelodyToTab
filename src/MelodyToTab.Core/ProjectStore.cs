using System.IO.Compression;
using System.Text.Json;

namespace MelodyToTab.Core;

public static class ProjectStore
{
    public static void Save(string path, Session session, AudioClip? audio)
    {
        Music.Validate(session);
        string temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            using (var zip = ZipFile.Open(temporary, ZipArchiveMode.Create))
            {
                using (var json = zip.CreateEntry("project.json").Open()) JsonSerializer.Serialize(json, session, new JsonSerializerOptions { WriteIndented = true });
                if (audio is not null) using (var wav = zip.CreateEntry("recording.wav").Open()) WavCodec.Write(wav, audio);
            }
            File.Move(temporary, path, true);
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }
    public static (Session Session, AudioClip? Audio) Load(string path)
    {
        using var zip = ZipFile.OpenRead(path);
        var entry = zip.GetEntry("project.json") ?? throw new InvalidDataException("Project metadata is missing.");
        if (entry.Length > 2_000_000) throw new InvalidDataException("Project metadata is too large.");
        Session session;
        using (var json = entry.Open()) session = JsonSerializer.Deserialize<Session>(json) ?? throw new InvalidDataException("Invalid project.");
        Music.Validate(session); session.Notes = session.Notes.OrderBy(n => n.Start).ToList();
        GuitarArranger.Assign(session.Notes);
        AudioClip? clip = null;
        if (zip.GetEntry("recording.wav") is { } wav)
        {
            if (wav.Length > 40_000_000) throw new InvalidDataException("Project audio is too large.");
            using var source = wav.Open(); using var memory = new MemoryStream(); source.CopyTo(memory); memory.Position = 0; clip = WavCodec.Read(memory);
        }
        return (session, clip);
    }
}
