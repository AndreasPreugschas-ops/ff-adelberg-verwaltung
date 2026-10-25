using System.ComponentModel.DataAnnotations;
using Fevad.Domain.Gemeinsam;

namespace Fevad.Domain.Mitglieder;

/// <summary>Feld der Stammdaten, dessen Änderung ein Mitglied beantragen kann (V36).</summary>
public enum AenderungFeld
{
    /// <summary>Straße und Hausnummer der Wohnanschrift.</summary>
    [Display(Name = "Straße")] Strasse,
    /// <summary>Postleitzahl der Wohnanschrift.</summary>
    [Display(Name = "PLZ")] Plz,
    /// <summary>Wohnort.</summary>
    [Display(Name = "Ort")] Ort,
    /// <summary>Private Mobilnummer.</summary>
    [Display(Name = "Mobil")] TelefonMobil,
    /// <summary>Private Festnetznummer.</summary>
    [Display(Name = "Telefon privat")] TelefonFestnetz,
    /// <summary>Private E-Mail-Adresse.</summary>
    [Display(Name = "E-Mail-Adresse")] Email,
    /// <summary>Name einer Person, die im Notfall verständigt wird.</summary>
    [Display(Name = "Notfallkontakt")] NotfallkontaktName,
    /// <summary>Telefonnummer des Notfallkontakts.</summary>
    [Display(Name = "Telefon Notfallkontakt")] NotfallkontaktTelefon,
    /// <summary>Beruf.</summary>
    [Display(Name = "Beruf")] Beruf,
    /// <summary>Name des Arbeitgebers.</summary>
    [Display(Name = "Arbeitgeber")] Arbeitgeber,
    /// <summary>Straße des Arbeitgebers.</summary>
    [Display(Name = "Straße Arbeitgeber")] ArbeitgeberStrasse,
    /// <summary>Postleitzahl des Arbeitgebers.</summary>
    [Display(Name = "PLZ Arbeitgeber")] ArbeitgeberPlz,
    /// <summary>Arbeitsort.</summary>
    [Display(Name = "Ort Arbeitgeber")] Arbeitsort,
    /// <summary>Dienstliche Festnetznummer.</summary>
    [Display(Name = "Telefon dienstlich")] ArbeitgeberTelefon,
    /// <summary>Dienstliche Mobilnummer.</summary>
    [Display(Name = "Mobil dienstlich")] ArbeitgeberMobil,
    /// <summary>Dienstliche E-Mail-Adresse.</summary>
    [Display(Name = "E-Mail dienstlich")] ArbeitgeberEmail,
}

/// <summary>Bearbeitungsstand eines Änderungsantrags.</summary>
public enum AenderungsantragStatus
{
    /// <summary>Gestellt, wartet auf die Personalverwaltung.</summary>
    [Display(Name = "offen")] Offen,
    /// <summary>In die Stammdaten übernommen.</summary>
    [Display(Name = "übernommen")] Uebernommen,
    /// <summary>Von der Personalverwaltung abgelehnt.</summary>
    [Display(Name = "abgelehnt")] Abgelehnt,
    /// <summary>Vom Mitglied zurückgezogen.</summary>
    [Display(Name = "zurückgezogen")] Zurueckgezogen,
}

/// <summary>
/// Antrag eines Mitglieds, die eigenen Kontakt-, Notfall- oder Arbeitgeberdaten zu berichtigen (V36, Art. 16 DSGVO). Das Mitglied
/// ändert nichts selbst; eine Person mit Mitglieder.Bearbeiten übernimmt oder lehnt ab (Andi, 25.10.2026). Abgelöste Anschriften
/// gehen bei der Übernahme in den Anschriftenverlauf (L34). Entschiedene Anträge löscht der Löschlauf nach L32.
/// </summary>
public class Aenderungsantrag : Entitaet
{
    /// <summary>Schlüssel des Mitglieds, dessen Daten geändert werden sollen.</summary>
    public Guid MitgliedId { get; set; }
    /// <summary>Das Mitglied.</summary>
    public Mitglied? Mitglied { get; set; }

    /// <summary>Bearbeitungsstand.</summary>
    public AenderungsantragStatus Status { get; set; }

    /// <summary>Freiwillige Bemerkung des Mitglieds, z. B. „gilt ab 1. Dezember“.</summary>
    public string? Bemerkung { get; set; }

