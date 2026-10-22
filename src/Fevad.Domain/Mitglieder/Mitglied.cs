using Fevad.Domain.Gemeinsam;

namespace Fevad.Domain.Mitglieder;

public class Mitglied : Entitaet
{
    public required string Personalnummer { get; set; }

    /// <summary>Interne ID aus MP-FEUER für den Migrationsabgleich.</summary>
    public string? LegacyMpFeuerId { get; set; }
    public string? Barcode { get; set; }

    public string? Anrede { get; set; }
    public string? Titel { get; set; }
    public required string Vorname { get; set; }
    public required string Nachname { get; set; }
    public DateOnly Geburtsdatum { get; set; }

    public string? Strasse { get; set; }
    public string? Plz { get; set; }
    public string? Ort { get; set; }

    /// <summary>Im Format E.164 (+49...). Für alamos FE2 wird daraus 0049... erzeugt.</summary>
    public string? TelefonMobil { get; set; }
    public string? TelefonFestnetz { get; set; }
    public string? Email { get; set; }

    /// <summary>Für die Tagesverfügbarkeit; nur für Führungskräfte sichtbar.</summary>
    public string? Arbeitgeber { get; set; }
    public string? Arbeitsort { get; set; }
    public string? Notizen { get; set; }

    /// <summary>Stabile externalDbId für den alamos-Adressbuch-Sync.</summary>
    public Guid Fe2ExternalId { get; set; } = Guid.CreateVersion7();
    public bool Fe2SyncAktiv { get; set; }

    public List<Mitgliedschaft> Mitgliedschaften { get; set; } = [];
    public List<DienstgradZuordnung> Dienstgrade { get; set; } = [];
    public List<FunktionZuordnung> Funktionen { get; set; } = [];

    public string Anzeigename => $"{Nachname}, {Vorname}";

    public int AlterAm(DateOnly stichtag) => Alter(Geburtsdatum, stichtag);

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

    public Mitgliedschaft? AktuelleMitgliedschaft(DateOnly stichtag) =>
        Mitgliedschaften
            .Where(m => m.IstAktivAm(stichtag))
            .OrderByDescending(m => m.GueltigAb)
            .FirstOrDefault();

    public DienstgradZuordnung? AktuellerDienstgrad(DateOnly stichtag) =>
        Dienstgrade
            .Where(d => d.Ab <= stichtag)
            .OrderByDescending(d => d.Ab)
            .FirstOrDefault();
}
