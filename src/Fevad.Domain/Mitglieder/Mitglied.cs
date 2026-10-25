using System.ComponentModel.DataAnnotations;
using Fevad.Domain.Gemeinsam;

namespace Fevad.Domain.Mitglieder;

/// <summary>Für die Jahresstatistik und die Dienstgradbezeichnung (Feuerwehrmann/-frau).</summary>
public enum Geschlecht
{
    /// <summary>Männlich.</summary>
    [Display(Name = "männlich")] Maennlich,
    /// <summary>Weiblich.</summary>
    [Display(Name = "weiblich")] Weiblich,
    /// <summary>Divers.</summary>
    [Display(Name = "divers")] Divers,
}

/// <summary>
/// Angehöriger der Feuerwehr mit Stammdaten, Kontaktdaten und Arbeitgeber. Zeiten in Abteilungen, Dienstgrade, Funktionen,
/// Erziehungsberechtigte und frühere Anschriften hängen als Listen daran.
/// </summary>
public class Mitglied : Entitaet
{
    /// <summary>Personalnummer der Feuerwehr; eindeutig.</summary>
    public required string Personalnummer { get; set; }

    /// <summary>Interne ID aus MP-FEUER für den Migrationsabgleich.</summary>
    public string? LegacyMpFeuerId { get; set; }
    /// <summary>Barcode des Dienstausweises, z. B. für die Anwesenheitserfassung.</summary>
    public string? Barcode { get; set; }

    /// <summary>Anrede für Briefe und Formulare.</summary>
    public string? Anrede { get; set; }
    /// <summary>Akademischer Titel, z. B. „Dr.“.</summary>
    public string? Titel { get; set; }
    /// <summary>Vorname.</summary>
    public required string Vorname { get; set; }
    /// <summary>Nachname.</summary>
    public required string Nachname { get; set; }
    /// <summary>Geburtsdatum; bestimmt Altersgrenzen, Jugendfeuerwehr und Volljährigkeit.</summary>
    public DateOnly Geburtsdatum { get; set; }
    /// <summary>Geschlecht; darf offen bleiben.</summary>
    public Geschlecht? Geschlecht { get; set; }

    /// <summary>Straße und Hausnummer der Wohnanschrift.</summary>
    public string? Strasse { get; set; }
    /// <summary>Postleitzahl der Wohnanschrift.</summary>
    public string? Plz { get; set; }
    /// <summary>Ort der Wohnanschrift.</summary>
    public string? Ort { get; set; }

    /// <summary>Im Format E.164 (+49...). Für alamos FE2 wird daraus 0049... erzeugt.</summary>
    public string? TelefonMobil { get; set; }
    /// <summary>Festnetznummer.</summary>
    public string? TelefonFestnetz { get; set; }
    /// <summary>Private E-Mail-Adresse.</summary>
    public string? Email { get; set; }

    /// <summary>Ausgeübter Beruf, für die Tagesverfügbarkeit und Fachkenntnisse.</summary>
    public string? Beruf { get; set; }

    /// <summary>Für die Tagesverfügbarkeit; nur mit eigenem Recht sichtbar (Mitglieder.ArbeitgeberLesen).</summary>
    public string? Arbeitgeber { get; set; }
    /// <summary>Straße und Hausnummer des Arbeitgebers; nur mit dem Recht Mitglieder.ArbeitgeberLesen sichtbar.</summary>
    public string? ArbeitgeberStrasse { get; set; }
    /// <summary>Postleitzahl des Arbeitgebers.</summary>
    public string? ArbeitgeberPlz { get; set; }

    /// <summary>Ort des Arbeitgebers bzw. der Arbeitsstelle.</summary>
    public string? Arbeitsort { get; set; }
    /// <summary>Telefonnummer beim Arbeitgeber.</summary>
    public string? ArbeitgeberTelefon { get; set; }
    /// <summary>Diensthandy beim Arbeitgeber.</summary>
    public string? ArbeitgeberMobil { get; set; }
    /// <summary>Dienstliche E-Mail-Adresse.</summary>
    public string? ArbeitgeberEmail { get; set; }
    /// <summary>Interne Notizen der Personalverwaltung.</summary>
    public string? Notizen { get; set; }

    /// <summary>
    /// Einwilligung in Fotos für die Öffentlichkeitsarbeit (Art. 7 DSGVO), mit Datum zum Nachweis. Ein Widerruf setzt
    /// <see cref="FotoWiderrufAm"/>; eine neue Einwilligung ein neues Datum und hebt den Widerruf auf.
    /// </summary>
    public DateOnly? FotoEinwilligungAm { get; set; }
    /// <summary>Tag des Widerrufs der Foto-Einwilligung; leer, solange nicht widerrufen.</summary>
    public DateOnly? FotoWiderrufAm { get; set; }

    /// <summary>Die Foto-Einwilligung liegt vor und ist nicht widerrufen.</summary>
    public bool FotoEinwilligung => FotoEinwilligungAm is not null && FotoWiderrufAm is null;

