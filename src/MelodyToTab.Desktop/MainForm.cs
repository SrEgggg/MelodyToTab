using System.Globalization;
using MelodyToTab.Core;

namespace MelodyToTab.Desktop;

internal sealed class MainForm : Form
{
    private readonly AudioService audio = new();
    private Session session = new();
    private AudioClip? clip;
    private List<Harmonization> harmonies = [];
    private readonly Stack<(List<MelodyNote> Notes, int KeyRoot, bool Minor)> undo = new();
    private readonly WaveView wave = new() { Dock = DockStyle.Fill };
    private readonly TabView tab = new() { Dock = DockStyle.Fill };
    private readonly DataGridView notes = new() { Dock = DockStyle.Fill };
    private readonly FlowLayoutPanel chords = new() { Dock = DockStyle.Fill, AutoScroll = true, FlowDirection = FlowDirection.TopDown, WrapContents = false, Padding = new Padding(10) };
    private readonly ComboBox devices = Theme.Combo(220), keys = Theme.Combo(125), alternatives = Theme.Combo(240);
    private readonly NumericUpDown tempo = new() { Minimum = 40, Maximum = 240, Value = 100, Width = 64, BackColor = Theme.Raised, ForeColor = Theme.Text, Margin = new Padding(0, 9, 10, 4) };
    private readonly Label status = Theme.Label("Ready · Select your microphone, then record a melody.", 10), duration = Theme.Label("00:00.0", 12, true), range = Theme.Label("Standard tuning · E A D G B e", 10);
    private readonly ProgressBar volume = new() { Width = 95, Height = 12, Margin = new Padding(8, 17, 12, 4) }, progress = new() { Dock = DockStyle.Bottom, Height = 3 };
    private readonly System.Windows.Forms.Timer clock = new() { Interval = 60 };
    private readonly List<Control> mutationControls = [];
    private Button record = null!, stop = null!, analyze = null!, original = null!, melody = null!, accompaniment = null!;
    private bool busy, refreshing, dirty, stopping, wasPlaying;
    private string? projectPath;
    private CancellationTokenSource? cancellation;
    private Harmonization? SelectedHarmony => harmonies.ElementAtOrDefault(session.Harmony);

