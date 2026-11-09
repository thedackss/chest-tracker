using System.Globalization;

namespace ChestTracker;

/// <summary>English / Spanish UI text. Stored data never depends on the language.</summary>
public static class Text
{
    public static readonly string[] Languages = { "en", "es" };

    public static string Language { get; private set; } = Default;

    public static string Default =>
        CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "es" ? "es" : "en";

    public static CultureInfo Culture => Language == "es" ? CultureInfo.GetCultureInfo("es-MX") : CultureInfo.GetCultureInfo("en-US");

    public static void Set(string? language) => Language = Array.IndexOf(Languages, language) >= 0 ? language! : Default;

    public static string T(string en, string es) => Language == "es" ? es : en;

    public static string N(int value) => value.ToString("N0", Culture);

    /// <summary>"1 chest" / "3 chests" (or Spanish).</summary>
    public static string Count(int n, string enOne, string enMany, string esOne, string esMany) =>
        $"{N(n)} {(n == 1 ? T(enOne, esOne) : T(enMany, esMany))}";

    public static string Chests(int n) => Count(n, "chest", "chests", "cofre", "cofres");
    public static string Matches(int n) => Count(n, "match", "matches", "partida", "partidas");
}