    /// <summary>Zeitpunkt der Entscheidung oder Rücknahme in UTC.</summary>
    public DateTimeOffset? EntschiedenAm { get; set; }
    /// <summary>Wer entschieden hat (Benutzername).</summary>
    public string? EntschiedenVon { get; set; }
    /// <summary>Begründung einer Ablehnung; das Mitglied sieht sie.</summary>
    public string? Ablehnungsgrund { get; set; }

    /// <summary>Die beantragten Änderungen, je Feld alt und neu.</summary>
    public List<Aenderungsfeld> Felder { get; set; } = [];

    /// <summary>Ist noch offen.</summary>
    public bool Offen => Status == AenderungsantragStatus.Offen;

    /// <summary>Betrifft Beruf oder Arbeitgeber; dann braucht die Übernahme auch das Recht Mitglieder.Arbeitgeber.</summary>
    public bool BetrifftArbeitgeber => Felder.Any(f => Aenderungsfelder.Beruflich(f.Feld));

    /// <summary>
    /// Baut einen Antrag aus den gewünschten Werten. Aufgenommen werden nur Felder, deren Wert sich gegenüber den gespeicherten
    /// Stammdaten wirklich ändert; Leerzeichen am Rand zählen nicht, ein leerer Wert heißt „löschen“.
    /// </summary>
    /// <param name="mitglied">Das Mitglied mit den gespeicherten Daten.</param>
    /// <param name="gewuenscht">Gewünschte Werte je Feld; fehlende Felder bleiben unverändert.</param>
    /// <param name="bemerkung">Freiwillige Bemerkung.</param>
    /// <returns>Der Antrag oder null, wenn sich nichts ändert.</returns>
    public static Aenderungsantrag? Erstellen(Mitglied mitglied, IReadOnlyDictionary<AenderungFeld, string?> gewuenscht, string? bemerkung)
    {
        var felder = gewuenscht
            .Select(g => (g.Key, Alt: Aenderungsfelder.Lesen(mitglied, g.Key), Neu: Aenderungsfelder.Bereinigen(g.Value)))
            .Where(f => !Aenderungsfelder.Gleich(f.Alt, f.Neu))
            .OrderBy(f => f.Key)
            .Select(f => new Aenderungsfeld { Feld = f.Key, Alt = f.Alt, Neu = f.Neu })
            .ToList();
        return felder.Count == 0
            ? null
            : new Aenderungsantrag { MitgliedId = mitglied.Id, Status = AenderungsantragStatus.Offen, Bemerkung = Aenderungsfelder.Bereinigen(bemerkung), Felder = felder };
    }

    /// <summary>
    /// Übernimmt die Änderungen in die Stammdaten und schließt den Antrag. Liefert die abgelösten Anschriften für den Verlauf (L34).
    /// </summary>
    /// <param name="mitglied">Das Mitglied, in das übernommen wird.</param>
    /// <param name="heute">Tag der Übernahme; Ende der abgelösten Anschriften.</param>
    /// <param name="jetzt">Zeitpunkt der Entscheidung in UTC.</param>
    /// <param name="von">Benutzername der entscheidenden Person.</param>
    /// <returns>Abgelöste private und berufliche Anschrift, soweit sie sich ändern.</returns>
    /// <exception cref="InvalidOperationException">Der Antrag ist nicht mehr offen oder gehört zu einem anderen Mitglied.</exception>
    public IReadOnlyList<FruehereAnschrift> Uebernehmen(Mitglied mitglied, DateOnly heute, DateTimeOffset jetzt, string? von)
    {
        if (!Offen || mitglied.Id != MitgliedId)
        {
            throw new InvalidOperationException("Der Änderungsantrag ist nicht offen oder gehört zu einem anderen Mitglied.");
        }

        string? Wert(AenderungFeld feld) => Felder.FirstOrDefault(f => f.Feld == feld) is { } f ? f.Neu : Aenderungsfelder.Lesen(mitglied, feld);

        var verlauf = new[]
        {
            FruehereAnschrift.Privat(mitglied, Wert(AenderungFeld.Strasse), Wert(AenderungFeld.Plz), Wert(AenderungFeld.Ort), heute),
            FruehereAnschrift.Beruflich(mitglied, Wert(AenderungFeld.Arbeitgeber), Wert(AenderungFeld.ArbeitgeberStrasse),
                Wert(AenderungFeld.ArbeitgeberPlz), Wert(AenderungFeld.Arbeitsort), heute),
        }.OfType<FruehereAnschrift>().ToList();

        foreach (var f in Felder)
        {
            Aenderungsfelder.Schreiben(mitglied, f.Feld, f.Neu);
        }

        Abschliessen(AenderungsantragStatus.Uebernommen, jetzt, von);
        return verlauf;
    }