    /// <summary>
    /// Vom Löschlauf nach L7 anonymisiert: Name und Personalnummer ersetzt, Geburtsdatum auf den 1. Januar des Jahrzehnts gerundet, Laufbahn
    /// gelöscht. Übrig bleiben Mitgliedschaftszeiträume, Einsatz- und Abrechnungsdaten nur noch für Statistik und Kasse.
    /// </summary>
    public DateTimeOffset? AnonymisiertAm { get; set; }

    /// <summary>
    /// Kennzeichen „aktiver Atemschutzgeräteträger“: nur wer es hat, wird mit Vorsorge, Übungen und Einsatzstatus überwacht.
    /// Haken weg heißt nicht mehr überwacht (Andi, 25.10.2026).
    /// </summary>
    public bool AtemschutzAktiv { get; set; }

    /// <summary>Stabile externalDbId für den alamos-Adressbuch-Sync.</summary>
    public Guid Fe2ExternalId { get; set; } = Guid.CreateVersion7();
    /// <summary>Das Mitglied wird ins alamos-FE2-Adressbuch übertragen.</summary>
    public bool Fe2SyncAktiv { get; set; }

    /// <summary>Zeiten in Abteilungen, auch beendete und Vordienstzeiten.</summary>
    public List<Mitgliedschaft> Mitgliedschaften { get; set; } = [];
    /// <summary>Verliehene Dienstgrade mit Datum; der jüngste bis zum Stichtag gilt.</summary>
    public List<DienstgradZuordnung> Dienstgrade { get; set; } = [];
    /// <summary>Wahlämter und Bestellungen, auch beendete.</summary>
    public List<FunktionZuordnung> Funktionen { get; set; } = [];
    /// <summary>Erziehungsberechtigte bei Minderjährigen, höchstens zwei.</summary>
    public List<Erziehungsberechtigter> Erziehungsberechtigte { get; set; } = [];
    /// <summary>Verlauf abgelöster privater und beruflicher Anschriften.</summary>
    public List<FruehereAnschrift> FruehereAnschriften { get; set; } = [];

    /// <summary>„Nachname, Vorname“ für Listen.</summary>
    public string Anzeigename => $"{Nachname}, {Vorname}";

    /// <summary>Alter des Mitglieds an einem Tag.</summary>
    /// <param name="stichtag">Tag, an dem das Alter bestimmt wird.</param>
    /// <returns>Vollendete Lebensjahre am Stichtag.</returns>
    public int AlterAm(DateOnly stichtag) => Alter(Geburtsdatum, stichtag);

    /// <summary>Vollendete Lebensjahre an einem Tag; am 29. Februar Geborene vollenden das Lebensjahr in Nicht-Schaltjahren am 1. März.</summary>
    /// <param name="geburtsdatum">Geburtsdatum.</param>
    /// <param name="stichtag">Tag, an dem das Alter bestimmt wird.</param>
    /// <returns>Vollendete Lebensjahre.</returns>
    public static int Alter(DateOnly geburtsdatum, DateOnly stichtag)
    {
        var alter = stichtag.Year - geburtsdatum.Year;

        // Am 29.02. Geborene vollenden ihr Lebensjahr in Nicht-Schaltjahren erst am 01.03.
        var geburtstag = geburtsdatum is { Month: 2, Day: 29 } && !DateTime.IsLeapYear(stichtag.Year)
            ? new DateOnly(stichtag.Year, 3, 1)
            : geburtsdatum.AddYears(alter);
        if (stichtag < geburtstag)
        {
            alter--;
        }

        return alter;
    }

    /// <summary>Die am Stichtag bestehende Mitgliedschaft; bei Überschneidung die zuletzt begonnene. Erwartet geladene Mitgliedschaften.</summary>
    /// <param name="stichtag">Tag, für den die Mitgliedschaft gesucht wird.</param>
    /// <returns>Die Mitgliedschaft oder null, wenn das Mitglied an dem Tag keiner Abteilung angehört.</returns>
    public Mitgliedschaft? AktuelleMitgliedschaft(DateOnly stichtag) =>
        Mitgliedschaften
            .Where(m => m.IstAktivAm(stichtag))
            .OrderByDescending(m => m.GueltigAb)
            .FirstOrDefault();

    /// <summary>Der am Stichtag geltende Dienstgrad, also die jüngste Zuordnung bis dahin. Erwartet geladene Dienstgrade.</summary>
    /// <param name="stichtag">Tag, für den der Dienstgrad gesucht wird.</param>
    /// <returns>Die Zuordnung oder null, wenn bis dahin kein Dienstgrad verliehen war.</returns>
    public DienstgradZuordnung? AktuellerDienstgrad(DateOnly stichtag) =>
        Dienstgrade
            .Where(d => d.Ab <= stichtag)
            .OrderByDescending(d => d.Ab)
            .FirstOrDefault();
}
