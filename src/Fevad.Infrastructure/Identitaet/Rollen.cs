namespace Fevad.Infrastructure.Identitaet;

/// <summary>Technische Rollen. Seiten prüfen keine Rollen direkt, sondern Policies (siehe Berechtigungen).</summary>
public static class Rollen
{
    public const string Administrator = "Administrator";
    public const string Kommandant = "Kommandant";
    public const string Stellvertreter = "Stellvertreter";
    public const string Fuehrungskraft = "Fuehrungskraft";
    public const string Geraetewart = "Geraetewart";
    public const string Atemschutzgeraetewart = "Atemschutzgeraetewart";
    public const string Kleiderwart = "Kleiderwart";
    public const string Jugendwart = "Jugendwart";
    public const string Kassier = "Kassier";
    public const string Schriftfuehrer = "Schriftfuehrer";
    public const string Mitglied = "Mitglied";
    public const string GemeindeLesen = "GemeindeLesen";

    public static readonly string[] Alle =
    [
        Administrator, Kommandant, Stellvertreter, Fuehrungskraft, Geraetewart, Atemschutzgeraetewart, Kleiderwart,
        Jugendwart, Kassier, Schriftfuehrer, Mitglied, GemeindeLesen,
    ];

    /// <summary>Anzeigenamen für die Oberfläche.</summary>
    public static readonly IReadOnlyDictionary<string, string> Bezeichnungen = new Dictionary<string, string>
    {
        [Administrator] = "Administrator",
        [Kommandant] = "Kommandant",
        [Stellvertreter] = "Stellvertretender Kommandant",
        [Fuehrungskraft] = "Führungskraft",
        [Geraetewart] = "Gerätewart",
        [Atemschutzgeraetewart] = "Atemschutzgerätewart",
        [Kleiderwart] = "Kleiderwart",
        [Jugendwart] = "Jugendwart",
        [Kassier] = "Kassier",
        [Schriftfuehrer] = "Schriftführer",
        [Mitglied] = "Mitglied",
        [GemeindeLesen] = "Gemeinde (nur lesen)",
    };

    public static string Bezeichnung(string rolle) => Bezeichnungen.GetValueOrDefault(rolle, rolle);

    /// <summary>Rollen, für die eine Zwei-Faktor-Anmeldung Pflicht ist.</summary>
    public static readonly string[] ZweiFaktorPflicht =
    [
        Administrator, Kommandant, Stellvertreter, Geraetewart, Atemschutzgeraetewart, Kleiderwart, Jugendwart, Kassier,
        Schriftfuehrer, GemeindeLesen,
    ];
}