    /// <summary>Lehnt den Antrag mit Begründung ab.</summary>
    /// <param name="grund">Begründung für das Mitglied.</param>
    /// <param name="jetzt">Zeitpunkt in UTC.</param>
    /// <param name="von">Benutzername der entscheidenden Person.</param>
    /// <exception cref="InvalidOperationException">Der Antrag ist nicht mehr offen.</exception>
    public void Ablehnen(string grund, DateTimeOffset jetzt, string? von)
    {
        if (!Offen)
        {
            throw new InvalidOperationException("Der Änderungsantrag ist nicht mehr offen.");
        }

        Ablehnungsgrund = grund.Trim();
        Abschliessen(AenderungsantragStatus.Abgelehnt, jetzt, von);
    }

    /// <summary>Das Mitglied zieht seinen offenen Antrag zurück.</summary>
    /// <param name="jetzt">Zeitpunkt in UTC.</param>
    /// <exception cref="InvalidOperationException">Der Antrag ist nicht mehr offen.</exception>
    public void Zurueckziehen(DateTimeOffset jetzt)
    {
        if (!Offen)
        {
            throw new InvalidOperationException("Der Änderungsantrag ist nicht mehr offen.");
        }

        Abschliessen(AenderungsantragStatus.Zurueckgezogen, jetzt, null);
    }

    private void Abschliessen(AenderungsantragStatus status, DateTimeOffset jetzt, string? von)
    {
        Status = status;
        EntschiedenAm = jetzt;
        EntschiedenVon = von;
    }
}

/// <summary>Eine beantragte Änderung: Feld mit bisherigem und gewünschtem Wert.</summary>
public class Aenderungsfeld : Entitaet
{
    /// <summary>Schlüssel des Antrags.</summary>
    public Guid AenderungsantragId { get; set; }
    /// <summary>Der Antrag.</summary>
    public Aenderungsantrag? Aenderungsantrag { get; set; }

    /// <summary>Welches Feld.</summary>
    public AenderungFeld Feld { get; set; }

    /// <summary>Wert beim Stellen des Antrags. Nicht im Protokoll, weil Kontaktdaten sonst dort doppelt lägen.</summary>
    [NichtProtokollieren]
    public string? Alt { get; set; }

    /// <summary>Gewünschter Wert; leer heißt löschen. Nicht im Protokoll.</summary>
    [NichtProtokollieren]
    public string? Neu { get; set; }
}

/// <summary>Lesen und Schreiben der beantragbaren Felder am Mitglied.</summary>
public static class Aenderungsfelder
{
    /// <summary>Höchstlänge je Feld, wie in der Datenbank.</summary>
    /// <param name="feld">Das Feld.</param>
    /// <returns>Die zulässige Zeichenzahl.</returns>
    public static int Laenge(AenderungFeld feld) => feld switch
    {
        AenderungFeld.Plz or AenderungFeld.ArbeitgeberPlz => 10,
        AenderungFeld.TelefonMobil or AenderungFeld.TelefonFestnetz or AenderungFeld.NotfallkontaktTelefon
            or AenderungFeld.ArbeitgeberTelefon or AenderungFeld.ArbeitgeberMobil => 20,
        AenderungFeld.Email or AenderungFeld.ArbeitgeberEmail or AenderungFeld.Strasse or AenderungFeld.ArbeitgeberStrasse or AenderungFeld.Arbeitgeber => 200,
        _ => 100,
    };

    /// <summary>Gehört zu Beruf und Arbeitgeber (Recht Mitglieder.Arbeitgeber).</summary>
    /// <param name="feld">Das Feld.</param>
    /// <returns><c>true</c> für Beruf und Arbeitgeberfelder.</returns>
    public static bool Beruflich(AenderungFeld feld) => feld >= AenderungFeld.Beruf;

