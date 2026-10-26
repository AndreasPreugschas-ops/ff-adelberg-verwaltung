namespace Fevad.Infrastructure.Identitaet;

/// <summary>Technische Rollen. Seiten prüfen keine Rollen direkt, sondern Policies (siehe Berechtigungen).</summary>
public static class Rollen
{
    /// <summary>Verwaltet Konten, Rollen und Einstellungen.</summary>
    public const string Administrator = "Administrator";
    /// <summary>Kommandant der Gemeindefeuerwehr.</summary>
    public const string Kommandant = "Kommandant";
    /// <summary>Stellvertretender Kommandant.</summary>
    public const string Stellvertreter = "Stellvertreter";
    /// <summary>Führungskraft, z. B. Gruppen- oder Zugführer.</summary>
    public const string Fuehrungskraft = "Fuehrungskraft";
    /// <summary>Gerätewart.</summary>
    public const string Geraetewart = "Geraetewart";
    /// <summary>Atemschutzgerätewart.</summary>
    public const string Atemschutzgeraetewart = "Atemschutzgeraetewart";
    /// <summary>Kleiderwart (Kleiderkammer).</summary>
    public const string Kleiderwart = "Kleiderwart";
    /// <summary>Jugendfeuerwehrwart.</summary>
    public const string Jugendwart = "Jugendwart";
    /// <summary>Betreuer der Jugendfeuerwehr ohne Wartamt: sieht nur Anwesenheit und Notfallliste der Jugendfeuerwehr (V46).</summary>
    public const string BetreuerJugend = "BetreuerJugend";
    /// <summary>Kassier der Kameradschaftskasse.</summary>
    public const string Kassier = "Kassier";
    /// <summary>Schriftführer.</summary>
    public const string Schriftfuehrer = "Schriftfuehrer";
    /// <summary>Angehöriger der Feuerwehr mit eigenem Konto (Selbstbedienung).</summary>
    public const string Mitglied = "Mitglied";
    /// <summary>Lesezugang der Gemeindeverwaltung.</summary>
    public const string GemeindeLesen = "GemeindeLesen";
    /// <summary>Hauptamt der Gemeinde: entscheidet über den Kostenersatz und erlässt den Bescheid (V21, V26).</summary>
    public const string Hauptamt = "Hauptamt";
    /// <summary>Kämmerei der Gemeinde: liest Entschädigung, Kassen, Statistik und Inventar, richtet den Finanzexport ein (V23, V26).</summary>
    public const string Kaemmerei = "Kaemmerei";
    /// <summary>Gemeindekasse, Sachgebiet Abgaben: sieht nur den Zahlungsstatus der Kostenersatz-Forderungen und spielt Rückmeldungen ein (V26).</summary>
    public const string Abgaben = "Abgaben";

    /// <summary>
    /// Rechnungsprüfer der Kassen (V05). Wird nicht in der Benutzerverwaltung vergeben: Wer als Mitglied eine laufende Funktion mit dieser
    /// vorgeschlagenen Rolle hat (Kassenprüfer, Amtszeit aus der Wahl), bekommt sie bei der Anmeldung und verliert sie mit dem Ende der Amtszeit.
    /// </summary>
    public const string Kassenpruefer = "Kassenpruefer";

    /// <summary>Elternkonto: sieht nur die Termine der Jugend-/Kinderfeuerwehr der zugeordneten Kinder (Erziehungsberechtigter.KontoId).</summary>
    public const string Erziehungsberechtigt = "Erziehungsberechtigt";

    /// <summary>Alle mitgelieferten Rollen.</summary>
    public static readonly string[] Alle =
    [
        Administrator, Kommandant, Stellvertreter, Fuehrungskraft, Geraetewart, Atemschutzgeraetewart, Kleiderwart,
        Jugendwart, BetreuerJugend, Kassier, Schriftfuehrer, Mitglied, GemeindeLesen, Hauptamt, Kaemmerei, Abgaben, Erziehungsberechtigt, Kassenpruefer,
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
        [BetreuerJugend] = "Betreuer Jugendfeuerwehr",
        [Kassier] = "Kassier",
        [Schriftfuehrer] = "Schriftführer",
        [Mitglied] = "Mitglied",
        [GemeindeLesen] = "Gemeinde (nur lesen)",
        [Hauptamt] = "Gemeinde: Hauptamt",
        [Kaemmerei] = "Gemeinde: Kämmerei",
        [Abgaben] = "Gemeinde: Kasse / Abgaben",
        [Erziehungsberechtigt] = "Erziehungsberechtigte (Elternkonto)",
        [Kassenpruefer] = "Kassenprüfer (aus der Funktion)",
    };

    /// <summary>Anzeigename einer mitgelieferten Rolle.</summary>
    /// <param name="rolle">Technischer Rollenname.</param>
    /// <returns>Der Anzeigename; bei unbekannten Rollen der Name selbst.</returns>
    public static string Bezeichnung(string rolle) => Bezeichnungen.GetValueOrDefault(rolle, rolle);

    /// <summary>Selbst angelegte Rollen verlangen immer die Zwei-Faktor-Anmeldung, weil ihre Rechte frei wählbar sind.</summary>
    public static bool ZweiFaktorNoetig(string rolle) => !Alle.Contains(rolle) || ZweiFaktorPflicht.Contains(rolle);

    /// <summary>Mitgelieferte Rollen, für die eine Zwei-Faktor-Anmeldung Pflicht ist.</summary>
    public static readonly string[] ZweiFaktorPflicht =
    [
        Administrator, Kommandant, Stellvertreter, Geraetewart, Atemschutzgeraetewart, Kleiderwart, Jugendwart, BetreuerJugend, Kassier,
        Schriftfuehrer, GemeindeLesen, Hauptamt, Kaemmerei, Abgaben, Kassenpruefer,
    ];

    /// <summary>
    /// Rollen der Gemeindeverwaltung und der Betreuer der Jugendfeuerwehr, die erst mit einer dokumentierten Verpflichtung auf Vertraulichkeit
    /// wirken (V26, V46, Art. 29 und 32 DSGVO).
    /// Ohne Eintrag „verpflichtet am“ im Konto nimmt die Anmeldung diese Rollen nicht ins Cookie auf.
    /// </summary>
    public static readonly string[] VerpflichtungPflicht = [GemeindeLesen, Hauptamt, Kaemmerei, Abgaben, BetreuerJugend];
}
