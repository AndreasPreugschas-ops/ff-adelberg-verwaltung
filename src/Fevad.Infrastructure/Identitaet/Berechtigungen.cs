using Microsoft.AspNetCore.Authorization;
using static Fevad.Infrastructure.Identitaet.Rollen;

namespace Fevad.Infrastructure.Identitaet;

/// <summary>Zentrale Zuordnung Policy → Rollen. Neue Rechte nur hier eintragen.</summary>
public static class Berechtigungen
{
    public const string MitgliederLesen = "Mitglieder.Lesen";
    public const string MitgliederBearbeiten = "Mitglieder.Bearbeiten";
    public const string AtemschutzLesen = "Atemschutz.Lesen";
    public const string AtemschutzBearbeiten = "Atemschutz.Bearbeiten";
    public const string AusbildungLesen = "Ausbildung.Lesen";
    public const string AusbildungBearbeiten = "Ausbildung.Bearbeiten";
    public const string AusbildungKataloge = "Ausbildung.Kataloge";

    /// <summary>Empfänger der wöchentlichen Fristerinnerung (keine Seitenrechte).</summary>
    public const string ErinnerungPersonal = "Fristen.ErinnerungPersonal";
    public const string ErinnerungAtemschutz = "Fristen.ErinnerungAtemschutz";
    public const string AnwesenheitLesen = "Dienste.AnwesenheitLesen";
    public const string DiensteBearbeiten = "Dienste.Bearbeiten";
    public const string EinsaetzeLesen = "Einsaetze.Lesen";
    public const string EinsaetzeBearbeiten = "Einsaetze.Bearbeiten";
    public const string EinsaetzeFreigeben = "Einsaetze.Freigeben";
    public const string TechnikLesen = "Technik.Lesen";
    public const string TechnikPruefen = "Technik.Pruefen";
    public const string Verwaltung = "Verwaltung";
    public const string BenutzerVerwalten = "Benutzer.Verwalten";

    private static readonly Dictionary<string, string[]> Zuordnung = new()
    {
        // Jugendwart bekommt Mitgliederrechte erst mit der Einschränkung auf die Jugendfeuerwehr (Datenbereichs-Regel).
        [MitgliederLesen] = [Administrator, Kommandant, Stellvertreter, Fuehrungskraft, Schriftfuehrer, Atemschutzgeraetewart, Kassier],
        [MitgliederBearbeiten] = [Administrator, Kommandant, Stellvertreter],
        [AnwesenheitLesen] = [Kommandant, Stellvertreter, Fuehrungskraft, Schriftfuehrer],
        [AtemschutzLesen] = [Kommandant, Stellvertreter, Atemschutzgeraetewart],
        [AtemschutzBearbeiten] = [Kommandant, Stellvertreter, Atemschutzgeraetewart],
        [AusbildungLesen] = [Kommandant, Stellvertreter, Fuehrungskraft, Schriftfuehrer, Atemschutzgeraetewart],
        [AusbildungBearbeiten] = [Kommandant, Stellvertreter, Fuehrungskraft],
        [AusbildungKataloge] = [Administrator, Kommandant, Stellvertreter],
        [ErinnerungPersonal] = [Kommandant, Stellvertreter],
        [ErinnerungAtemschutz] = [Kommandant, Stellvertreter, Atemschutzgeraetewart],
        [DiensteBearbeiten] = [Kommandant, Stellvertreter, Fuehrungskraft, Schriftfuehrer],
        [EinsaetzeLesen] = [Kommandant, Stellvertreter, Fuehrungskraft, GemeindeLesen],
        [EinsaetzeBearbeiten] = [Kommandant, Stellvertreter, Fuehrungskraft],
        [EinsaetzeFreigeben] = [Kommandant, Stellvertreter],
        [TechnikLesen] = [Kommandant, Stellvertreter, Fuehrungskraft, Geraetewart, Atemschutzgeraetewart],
        [TechnikPruefen] = [Kommandant, Stellvertreter, Geraetewart, Atemschutzgeraetewart],
        [Verwaltung] = [Administrator],
        [BenutzerVerwalten] = [Administrator],
    };

    public static IReadOnlyDictionary<string, string[]> Alle => Zuordnung;

    public static void Registrieren(AuthorizationOptions optionen)
    {
        foreach (var (policy, rollen) in Zuordnung)
        {
            optionen.AddPolicy(policy, p => p.RequireRole(rollen));
        }
    }
}