    /// <summary>Gespeicherter Wert eines Feldes.</summary>
    /// <param name="m">Das Mitglied.</param>
    /// <param name="feld">Das Feld.</param>
    /// <returns>Der Wert oder null.</returns>
    public static string? Lesen(Mitglied m, AenderungFeld feld) => feld switch
    {
        AenderungFeld.Strasse => m.Strasse,
        AenderungFeld.Plz => m.Plz,
        AenderungFeld.Ort => m.Ort,
        AenderungFeld.TelefonMobil => m.TelefonMobil,
        AenderungFeld.TelefonFestnetz => m.TelefonFestnetz,
        AenderungFeld.Email => m.Email,
        AenderungFeld.NotfallkontaktName => m.NotfallkontaktName,
        AenderungFeld.NotfallkontaktTelefon => m.NotfallkontaktTelefon,
        AenderungFeld.Beruf => m.Beruf,
        AenderungFeld.Arbeitgeber => m.Arbeitgeber,
        AenderungFeld.ArbeitgeberStrasse => m.ArbeitgeberStrasse,
        AenderungFeld.ArbeitgeberPlz => m.ArbeitgeberPlz,
        AenderungFeld.Arbeitsort => m.Arbeitsort,
        AenderungFeld.ArbeitgeberTelefon => m.ArbeitgeberTelefon,
        AenderungFeld.ArbeitgeberMobil => m.ArbeitgeberMobil,
        AenderungFeld.ArbeitgeberEmail => m.ArbeitgeberEmail,
        _ => throw new ArgumentOutOfRangeException(nameof(feld)),
    };

    /// <summary>Setzt den Wert eines Feldes.</summary>
    /// <param name="m">Das Mitglied.</param>
    /// <param name="feld">Das Feld.</param>
    /// <param name="wert">Neuer Wert; null löscht.</param>
    public static void Schreiben(Mitglied m, AenderungFeld feld, string? wert)
    {
        switch (feld)
        {
            case AenderungFeld.Strasse: m.Strasse = wert; break;
            case AenderungFeld.Plz: m.Plz = wert; break;
            case AenderungFeld.Ort: m.Ort = wert; break;
            case AenderungFeld.TelefonMobil: m.TelefonMobil = wert; break;
            case AenderungFeld.TelefonFestnetz: m.TelefonFestnetz = wert; break;
            case AenderungFeld.Email: m.Email = wert; break;
            case AenderungFeld.NotfallkontaktName: m.NotfallkontaktName = wert; break;
            case AenderungFeld.NotfallkontaktTelefon: m.NotfallkontaktTelefon = wert; break;
            case AenderungFeld.Beruf: m.Beruf = wert; break;
            case AenderungFeld.Arbeitgeber: m.Arbeitgeber = wert; break;
            case AenderungFeld.ArbeitgeberStrasse: m.ArbeitgeberStrasse = wert; break;
            case AenderungFeld.ArbeitgeberPlz: m.ArbeitgeberPlz = wert; break;
            case AenderungFeld.Arbeitsort: m.Arbeitsort = wert; break;
            case AenderungFeld.ArbeitgeberTelefon: m.ArbeitgeberTelefon = wert; break;
            case AenderungFeld.ArbeitgeberMobil: m.ArbeitgeberMobil = wert; break;
            case AenderungFeld.ArbeitgeberEmail: m.ArbeitgeberEmail = wert; break;
            default: throw new ArgumentOutOfRangeException(nameof(feld));
        }
    }

    /// <summary>Leerzeichen am Rand und doppelte Leerzeichen entfernen; leer wird null.</summary>
    /// <param name="wert">Eingabe.</param>
    /// <returns>Der bereinigte Wert oder null.</returns>
    public static string? Bereinigen(string? wert) =>
        string.IsNullOrWhiteSpace(wert) ? null : string.Join(' ', wert.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));

    /// <summary>Zwei Werte sind gleich, wenn sie sich nur in Leerzeichen am Rand unterscheiden; leer und null sind gleich.</summary>
    /// <param name="a">Erster Wert.</param>
    /// <param name="b">Zweiter Wert.</param>
    /// <returns><c>true</c> bei Gleichheit.</returns>
    public static bool Gleich(string? a, string? b) => string.Equals(Bereinigen(a), Bereinigen(b), StringComparison.Ordinal);
}
