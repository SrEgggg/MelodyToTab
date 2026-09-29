using MelodyToTab.Core;

namespace MelodyToTab.Desktop;

internal sealed class NoteEditor : Form
{
    public MelodyNote Result { get; private set; }
    public NoteEditor(MelodyNote note)
    {
        Result = note.Copy(); Text = "Edit melody note"; BackColor = Theme.Background; ForeColor = Theme.Text; Font = new Font("Segoe UI", 10); ClientSize = new Size(420, 350); FormBorderStyle = FormBorderStyle.FixedDialog; MaximizeBox = false; MinimizeBox = false; StartPosition = FormStartPosition.CenterParent; AutoScaleMode = AutoScaleMode.Dpi;
        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(22), ColumnCount = 2, RowCount = 6 }; layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 40)); layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 60)); Controls.Add(layout);
        var pitch = new TextBox { Text = note.Name, Dock = DockStyle.Fill, BackColor = Theme.Raised, ForeColor = Theme.Text, AccessibleName = "Pitch, note name and octave" };
        NumericUpDown Number(decimal value, decimal maximum) => new() { Minimum = 0, Maximum = maximum, DecimalPlaces = 2, Increment = .01m, Value = Math.Clamp(value, 0, maximum), Dock = DockStyle.Fill, BackColor = Theme.Raised, ForeColor = Theme.Text };
        var start = Number((decimal)note.Start, 95); var duration = Number((decimal)note.Duration, 95); duration.Minimum = .04m;
        var fingering = Theme.Combo(200); fingering.Dock = DockStyle.Fill;
        void Positions()
        {
            fingering.Items.Clear(); fingering.Items.Add("Automatic fingering");
            try { foreach (var p in GuitarArranger.Positions(Music.Parse(pitch.Text))) fingering.Items.Add($"String {p.String} / fret {p.Fret}"); } catch (FormatException) { }
            fingering.SelectedIndex = 0;
        }
        pitch.TextChanged += (_, _) => Positions(); Positions();
        if (note.Locked) fingering.SelectedIndex = Math.Max(0, fingering.Items.IndexOf($"String {note.String} / fret {note.Fret}"));
        layout.Controls.Add(Theme.Label("Pitch"), 0, 0); layout.Controls.Add(pitch, 1, 0); layout.Controls.Add(Theme.Label("Start (seconds)"), 0, 1); layout.Controls.Add(start, 1, 1); layout.Controls.Add(Theme.Label("Length (seconds)"), 0, 2); layout.Controls.Add(duration, 1, 2); layout.Controls.Add(Theme.Label("Guitar position"), 0, 3); layout.Controls.Add(fingering, 1, 3);
        var hint = Theme.Label("Use sharps: C4, F#3, A4.\nTiming must not overlap another note.", 9); layout.Controls.Add(hint, 0, 4); layout.SetColumnSpan(hint, 2);
        var buttons = new FlowLayoutPanel { AutoSize = true, Dock = DockStyle.Fill }; layout.Controls.Add(buttons, 0, 5); layout.SetColumnSpan(buttons, 2);
        var save = Theme.Button("&Apply", (_, _) =>
        {
            try
            {
                Result.Midi = Music.Parse(pitch.Text); Result.Start = (double)start.Value; Result.Duration = (double)duration.Value; Result.Confidence = 1; Result.Locked = fingering.SelectedIndex > 0;
                if (Result.Locked) (Result.String, Result.Fret) = GuitarArranger.Positions(Result.Midi)[fingering.SelectedIndex - 1];
                DialogResult = DialogResult.OK;
            }
            catch (Exception ex) { MessageBox.Show(this, ex.Message, "Check the note"); }
        }, true);
        var cancel = Theme.Button("Cancel", (_, _) => DialogResult = DialogResult.Cancel); buttons.Controls.Add(save); buttons.Controls.Add(cancel); AcceptButton = save; CancelButton = cancel;
    }
}
