using Microsoft.AspNetCore.Authorization;
using static FfAdelberg.Infrastructure.Identitaet.Rollen;

namespace FfAdelberg.Infrastructure.Identitaet;

/// <summary>Zentrale Zuordnung Policy → Rollen. Neue Rechte nur hier eintragen.</summary>
public static class Berechtigungen
{
    public const string MitgliederLesen = "Mitglieder.Lesen";
    public const string MitgliederBearbeiten = "Mitglieder.Bearbeiten";
    public const string AtemschutzLesen = "Atemschutz.Lesen";
    public const string AtemschutzBearbeiten = "Atemschutz.Bearbeiten";
    public const string DiensteBearbeiten = "Dienste.Bearbeiten";
    public const string EinsaetzeLesen = "Einsaetze.Lesen";
    public const string EinsaetzeBearbeiten = "Einsaetze.Bearbeiten";
    public const string EinsaetzeFreigeben = "Einsaetze.Freigeben";
    public const string TechnikLesen = "Technik.Lesen";
    public const string TechnikPruefen = "Technik.Pruefen";
    public const string Verwaltung = "Verwaltung";

    private static readonly Dictionary<string, string[]> Zuordnung = new()
    {
        [MitgliederLesen] = [Administrator, Kommandant, Stellvertreter, Fuehrungskraft, Jugendwart, Schriftfuehrer, Atemschutzgeraetewart, Kassier],
        [MitgliederBearbeiten] = [Administrator, Kommandant, Stellvertreter, Jugendwart],
        [AtemschutzLesen] = [Kommandant, Stellvertreter, Atemschutzgeraetewart],
        [AtemschutzBearbeiten] = [Kommandant, Stellvertreter, Atemschutzgeraetewart],
        [DiensteBearbeiten] = [Kommandant, Stellvertreter, Fuehrungskraft, Jugendwart, Schriftfuehrer],
        [EinsaetzeLesen] = [Kommandant, Stellvertreter, Fuehrungskraft, GemeindeLesen],
        [EinsaetzeBearbeiten] = [Kommandant, Stellvertreter, Fuehrungskraft],
        [EinsaetzeFreigeben] = [Kommandant, Stellvertreter],
        [TechnikLesen] = [Kommandant, Stellvertreter, Fuehrungskraft, Geraetewart, Atemschutzgeraetewart],
        [TechnikPruefen] = [Kommandant, Stellvertreter, Geraetewart, Atemschutzgeraetewart],
        [Verwaltung] = [Administrator],
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