    public MainForm(string? initialPath)
    {
        Text = "Melody / Tab"; ClientSize = new Size(1320, 940); MinimumSize = new Size(1050, 800); BackColor = Theme.Background; ForeColor = Theme.Text; Font = new Font("Segoe UI", 10); AutoScaleMode = AutoScaleMode.Dpi; KeyPreview = true;
        BuildLayout(); LoadDevices(); RefreshScore();
        clock.Tick += async (_, _) => await Tick(); clock.Start();
        FormClosing += HandleClosing;
        KeyDown += async (_, e) =>
        {
            if (e.KeyCode == Keys.Escape) { cancellation?.Cancel(); await Stop(); e.Handled = true; }
            if (e.Control && e.KeyCode == Keys.S && !busy && !audio.Recording) { SaveProject(); e.Handled = true; }
            if (e.KeyCode == Keys.Space && !busy && !audio.Recording && ActiveControl is not (TextBox or NumericUpDown or ComboBox)) { if (audio.Playing) audio.StopPlayback(); else await Play(false); e.Handled = true; e.SuppressKeyPress = true; }
        };
        Shown += async (_, _) => { if (initialPath is not null && File.Exists(initialPath)) { if (Path.GetExtension(initialPath).Equals(".mtab", StringComparison.OrdinalIgnoreCase)) await OpenProject(initialPath); else await Import(initialPath); } };
    }
    private void BuildLayout()
    {
        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 7, Padding = new Padding(24, 16, 24, 10) };
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 60)); layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 60)); layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 200)); layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 52)); layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 52)); layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 54)); Controls.Add(layout);
        var header = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2 }; header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100)); header.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        var brand = Theme.Label("MELODY / TAB", 23, true); brand.Margin = new Padding(0, 4, 0, 0); header.Controls.Add(brand, 0, 0);
        var files = new FlowLayoutPanel { AutoSize = true, WrapContents = false, Dock = DockStyle.Fill };
        void FileButton(string name, EventHandler action) { var b = Theme.Button(name, action); files.Controls.Add(b); mutationControls.Add(b); }
        FileButton("&Open", async (_, _) => await OpenProject()); FileButton("&Save", (_, _) => SaveProject()); FileButton("PDF", (_, _) => Export(true)); FileButton("MIDI", (_, _) => Export(false)); header.Controls.Add(files, 1, 0); layout.Controls.Add(header, 0, 0);
        var recording = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = false };
        record = Theme.Button("●  &Record", async (_, _) => await StartRecording(), true); record.MinimumSize = new Size(132, 40);
        stop = Theme.Button("■  Stop", async (_, _) => await Stop());
        var retry = Theme.Button("Retry", async (_, _) => await StartRecording()); var import = Theme.Button("Import &WAV", async (_, _) => await Import());
        var refresh = Theme.Button("Refresh mics", (_, _) => LoadDevices());
        devices.AccessibleName = "Recording microphone"; volume.AccessibleName = "Microphone input peak level";
        recording.Controls.AddRange([record, stop, import, devices, refresh, retry, volume, duration]); mutationControls.AddRange([record, import, devices, refresh, retry]); layout.Controls.Add(recording, 0, 1); layout.Controls.Add(wave, 0, 2);
        var transport = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = false };
        analyze = Theme.Button("Analyze", async (_, _) => await Analyze()); original = Theme.Button("▶  Original", (_, _) => PlayOriginal()); melody = Theme.Button("▶  Melody", async (_, _) => await Play(false)); accompaniment = Theme.Button("▶  With chords", async (_, _) => await Play(true));
        transport.Controls.AddRange([analyze, original, melody, accompaniment]); mutationControls.AddRange([analyze, original, melody, accompaniment]);
        var caption = Theme.Label("01  CAPTURE     /     02  REVIEW     /     03  ARRANGE", 9); caption.ForeColor = Theme.Muted; caption.Margin = new Padding(20, 14, 0, 0); transport.Controls.Add(caption); layout.Controls.Add(transport, 0, 3);
        var settings = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = false }; settings.Controls.Add(Theme.Label("Key"));
        for (int min = 0; min < 2; min++) for (int root = 0; root < 12; root++) keys.Items.Add(Music.Names[root] + (min == 1 ? " minor" : " major"));
        keys.SelectedIndex = 0; keys.AccessibleName = "Key override"; tempo.AccessibleName = "Tempo override in beats per minute";
        keys.SelectedIndexChanged += (_, _) => { if (refreshing) return; audio.StopPlayback(); session.KeyRoot = keys.SelectedIndex % 12; session.Minor = keys.SelectedIndex >= 12; dirty = true; RefreshScore(false); };
        tempo.ValueChanged += (_, _) => { if (refreshing) return; audio.StopPlayback(); session.Tempo = (int)tempo.Value; dirty = true; RefreshScore(false); };
        settings.Controls.Add(keys); settings.Controls.Add(Theme.Label("BPM")); settings.Controls.Add(tempo);
        var down = Theme.Button("− Octave", (_, _) => Transpose(-12)); var up = Theme.Button("+ Octave", (_, _) => Transpose(12)); var semiDown = Theme.Button("−1", (_, _) => Transpose(-1)); var semiUp = Theme.Button("+1", (_, _) => Transpose(1));
        settings.Controls.AddRange([down, up, semiDown, semiUp, range]); mutationControls.AddRange([keys, tempo, down, up, semiDown, semiUp]); layout.Controls.Add(settings, 0, 4);
        var split = new SplitContainer { Dock = DockStyle.Fill, BackColor = Theme.Background, SplitterWidth = 12, FixedPanel = FixedPanel.Panel2 }; split.Size = new Size(1250, 440); split.SplitterDistance = 890; split.Panel1MinSize = 500; split.Panel2MinSize = 310; layout.Controls.Add(split, 0, 5);
        var pages = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2 }; pages.RowStyles.Add(new RowStyle(SizeType.Absolute, 48)); pages.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        var navigation = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = false }; var workArea = new Panel { Dock = DockStyle.Fill, BackColor = Theme.Surface }; pages.Controls.Add(navigation, 0, 0); pages.Controls.Add(workArea, 0, 1);
        var transcription = new Panel { Dock = DockStyle.Fill, BackColor = Theme.Surface, Padding = new Padding(8) }; var tablature = new Panel { Dock = DockStyle.Fill, BackColor = Theme.Surface, Visible = false }; workArea.Controls.Add(transcription); workArea.Controls.Add(tablature); split.Panel1.Controls.Add(pages);
        var reviewButton = Theme.Button("Transcription", (_, _) => { }, true); var tabButton = Theme.Button("Guitar tablature", (_, _) => { });
        void SelectPage(bool guitar) { transcription.Visible = !guitar; tablature.Visible = guitar; reviewButton.BackColor = guitar ? Theme.Raised : Theme.Accent; reviewButton.ForeColor = guitar ? Theme.Text : Theme.Background; tabButton.BackColor = guitar ? Theme.Accent : Theme.Raised; tabButton.ForeColor = guitar ? Theme.Background : Theme.Text; }
        reviewButton.Click += (_, _) => SelectPage(false); tabButton.Click += (_, _) => SelectPage(true); navigation.Controls.AddRange([reviewButton, tabButton]);
        var editLayout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3 }; editLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 35)); editLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); editLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 49)); transcription.Controls.Add(editLayout);
        var hint = Theme.Label("Double-click a note to edit pitch, timing or fingering.  ? = review confidence", 9); hint.ForeColor = Theme.Muted; editLayout.Controls.Add(hint, 0, 0);
        Theme.Grid(notes); foreach (var (id, title) in new[] { ("pitch", "Note"), ("start", "Start · s"), ("length", "Length · s"), ("position", "String / fret"), ("confidence", "Confidence") }) notes.Columns.Add(id, title);
        foreach (DataGridViewColumn column in notes.Columns) column.SortMode = DataGridViewColumnSortMode.NotSortable;
        notes.CellDoubleClick += (_, e) => { if (e.RowIndex >= 0) EditNote(false); }; notes.KeyDown += (_, e) => { if (e.KeyCode == Keys.Enter) { EditNote(false); e.Handled = true; } if (e.KeyCode == Keys.Delete) { DeleteNote(); e.Handled = true; } }; editLayout.Controls.Add(notes, 0, 1);
        var editing = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = false };
        var edit = Theme.Button("&Edit note", (_, _) => EditNote(false)); var add = Theme.Button("Add note", (_, _) => EditNote(true)); var delete = Theme.Button("Delete", (_, _) => DeleteNote()); var undoButton = Theme.Button("Undo edit", (_, _) => Undo()); editing.Controls.AddRange([edit, add, delete, undoButton]); mutationControls.AddRange([edit, add, delete, undoButton, notes]); editLayout.Controls.Add(editing, 0, 2); tablature.Controls.Add(tab);
        var harmonyPanel = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 4 }; harmonyPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 46)); harmonyPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 36)); harmonyPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 42)); harmonyPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); split.Panel2.Controls.Add(harmonyPanel);
        harmonyPanel.Controls.Add(Theme.Label("Possible harmonizations", 14, true), 0, 0); var subtitle = Theme.Label("A melody can support many chord progressions.", 9); subtitle.ForeColor = Theme.Muted; harmonyPanel.Controls.Add(subtitle, 0, 1);
        alternatives.Items.AddRange(["Closest fit", "Alternative color", "Beginner · open chords"]); alternatives.SelectedIndex = 0; alternatives.AccessibleName = "Harmonization alternative"; alternatives.SelectedIndexChanged += (_, _) => { if (refreshing) return; audio.StopPlayback(); session.Harmony = alternatives.SelectedIndex; dirty = true; RefreshChords(); }; mutationControls.Add(alternatives); harmonyPanel.Controls.Add(alternatives, 0, 2); harmonyPanel.Controls.Add(chords, 0, 3); chords.SizeChanged += (_, _) => { foreach (Control c in chords.Controls) c.Width = Math.Max(260, chords.ClientSize.Width - 30); };
        status.AutoSize = false; status.Dock = DockStyle.Fill; status.ForeColor = Theme.Muted; status.Margin = new Padding(0, 10, 0, 0); status.AccessibleName = "Application status"; layout.Controls.Add(status, 0, 6); Controls.Add(progress);
    }
    private void LoadDevices()
    {
        try { devices.Items.Clear(); devices.Items.AddRange(AudioService.Devices()); if (devices.Items.Count > 0) devices.SelectedIndex = 0; else status.Text = "No microphones found. Connect one and refresh, or import a WAV recording."; }
        catch (Exception ex) { Error(ex, "Could not list microphones"); }
        SetEnabled();
    }
    private void SetEnabled()
    {
        foreach (var c in mutationControls) c.Enabled = !busy && !audio.Recording;
        record.Enabled = !busy && !audio.Recording && devices.SelectedIndex >= 0;
        analyze.Enabled = !busy && !audio.Recording && clip is not null;
        original.Enabled = !busy && !audio.Recording && clip is not null;
        melody.Enabled = accompaniment.Enabled = !busy && !audio.Recording && session.Notes.Count > 0;
        stop.Enabled = busy || audio.Recording || audio.Playing;
    }
    private bool CanReplace() => !dirty || MessageBox.Show(this, "Discard the unsaved changes in this project?", "Unsaved melody", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes;
    private async Task StartRecording()
    {
        if (busy || audio.Recording || !CanReplace()) return;
        try
        {
            audio.Start(devices.SelectedIndex); session = new(); clip = null; projectPath = null; undo.Clear(); dirty = true; RefreshScore();
            status.Text = "Recording · Hum clearly, 15–30 cm from the mic. Stop when your phrase is complete. Maximum 90 seconds.";
        }
        catch (Exception ex) { Error(ex, "Microphone unavailable. Check Windows Settings > Privacy & security > Microphone, and close other recording apps."); }
        SetEnabled(); await Task.CompletedTask;
    }
    private async Task Stop()
    {
        cancellation?.Cancel(); audio.StopPlayback();
        if (audio.Recording && !stopping)
        {
            stopping = true;
            try
            {
                clip = await audio.StopAsync(); wave.Audio = clip; dirty = true;
                if (clip.Duration < .2)
                {
                    clip = null; wave.Audio = null; duration.Text = "00:00.0";
                    status.Text = "Recording was too short. Record at least 0.2 seconds.";
                }
                else await Analyze();
            }
            catch (Exception ex) { Error(ex, "Recording stopped unexpectedly. Retry or import a WAV."); }
            finally { stopping = false; }
        }
        wave.Playhead = tab.Playhead = -1; wave.Invalidate(); tab.Invalidate(); SetEnabled();
    }
    private async Task Tick()
    {
        if (audio.Recording)
        {
            double seconds = audio.Duration; duration.Text = TimeSpan.FromSeconds(seconds).ToString(@"mm\:ss\.f"); volume.Value = (int)Math.Clamp(audio.Level * 100, 0, 100); wave.Audio = audio.Snapshot(); wave.Invalidate();
            if ((seconds >= 90 || audio.CaptureEnded) && !stopping) await Stop();
        }
        else if (audio.Playing)
        {
            wasPlaying = true; double position = audio.Position; wave.Playhead = tab.Playhead = position; wave.Invalidate(); tab.Invalidate();
            foreach (var view in chords.Controls.OfType<ChordView>()) { bool active = position >= view.Bar.Start && position < view.Bar.Start + view.Bar.Duration; if (view.Active != active) { view.Active = active; view.Invalidate(); if (active) chords.ScrollControlIntoView(view); } }
            int row = session.Notes.FindIndex(n => position >= n.Start && position < n.Start + n.Duration);
            if (row >= 0 && row < notes.Rows.Count && !notes.Rows[row].Selected) { notes.ClearSelection(); notes.Rows[row].Selected = true; if (!notes.Rows[row].Displayed) notes.FirstDisplayedScrollingRowIndex = row; tab.Follow(session.Notes[row]); }
        }
        else if (wasPlaying)
        {
            wasPlaying = false; wave.Playhead = tab.Playhead = -1; wave.Invalidate(); tab.Invalidate(); foreach (var view in chords.Controls.OfType<ChordView>()) { view.Active = false; view.Invalidate(); }
            if (audio.PlaybackError is { } error) Error(error, "Playback device became unavailable."); SetEnabled();
        }
    }
    private async Task Run(string message, Func<CancellationToken, Task> action)
    {
        if (busy) return; busy = true; cancellation = new(); SetEnabled(); status.Text = message; progress.Value = 0;
        try { await action(cancellation.Token); }
        catch (OperationCanceledException) { status.Text = "Operation cancelled. Your previous transcription is retained."; }
        catch (Exception ex) { Error(ex, "Unable to complete this operation"); }
        finally { busy = false; cancellation.Dispose(); cancellation = null; progress.Value = 0; SetEnabled(); }
    }
    private async Task Import(string? path = null)
    {
        if (busy || audio.Recording || !CanReplace()) return;
        if (path is null) { using var dialog = new OpenFileDialog { Filter = "WAV audio (*.wav)|*.wav" }; if (dialog.ShowDialog(this) != DialogResult.OK) return; path = dialog.FileName; }
        bool imported = false;
        await Run("Reading recording…", async ct =>
        {
            var loaded = await Task.Run(() => { using var f = File.OpenRead(path); return WavCodec.Read(f); }, ct);
            ct.ThrowIfCancellationRequested(); audio.StopPlayback(); clip = loaded; session = new() { Title = Path.GetFileNameWithoutExtension(path) }; undo.Clear(); projectPath = null; dirty = true; imported = true; RefreshScore();
        });
        if (imported) await Analyze();
    }
    private async Task Analyze()
    {
        if (clip is null || busy || audio.Recording) return;
        if (session.Notes.Count > 0 && MessageBox.Show(this, "Replace the current note edits with a new analysis?", "Analyze recording", MessageBoxButtons.YesNo) == DialogResult.No) return;
        audio.StopPlayback();
        await Run("Listening for stable pitches…  Esc cancels.", async ct =>
        {
            var reporter = new Progress<int>(p => progress.Value = Math.Clamp(p, 0, 100));
            var result = await Task.Run(() => PitchAnalyzer.Analyze(clip, reporter, ct), ct); ct.ThrowIfCancellationRequested();
            undo.Clear(); session.Notes = result.Notes; session.Tempo = result.Tempo; session.KeyRoot = result.KeyRoot; session.Minor = result.Minor; dirty = true; RefreshScore(); status.Text = result.Message;
        });
    }
    private void RefreshScore(bool controls = true)
    {
        GuitarArranger.Assign(session.Notes); harmonies = HarmonyEngine.Suggest(session);
        if (controls) { refreshing = true; keys.SelectedIndex = session.KeyRoot + (session.Minor ? 12 : 0); tempo.Value = session.Tempo; alternatives.SelectedIndex = session.Harmony; refreshing = false; }
        int selected = notes.CurrentRow?.Index ?? -1; notes.Rows.Clear();
        foreach (var n in session.Notes)
        {
            int row = notes.Rows.Add(n.Name, n.Start.ToString("0.00", CultureInfo.InvariantCulture), n.Duration.ToString("0.00", CultureInfo.InvariantCulture), n.String == 0 ? "Transpose needed" : $"{n.String} / {n.Fret}" + (n.Locked ? " · locked" : ""), $"{n.Confidence:P0}" + (n.Confidence < .85 ? " ?" : ""));
            if (n.String == 0 || n.Confidence < .85) notes.Rows[row].DefaultCellStyle.ForeColor = Theme.Warning;
        }
        if (selected >= 0 && selected < notes.Rows.Count) notes.CurrentCell = notes.Rows[selected].Cells[0];
        wave.Audio = clip; wave.Notes = session.Notes; wave.Invalidate(); tab.Session = session;
        int outOfRange = session.Notes.Count(n => n.String == 0); range.Text = outOfRange > 0 ? $"{outOfRange} notes need transposition" : "Standard tuning · E A D G B e"; range.ForeColor = outOfRange > 0 ? Theme.Warning : Theme.Muted;
        duration.Text = TimeSpan.FromSeconds(clip?.Duration ?? 0).ToString(@"mm\:ss\.f"); RefreshChords(); SetEnabled();
    }
    private void RefreshChords()
    {
        chords.SuspendLayout(); foreach (Control c in chords.Controls.Cast<Control>().ToArray()) c.Dispose(); chords.Controls.Clear();
        if (SelectedHarmony is { } h) foreach (var bar in h.Bars) chords.Controls.Add(new ChordView(bar) { Width = Math.Max(260, chords.ClientSize.Width - 30) });
        else { var empty = Theme.Label("Chords follow your melody.\n\nRecord or import audio to begin.\n\n4/4 measures start at 0 seconds.\nTempo changes the measure guide,\nnot the original note timing.", 10); empty.ForeColor = Theme.Muted; chords.Controls.Add(empty); }
        chords.ResumeLayout(); tab.Harmony = SelectedHarmony; tab.Invalidate();
    }
    private void EditNote(bool add)
    {
        if (busy || audio.Recording) return; int index = notes.CurrentRow?.Index ?? -1; if (!add && (index < 0 || index >= session.Notes.Count)) return;
        var source = add ? new MelodyNote { Midi = 60, Start = session.Notes.Select(n => n.Start + n.Duration).DefaultIfEmpty(0).Max() + .1, Duration = .5 } : session.Notes[index];
        using var editor = new NoteEditor(source); if (editor.ShowDialog(this) != DialogResult.OK) return;
        var proposed = session.Notes.Select(n => n.Copy()).ToList(); if (add) proposed.Add(editor.Result); else proposed[index] = editor.Result; proposed = proposed.OrderBy(n => n.Start).ToList();
        try { Music.Validate(new Session { Notes = proposed }); PushUndo(); session.Notes = proposed; dirty = true; RefreshScore(); } catch (Exception ex) { Error(ex, "Check note timing"); }
    }
    private void PushUndo() { audio.StopPlayback(); undo.Push((session.Notes.Select(n => n.Copy()).ToList(), session.KeyRoot, session.Minor)); }
    private void DeleteNote() { int row = notes.CurrentRow?.Index ?? -1; if (busy || audio.Recording || row < 0 || row >= session.Notes.Count) return; PushUndo(); session.Notes.RemoveAt(row); dirty = true; RefreshScore(); }
    private void Undo() { if (undo.Count == 0) return; audio.StopPlayback(); var previous = undo.Pop(); session.Notes = previous.Notes; session.KeyRoot = previous.KeyRoot; session.Minor = previous.Minor; dirty = true; RefreshScore(); }
    private void Transpose(int semitones)
    {
        if (session.Notes.Count == 0) return;
        try { PushUndo(); GuitarArranger.Transpose(session, semitones); dirty = true; RefreshScore(); status.Text = $"Melody transposed {semitones:+0;-0} semitones. The original recording stays at its recorded pitch."; } catch (Exception ex) { Error(ex, "Could not transpose"); }
    }
    private void PlayOriginal() { if (clip is null) return; try { audio.Play(clip); SetEnabled(); status.Text = "Playing original recording."; } catch (Exception ex) { Error(ex, "Audio output unavailable"); } }
    private async Task Play(bool withChords)
    {
        if (session.Notes.Count == 0 || busy || audio.Recording) return;
        audio.StopPlayback(); await Run("Preparing local synthesis…", async ct => { var rendered = await Task.Run(() => Synthesizer.Render(session, withChords ? SelectedHarmony : null, ct), ct); ct.ThrowIfCancellationRequested(); audio.Play(rendered); status.Text = withChords ? "Playing melody with the selected harmonization." : "Playing the transcribed melody. Space stops playback."; });
    }
    private void SaveProject()
    {
        using var dialog = new SaveFileDialog { Filter = "Melody / Tab project (*.mtab)|*.mtab", FileName = projectPath ?? session.Title + ".mtab" }; if (dialog.ShowDialog(this) != DialogResult.OK) return;
        try { ProjectStore.Save(dialog.FileName, session, clip); projectPath = dialog.FileName; dirty = false; status.Text = "Project saved with recording, note edits, fingerings and arrangement settings."; } catch (Exception ex) { Error(ex, "Could not save project"); }
    }
    private async Task OpenProject(string? path = null)
    {
        if (busy || audio.Recording || !CanReplace()) return;
        if (path is null) { using var dialog = new OpenFileDialog { Filter = "Melody / Tab project (*.mtab)|*.mtab" }; if (dialog.ShowDialog(this) != DialogResult.OK) return; path = dialog.FileName; }
        await Run("Opening project…", async ct => { var loaded = await Task.Run(() => ProjectStore.Load(path), ct); ct.ThrowIfCancellationRequested(); audio.StopPlayback(); session = loaded.Session; clip = loaded.Audio; projectPath = path; undo.Clear(); dirty = false; RefreshScore(); status.Text = "Project restored. Original audio and note edits are ready."; });
    }
    private void Export(bool pdf)
    {
        if (session.Notes.Count == 0) { status.Text = "Record, import or add notes before exporting."; return; }
        using var dialog = new SaveFileDialog { Filter = pdf ? "Printable PDF (*.pdf)|*.pdf" : "MIDI melody (*.mid)|*.mid", FileName = session.Title + (pdf ? ".pdf" : ".mid") }; if (dialog.ShowDialog(this) != DialogResult.OK) return;
        try { if (pdf) PdfExporter.Write(dialog.FileName, session, SelectedHarmony); else MidiExporter.Write(dialog.FileName, session); status.Text = pdf ? "PDF exported with tablature, chord names and exact timing." : "MIDI exported with melody pitches, timing and tempo."; } catch (Exception ex) { Error(ex, "Export failed"); }
    }
    private void Error(Exception ex, string context) { status.Text = context; MessageBox.Show(this, context + "\n\n" + ex.Message, "Melody / Tab", MessageBoxButtons.OK, MessageBoxIcon.Information); }
    private void HandleClosing(object? sender, FormClosingEventArgs e)
    {
        if (busy || audio.Recording) { e.Cancel = true; cancellation?.Cancel(); status.Text = "Stop the recording or wait for cancellation before closing."; return; }
        if (!CanReplace()) { e.Cancel = true; return; }
        clock.Stop(); clock.Dispose(); audio.Dispose();
    }
}
