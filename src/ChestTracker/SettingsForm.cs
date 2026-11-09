namespace ChestTracker;

/// <summary>Settings window, also shown on first launch as the welcome screen.</summary>
public sealed class SettingsForm : Form
{
    private readonly Settings _settings;
    private readonly int _trackedQuestChests;
    private readonly ComboBox _language = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 160 };
    private readonly NumericUpDown _questNow = new() { Maximum = 1_000_000, Width = 110 };
    private readonly Label _startInfo = new() { AutoSize = true, ForeColor = SystemColors.GrayText };
    private readonly TextBox _folder = new() { Width = 340 };
    private readonly CheckBox _autostart = new() { AutoSize = true };
    private readonly CheckBox _overlay = new() { AutoSize = true };
    private readonly CheckBox _onlyInGame = new() { AutoSize = true };
    private readonly ComboBox _size = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 90 };
    private static readonly int[] Sizes = { 75, 90, 100, 115, 130, 150, 175 };

    /// <param name="trackedQuestChests">Quest chests counted from replays so far (without the starting count).</param>
    public SettingsForm(Settings settings, int trackedQuestChests, bool welcome)
    {
        _settings = settings;
        _trackedQuestChests = trackedQuestChests;

        Text = welcome ? Text_("Welcome to chest-tracker", "Bienvenido a chest-tracker") : Text_("chest-tracker settings", "Ajustes de chest-tracker");
        Icon = ChestArt.AppIcon();
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = MinimizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;
        AutoSize = true;
        AutoSizeMode = AutoSizeMode.GrowAndShrink;
        Font = new Font("Segoe UI", 9.5f);
        Padding = new Padding(14);

        var layout = new TableLayoutPanel { ColumnCount = 2, AutoSize = true, Dock = DockStyle.Fill };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

        if (welcome)
        {
            var intro = new Label
            {
                AutoSize = true,
                MaximumSize = new Size(520, 0),
                Margin = new Padding(0, 0, 0, 12),
                Text = Text_(
                    "Counts every chest you open, dig up or break, from Fortnite's replay files.\n\nIn Fortnite, turn on Settings → Game → Replays → Record Replays, or there is nothing to count.",
                    "Cuenta cada cofre que abres, desentierras o rompes, a partir de las repeticiones de Fortnite.\n\nEn Fortnite, activa Ajustes → Juego → Repeticiones → Grabar repeticiones, o no habrá nada que contar."),
            };
            layout.Controls.Add(intro);
            layout.SetColumnSpan(intro, 2);
        }

        _language.Items.AddRange(new object[] { "English", "Español" });
        _language.SelectedIndex = Array.IndexOf(ChestTracker.Text.Languages, settings.Language ?? ChestTracker.Text.Language) is var li && li >= 0 ? li : 0;
        AddRow(layout, Text_("Language", "Idioma"), _language);

        // Starting count: asked as "where is your quest now", which is what the player can actually see.
        _questNow.Value = Math.Clamp(settings.StartingChests + trackedQuestChests, 0, (int)_questNow.Maximum);
        _questNow.ValueChanged += (_, _) => UpdateStartInfo();
        var questRow = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.TopDown, WrapContents = false, Margin = Padding.Empty };
        questRow.Controls.Add(_questNow);
        questRow.Controls.Add(_startInfo);
        AddRow(layout, Text_("Your \"Open treasure chests\"\nquest is at", "Tu misión \"Abre cofres\nde tesoro\" va en"), questRow);
        UpdateStartInfo();

        _folder.Text = settings.DemosFolder;
        var browse = new Button { Text = "…", Width = 32, Height = _folder.Height };
        browse.Click += (_, _) =>
        {
            using var dlg = new FolderBrowserDialog { SelectedPath = _folder.Text };
            if (dlg.ShowDialog(this) == DialogResult.OK) _folder.Text = dlg.SelectedPath;
        };
        var folderRow = new FlowLayoutPanel { AutoSize = true, WrapContents = false, Margin = Padding.Empty };
        folderRow.Controls.Add(_folder);
        folderRow.Controls.Add(browse);
        AddRow(layout, Text_("Replays folder", "Carpeta de repeticiones"), folderRow);

        _autostart.Text = Text_("Start with Windows and wait for Fortnite in the tray", "Iniciar con Windows y esperar a Fortnite en la bandeja");
        _autostart.Checked = settings.StartWithFortnite;
        AddRow(layout, "", _autostart);

        _overlay.Text = Text_("Show the overlay (Ctrl+Shift+O)", "Mostrar el overlay (Ctrl+Shift+O)");
        _overlay.Checked = settings.ShowOverlay;
        AddRow(layout, Text_("Overlay", "Overlay"), _overlay);
        _onlyInGame.Text = Text_("Only while Fortnite is running", "Solo mientras Fortnite está abierto");
        _onlyInGame.Checked = settings.OverlayOnlyInGame;
        AddRow(layout, "", _onlyInGame);
        _size.Items.AddRange(Sizes.Select(s => (object)$"{s} %").ToArray());
        _size.SelectedIndex = Math.Max(0, Array.IndexOf(Sizes, settings.OverlaySize) is var si && si >= 0 ? si : Array.IndexOf(Sizes, 100));
        AddRow(layout, Text_("Overlay size", "Tamaño del overlay"), _size);

        var hint = new Label
        {
            AutoSize = true,
            MaximumSize = new Size(520, 0),
            ForeColor = SystemColors.GrayText,
            Margin = new Padding(0, 8, 0, 8),
            Text = Text_(
                "The overlay shows over Fortnite in Windowed Fullscreen (Settings → Video → Window Mode). Ctrl+Shift+L locks/unlocks it; unlocked, drag it where you want.",
                "El overlay se ve sobre Fortnite en Pantalla completa en ventana (Ajustes → Vídeo → Modo de ventana). Ctrl+Shift+L lo fija/desfija; sin fijar, arrástralo donde quieras."),
        };
        layout.Controls.Add(hint);
        layout.SetColumnSpan(hint, 2);

        var ok = new Button { Text = welcome ? Text_("Start", "Empezar") : Text_("Save", "Guardar"), DialogResult = DialogResult.OK, AutoSize = true };
        var cancel = new Button { Text = Text_("Cancel", "Cancelar"), DialogResult = DialogResult.Cancel, AutoSize = true };
        ok.Click += (_, _) => Apply();
        var buttons = new FlowLayoutPanel { FlowDirection = FlowDirection.RightToLeft, AutoSize = true, Dock = DockStyle.Fill, Margin = new Padding(0, 6, 0, 0) };
        buttons.Controls.Add(ok);
        if (!welcome) buttons.Controls.Add(cancel);
        layout.Controls.Add(buttons);
        layout.SetColumnSpan(buttons, 2);

        Controls.Add(layout);
        AcceptButton = ok;
        CancelButton = welcome ? null : cancel;
    }

    private string Text_(string en, string es) => ChestTracker.Text.T(en, es);

    private void UpdateStartInfo()
    {
        var start = (int)_questNow.Value - _trackedQuestChests;
        _startInfo.Text = start >= 0
            ? Text_($"{ChestTracker.Text.N(_trackedQuestChests)} from replays + {ChestTracker.Text.N(start)} from before",
                    $"{ChestTracker.Text.N(_trackedQuestChests)} de repeticiones + {ChestTracker.Text.N(start)} de antes")
            : Text_($"Lower than the {ChestTracker.Text.N(_trackedQuestChests)} already counted from replays",
                    $"Menos que los {ChestTracker.Text.N(_trackedQuestChests)} ya contados de repeticiones");
    }

    private static void AddRow(TableLayoutPanel layout, string label, Control control)
    {
        layout.Controls.Add(new Label { Text = label, AutoSize = true, Anchor = AnchorStyles.Left | AnchorStyles.Top, Margin = new Padding(0, 6, 12, 6) });
        control.Margin = new Padding(0, 3, 0, 3);
        layout.Controls.Add(control);
    }

    private void Apply()
    {
        _settings.Language = ChestTracker.Text.Languages[Math.Max(0, _language.SelectedIndex)];
        _settings.StartingChests = Math.Max(0, (int)_questNow.Value - _trackedQuestChests);
        _settings.DemosFolder = string.IsNullOrWhiteSpace(_folder.Text) ? Settings.DefaultDemosFolder : _folder.Text.Trim();
        _settings.StartWithFortnite = _autostart.Checked;
        _settings.ShowOverlay = _overlay.Checked;
        _settings.OverlayOnlyInGame = _onlyInGame.Checked;
        _settings.OverlaySize = Sizes[Math.Max(0, _size.SelectedIndex)];
    }
}
