namespace MelodyToTab.Desktop;

internal static class Theme
{
    public static readonly Color Background = Color.FromArgb(22, 25, 28), Surface = Color.FromArgb(31, 35, 39), Raised = Color.FromArgb(43, 49, 54), Text = Color.FromArgb(237, 240, 238), Muted = Color.FromArgb(174, 187, 190), Accent = Color.FromArgb(101, 219, 188), Warning = Color.FromArgb(247, 193, 107), Line = Color.FromArgb(62, 72, 78);
    public static Label Label(string text, float size = 10, bool bold = false) => new() { Text = text, ForeColor = Text, AutoSize = true, Font = new Font("Segoe UI", size, bold ? FontStyle.Bold : FontStyle.Regular), Margin = new Padding(4, 9, 12, 4) };
    public static Button Button(string text, EventHandler action, bool primary = false)
    {
        var b = new Button { Text = text, AutoSize = true, MinimumSize = new Size(82, 36), Padding = new Padding(9, 3, 9, 3), FlatStyle = FlatStyle.Flat, BackColor = primary ? Accent : Raised, ForeColor = primary ? Background : Text, Margin = new Padding(0, 4, 8, 4), Cursor = Cursors.Hand, AccessibleName = text.Replace("&", "") };
        b.FlatAppearance.BorderSize = 0; b.Click += action; return b;
    }
    public static ComboBox Combo(int width) => new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = width, BackColor = Raised, ForeColor = Text, FlatStyle = FlatStyle.Flat, Margin = new Padding(0, 9, 10, 4) };
    public static void Grid(DataGridView g)
    {
        g.BackgroundColor = Surface; g.BorderStyle = BorderStyle.None; g.GridColor = Line; g.EnableHeadersVisualStyles = false;
        g.ColumnHeadersDefaultCellStyle = new DataGridViewCellStyle { BackColor = Raised, ForeColor = Muted, Font = new Font("Segoe UI", 10), Padding = new Padding(5) };
        g.DefaultCellStyle = new DataGridViewCellStyle { BackColor = Surface, ForeColor = Text, SelectionBackColor = Color.FromArgb(45, 91, 82), SelectionForeColor = Text, Padding = new Padding(5) };
        g.AlternatingRowsDefaultCellStyle.BackColor = Color.FromArgb(35, 40, 44);
        g.RowTemplate.Height = 36; g.ColumnHeadersHeight = 38; g.RowHeadersVisible = false; g.AllowUserToAddRows = false; g.AllowUserToDeleteRows = false;
        g.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill; g.SelectionMode = DataGridViewSelectionMode.FullRowSelect; g.MultiSelect = false; g.ReadOnly = true; g.AccessibleName = "Editable melody transcription. Enter edits the selected note.";
    }
}
