using Microsoft.AspNetCore.Authorization;
using static Fevad.Infrastructure.Identitaet.Rollen;

namespace Fevad.Infrastructure.Identitaet;

/// <summary>Zentrale Zuordnung Policy → Rollen. Neue Rechte nur hier eintragen.</summary>
public static class Berechtigungen
{
    public const string MitgliederLesen = "Mitglieder.Lesen";
    public const string MitgliederBearbeiten = "Mitglieder.Bearbeiten";

    /// <summary>Zugang zur Mitgliederliste; was jemand darin sieht, regelt der Datenbereich (Jugendwart: nur Jugendfeuerwehr).</summary>
    public const string MitgliederSehen = "Mitglieder.Sehen";
    public const string JugendLesen = "Jugend.Lesen";
    public const string JugendBearbeiten = "Jugend.Bearbeiten";
    public const string LaufbahnLesen = "Laufbahn.Lesen";
    public const string LaufbahnBearbeiten = "Laufbahn.Bearbeiten";
    public const string LaufbahnRegeln = "Laufbahn.Regeln";

    /// <summary>Empfänger der Erinnerung an Übertritte, Ehrungen und Beförderungen (keine Seitenrechte).</summary>
    public const string ErinnerungLaufbahn = "Fristen.ErinnerungLaufbahn";
    public const string ErinnerungJugend = "Fristen.ErinnerungJugend";
    public const string AtemschutzLesen = "Atemschutz.Lesen";
    public const string AtemschutzBearbeiten = "Atemschutz.Bearbeiten";
    public const string AusbildungLesen = "Ausbildung.Lesen";
    public const string AusbildungBearbeiten = "Ausbildung.Bearbeiten";
    public const string AusbildungKataloge = "Ausbildung.Kataloge";

    /// <summary>Empfänger der wöchentlichen Fristerinnerung (keine Seitenrechte).</summary>
    public const string ErinnerungPersonal = "Fristen.ErinnerungPersonal";
    public const string ErinnerungAtemschutz = "Fristen.ErinnerungAtemschutz";
    public const string AnwesenheitLesen = "Dienste.AnwesenheitLesen";

    /// <summary>Anwesenheitslisten aufrufen; der Jugendwart nur für Dienste der Jugendfeuerwehr (prüft Datenbereich).</summary>
    public const string AnwesenheitSehen = "Dienste.AnwesenheitSehen";
    public const string DiensteBearbeiten = "Dienste.Bearbeiten";
    public const string EinsaetzeLesen = "Einsaetze.Lesen";
    public const string EinsaetzeBearbeiten = "Einsaetze.Bearbeiten";
    public const string EinsaetzeFreigeben = "Einsaetze.Freigeben";
    public const string TechnikLesen = "Technik.Lesen";
    public const string TechnikPruefen = "Technik.Pruefen";
    public const string EntschaedigungLesen = "Entschaedigung.Lesen";
    public const string EntschaedigungAbrechnen = "Entschaedigung.Abrechnen";
    public const string EntschaedigungAbschliessen = "Entschaedigung.Abschliessen";
    public const string EntschaedigungSaetze = "Entschaedigung.Saetze";
    public const string StatistikLesen = "Statistik.Lesen";
    public const string Verwaltung = "Verwaltung";
    public const string BenutzerVerwalten = "Benutzer.Verwalten";

    private static readonly Dictionary<string, string[]> Zuordnung = new()
    {
        // MitgliederLesen und -Bearbeiten gelten für alle Mitglieder; der Jugendwart bekommt über Jugend.* nur die Jugendfeuerwehr.
        [MitgliederLesen] = [Administrator, Kommandant, Stellvertreter, Fuehrungskraft, Schriftfuehrer, Atemschutzgeraetewart, Kassier],
        [MitgliederBearbeiten] = [Administrator, Kommandant, Stellvertreter],
        [MitgliederSehen] = [Administrator, Kommandant, Stellvertreter, Fuehrungskraft, Schriftfuehrer, Atemschutzgeraetewart, Kassier, Jugendwart],
        [JugendLesen] = [Kommandant, Stellvertreter, Jugendwart],
        [JugendBearbeiten] = [Kommandant, Stellvertreter, Jugendwart],
        [LaufbahnLesen] = [Kommandant, Stellvertreter, Fuehrungskraft, Schriftfuehrer],
        [LaufbahnBearbeiten] = [Kommandant, Stellvertreter],
        [LaufbahnRegeln] = [Administrator, Kommandant, Stellvertreter],
        [ErinnerungLaufbahn] = [Kommandant, Stellvertreter],
        [ErinnerungJugend] = [Kommandant, Stellvertreter, Jugendwart],
        [AnwesenheitLesen] = [Kommandant, Stellvertreter, Fuehrungskraft, Schriftfuehrer],
        [AnwesenheitSehen] = [Kommandant, Stellvertreter, Fuehrungskraft, Schriftfuehrer, Jugendwart],
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
        // Entschädigung zahlt die Gemeinde: Kassier bereitet vor, Kommandant schließt ab, die Gemeinde liest und exportiert.
        [EntschaedigungLesen] = [Kommandant, Stellvertreter, Kassier, GemeindeLesen],
        [EntschaedigungAbrechnen] = [Kommandant, Stellvertreter, Kassier],
        [EntschaedigungAbschliessen] = [Kommandant, Stellvertreter],
        [EntschaedigungSaetze] = [Kommandant, Stellvertreter],
        [StatistikLesen] = [Kommandant, Stellvertreter, Fuehrungskraft, Schriftfuehrer, GemeindeLesen],
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
