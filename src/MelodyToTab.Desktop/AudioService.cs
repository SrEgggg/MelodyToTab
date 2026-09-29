using MelodyToTab.Core;
using NAudio.Wave;

namespace MelodyToTab.Desktop;

internal sealed class AudioService : IDisposable
{
    private WaveInEvent? input;
    private WaveOutEvent? output;
    private RawSourceWaveStream? source;
    private readonly List<float> recording = [];
    private readonly object gate = new();
    private TaskCompletionSource<AudioClip>? stopped;
    public bool Recording => input is not null;
    public bool Playing => output?.PlaybackState == PlaybackState.Playing;
    public double Position => output is not null && source is not null ? output.GetPosition() / (double)source.WaveFormat.AverageBytesPerSecond : 0;
    public double Duration { get { lock (gate) return recording.Count / 22050.0; } }
    public float Level { get; private set; }
    public Exception? PlaybackError { get; private set; }
    public static string[] Devices() => Enumerable.Range(0, WaveIn.DeviceCount).Select(i => WaveIn.GetCapabilities(i).ProductName).ToArray();
    public AudioClip Snapshot() { lock (gate) return new(recording.ToArray(), 22050); }
    public void Start(int device)
    {
        StopPlayback();
        if (input is not null) throw new InvalidOperationException("A recording is already running.");
        lock (gate) recording.Clear();
        var completion = new TaskCompletionSource<AudioClip>(TaskCreationOptions.RunContinuationsAsynchronously); stopped = completion;
        var capture = new WaveInEvent { DeviceNumber = device, WaveFormat = new WaveFormat(22050, 16, 1), BufferMilliseconds = 50 };
        capture.DataAvailable += (_, e) =>
        {
            float peak = 0;
            lock (gate) for (int i = 0; i + 1 < e.BytesRecorded && recording.Count < 22050 * WavCodec.MaxSeconds; i += 2)
            {
                float value = BitConverter.ToInt16(e.Buffer, i) / 32768f; recording.Add(value); peak = Math.Max(peak, Math.Abs(value));
            }
            Level = peak;
        };
        capture.RecordingStopped += (_, e) =>
        {
            if (e.Exception is not null) completion.TrySetException(e.Exception);
            else completion.TrySetResult(Snapshot());
        };
        try { input = capture; capture.StartRecording(); }
        catch { input = null; capture.Dispose(); throw; }
    }
    public bool CaptureEnded => stopped?.Task.IsCompleted == true;
    public async Task<AudioClip> StopAsync()
    {
        var capture = input ?? throw new InvalidOperationException("No recording is active.");
        try { capture.StopRecording(); return await stopped!.Task.WaitAsync(TimeSpan.FromSeconds(5)); }
        finally { input = null; capture.Dispose(); Level = 0; }
    }
    public void Play(AudioClip clip)
    {
        StopPlayback(); PlaybackError = null;
        try
        {
            byte[] bytes = new byte[clip.Samples.Length * 4]; Buffer.BlockCopy(clip.Samples, 0, bytes, 0, bytes.Length);
            source = new RawSourceWaveStream(new MemoryStream(bytes), WaveFormat.CreateIeeeFloatWaveFormat(clip.SampleRate, 1));
            output = new WaveOutEvent(); output.PlaybackStopped += (_, e) => PlaybackError = e.Exception;
            output.Init(source); output.Play();
        }
        catch { StopPlayback(); throw; }
    }
    public void StopPlayback() { output?.Stop(); output?.Dispose(); output = null; source?.Dispose(); source = null; }
    public void Dispose() { input?.Dispose(); input = null; StopPlayback(); }
}
