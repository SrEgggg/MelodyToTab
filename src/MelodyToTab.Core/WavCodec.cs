using System.Text;

namespace MelodyToTab.Core;

public static class WavCodec
{
    public const int MaxSeconds = 90;
    public static AudioClip Read(Stream stream)
    {
        using var r = new BinaryReader(stream, Encoding.ASCII, true);
        string Tag() => Encoding.ASCII.GetString(r.ReadBytes(4));
        if (Tag() != "RIFF") throw new InvalidDataException("Choose a PCM or IEEE float WAV file.");
        r.ReadUInt32();
        if (Tag() != "WAVE") throw new InvalidDataException("Not a WAV recording.");
        int format = 0, channels = 0, rate = 0, bits = 0, align = 0;
        long dataAt = 0; uint dataSize = 0;
        while (stream.Position + 8 <= stream.Length)
        {
            string tag = Tag(); uint size = r.ReadUInt32(); long next = stream.Position + size + (size & 1);
            if (next > stream.Length + 1) throw new InvalidDataException("The WAV file is truncated.");
            if (tag == "fmt " && size >= 16)
            {
                format = r.ReadUInt16(); channels = r.ReadUInt16(); rate = r.ReadInt32(); r.ReadInt32(); align = r.ReadUInt16(); bits = r.ReadUInt16();
                if (format == 65534 && size >= 40) { r.ReadUInt16(); r.ReadUInt16(); r.ReadUInt32(); format = r.ReadUInt16(); }
            }
            if (tag == "data") { dataAt = stream.Position; dataSize = size; }
            stream.Position = next;
        }
        if (channels is < 1 or > 8 || rate is < 8000 or > 192000 || (format != 1 && format != 3) || (format == 3 ? bits != 32 : bits is not (8 or 16 or 24 or 32)) || align != channels * bits / 8 || dataAt == 0 || dataSize % align != 0)
            throw new InvalidDataException("Supported WAV: 8/16/24/32-bit PCM or 32-bit float, 8–192 kHz.");
        int count = checked((int)(dataSize / align));
        if (count > rate * MaxSeconds || count < rate / 5) throw new InvalidDataException("Import a recording between 0.2 and 90 seconds.");
        stream.Position = dataAt;
        var samples = new float[count];
        for (int i = 0; i < count; i++)
        {
            double sum = 0;
            for (int c = 0; c < channels; c++)
            {
                double v = format == 3 ? r.ReadSingle() : bits switch
                {
                    8 => (r.ReadByte() - 128) / 128.0,
                    16 => r.ReadInt16() / 32768.0,
                    24 => Read24(r) / 8388608.0,
                    _ => r.ReadInt32() / 2147483648.0
                };
                if (!double.IsFinite(v)) throw new InvalidDataException("WAV contains invalid samples.");
                sum += v;
            }
            samples[i] = (float)Math.Clamp(sum / channels, -1, 1);
        }
        return new(samples, rate);
    }
    private static int Read24(BinaryReader r) { int n = r.ReadByte() | r.ReadByte() << 8 | r.ReadByte() << 16; return (n << 8) >> 8; }
    public static void Write(Stream stream, AudioClip clip)
    {
        using var w = new BinaryWriter(stream, Encoding.ASCII, true);
        w.Write("RIFF"u8); w.Write(36 + clip.Samples.Length * 2); w.Write("WAVEfmt "u8); w.Write(16); w.Write((short)1); w.Write((short)1);
        w.Write(clip.SampleRate); w.Write(clip.SampleRate * 2); w.Write((short)2); w.Write((short)16); w.Write("data"u8); w.Write(clip.Samples.Length * 2);
        foreach (float v in clip.Samples) w.Write((short)(Math.Clamp(v, -1, 1) * 32767));
    }
}
