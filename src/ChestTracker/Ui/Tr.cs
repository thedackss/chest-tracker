using Avalonia.Markup.Xaml;

namespace ChestTracker.Ui;

/// <summary>English / Spanish text in XAML: <c>{ui:Tr 'Language', 'Idioma'}</c>. Read when the view is built.</summary>
public sealed class Tr : MarkupExtension
{
    public Tr()
    {
    }

    public Tr(string en, string es)
    {
        En = en;
        Es = es;
    }

    public string En { get; set; } = "";
    public string Es { get; set; } = "";

    /// <summary>Upper case (section headings).</summary>
    public bool Upper { get; set; }

    public override object ProvideValue(IServiceProvider serviceProvider)
    {
        var s = Text.T(En, Es);
        return Upper ? s.ToUpper(Text.Culture) : s;
    }
}
