namespace FfAdelberg.Infrastructure.Identitaet;

/// <summary>Technische Rollen. Seiten prüfen keine Rollen direkt, sondern Policies (siehe Berechtigungen).</summary>
public static class Rollen
{
    public const string Administrator = "Administrator";
    public const string Kommandant = "Kommandant";
    public const string Stellvertreter = "Stellvertreter";
    public const string Fuehrungskraft = "Fuehrungskraft";
    public const string Geraetewart = "Geraetewart";
    public const string Atemschutzgeraetewart = "Atemschutzgeraetewart";
    public const string Jugendwart = "Jugendwart";
    public const string Kassier = "Kassier";
    public const string Schriftfuehrer = "Schriftfuehrer";
    public const string Mitglied = "Mitglied";
    public const string GemeindeLesen = "GemeindeLesen";

    public static readonly string[] Alle =
    [
        Administrator, Kommandant, Stellvertreter, Fuehrungskraft, Geraetewart, Atemschutzgeraetewart,
        Jugendwart, Kassier, Schriftfuehrer, Mitglied, GemeindeLesen,
    ];

    /// <summary>Rollen, für die eine Zwei-Faktor-Anmeldung Pflicht ist.</summary>
    public static readonly string[] ZweiFaktorPflicht =
    [
        Administrator, Kommandant, Stellvertreter, Geraetewart, Atemschutzgeraetewart, Jugendwart, Kassier,
        Schriftfuehrer, GemeindeLesen,
    ];
}
