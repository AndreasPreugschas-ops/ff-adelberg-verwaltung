using Fevad.Domain.Gemeinsam;
using Fevad.Domain.Laufbahn;
using Fevad.Domain.Mitglieder;
using Fevad.Infrastructure.Laufbahn;
using Fevad.Infrastructure.Persistenz;
using Fevad.Web.Pages.Laufbahn;
using Fevad.Web.Pages.Mitglieder;
using Microsoft.EntityFrameworkCore;

namespace Fevad.Web.Infrastruktur;

/// <summary>
/// Annahme eines Antrags: Aufnahmen legen das Mitglied mit Mitgliedschaft, Erziehungsberechtigten und Vordienstzeit an,
/// der Übertritt wechselt aus der Jugendfeuerwehr in die Einsatzabteilung, die Volljährigkeitserklärung wird vermerkt.
/// Die Foto-Einwilligung geht mit Datum ins Mitglied. Gespeichert wird beim Aufrufer.
/// </summary>
public sealed class Antragsannahme(AppDbContext db, Laufbahnabfrage laufbahn, ParameterLeser parameter, Uebertritt uebertritt)
{
    public sealed record Angaben(DateOnly Ab, string? Personalnummer, ProbezeitWahl Probezeit, DateOnly? ProbezeitBis, string? ProbezeitBemerkung);

    /// <summary>Wirft <see cref="InvalidOperationException"/> mit einer Meldung für die Oberfläche.</summary>
    public async Task<string> AnnehmenAsync(Antrag antrag, Angaben angaben, DateOnly heute, CancellationToken ct)
    {
        if (antrag.Abgeschlossen)
        {
            throw new InvalidOperationException("Der Antrag ist schon abgeschlossen.");
        }

        // Die Einwilligung gilt ab der Unterschrift; das ist der Eingang des Antrags.
        var unterschrieben = antrag.EingegangenAm ?? heute;
        string meldung;
        Mitglied mitglied;
        switch (antrag.Art)
        {
            case AntragArt.JugendEintritt or AntragArt.EinsatzEintritt when antrag.MitgliedId is null:
                var volljaehrig = await parameter.ZahlAsync(ParameterSchluessel.VolljaehrigkeitAlter, heute, 18, ct);
                if (Antragsregeln.ZuJungFuerAufnahme(antrag.Art, antrag.Geburtsdatum, angaben.Ab, volljaehrig))
                {
                    throw new InvalidOperationException(
                        $"Ohne Übertritt aus der Jugendfeuerwehr geht die Aufnahme in die Einsatzabteilung erst ab {volljaehrig} Jahren.");
                }

                (mitglied, meldung) = await AufnehmenAsync(antrag, angaben, ct);
                break;

            case AntragArt.UebertrittEinsatz:
                mitglied = await MitgliedAsync(antrag, ct);
                var jugend = await db.Mitgliedschaften
                    .Where(s => s.MitgliedId == mitglied.Id && !s.Vordienstzeit && s.GueltigBis == null && s.Abteilung!.Art == AbteilungArt.Jugend)
                    .FirstOrDefaultAsync(ct)
                    ?? throw new InvalidOperationException($"{mitglied.Anzeigename} ist nicht mehr in der Jugendfeuerwehr. Den Übertritt bitte in der Laufbahn eintragen.");
                meldung = (await uebertritt.AusfuehrenAsync(jugend, antrag.AbteilungId, angaben.Ab, angaben.Probezeit, angaben.ProbezeitBis,
                    angaben.ProbezeitBemerkung, ct)).Meldung(angaben.Ab);
                break;

            case AntragArt.Volljaehrigkeit:
                mitglied = await MitgliedAsync(antrag, ct);
                var volljaehrigAb = await parameter.ZahlAsync(ParameterSchluessel.VolljaehrigkeitAlter, heute, 18, ct);
                if (mitglied.AlterAm(heute) < volljaehrigAb)
                {
                    throw new InvalidOperationException(
                        $"{mitglied.Anzeigename} ist erst am {Anzeige.Datum(Laufbahnhinweise.Geburtstag(mitglied.Geburtsdatum, volljaehrigAb))} volljährig.");
                }

                meldung = $"Erklärung bei Volljährigkeit für {mitglied.Anzeigename} vermerkt.";
                break;

            default:
                throw new InvalidOperationException("Der Antrag gehört schon zu einem Mitglied; die Aufnahme bitte in der Laufbahn eintragen.");
        }

        if (antrag.FotoEinwilligung is { } foto)
        {
            BearbeitenModel.FotoEinwilligungUebernehmen(mitglied, foto, unterschrieben, heute);
        }

        antrag.Status = AntragStatus.Angenommen;
        antrag.EntschiedenAm = heute;
        antrag.Mitglied = mitglied;
        return meldung;
    }

    /// <summary>Nächste freie Personalnummer, wenn alle bisherigen Zahlen sind; sonst leer.</summary>
    public async Task<string?> PersonalnummerVorschlagAsync(CancellationToken ct)
    {
        var nummern = await db.Mitglieder.IgnoreQueryFilters().Select(m => m.Personalnummer).ToListAsync(ct);
        var zahlen = nummern.Where(n => n.Length is > 0 and <= 9 && n.All(char.IsAsciiDigit)).ToList();
        return zahlen.Count == 0 ? null : (zahlen.Max(int.Parse) + 1).ToString().PadLeft(zahlen.Max(n => n.Length), '0');
    }

    private async Task<(Mitglied, string)> AufnehmenAsync(Antrag antrag, Angaben angaben, CancellationToken ct)
    {
        var personalnummer = angaben.Personalnummer?.Trim();
        if (string.IsNullOrEmpty(personalnummer))
        {
            throw new InvalidOperationException("Für die Aufnahme eine Personalnummer angeben.");
        }

        // Der eindeutige Index gilt auch für gelöschte Mitglieder.
        if (await db.Mitglieder.IgnoreQueryFilters().AnyAsync(m => m.Personalnummer == personalnummer, ct))
        {
            throw new InvalidOperationException($"Die Personalnummer {personalnummer} ist schon vergeben.");
        }

        var ziel = await db.Abteilungen.SingleAsync(a => a.Id == antrag.AbteilungId, ct);
        var regeln = await laufbahn.RegelnAsync(angaben.Ab, ct);
        if (Mitgliedschaftswechsel.Pruefen(antrag.Geburtsdatum, ziel.Art, angaben.Ab, regeln) is { } fehler)
        {
            throw new InvalidOperationException(fehler);
        }

        var mitglied = new Mitglied
        {
            Personalnummer = personalnummer, Vorname = antrag.Vorname, Nachname = antrag.Nachname, Geburtsdatum = antrag.Geburtsdatum,
            Geschlecht = antrag.Geschlecht, Strasse = antrag.Strasse, Plz = antrag.Plz, Ort = antrag.Ort,
            TelefonMobil = antrag.Mobil, TelefonFestnetz = antrag.Telefon, Email = antrag.Email,
        };

        // Probezeit nur in der Einsatzabteilung (Satzung § 3 Abs. 2); Verzicht und Verkürzung beschließt der Ausschuss.
        var probe = ziel.MitProbezeit && angaben.Probezeit != ProbezeitWahl.Verzicht;
        var mitgliedschaft = new Mitgliedschaft
        {
            AbteilungId = ziel.Id, Status = probe ? MitgliedStatus.Probezeit : MitgliedStatus.Aktiv, GueltigAb = angaben.Ab,
            ProbezeitBis = !probe ? null
                : angaben.Probezeit == ProbezeitWahl.Verkuerzt
                    ? angaben.ProbezeitBis ?? throw new InvalidOperationException("Für eine verkürzte Probezeit das Ende angeben.")
                    : angaben.Ab.AddMonths(regeln.ProbezeitMonate),
            ProbezeitBemerkung = probe && !string.IsNullOrWhiteSpace(angaben.ProbezeitBemerkung) ? angaben.ProbezeitBemerkung.Trim() : null,
            Bemerkung = ziel.MitProbezeit && !probe ? "Ohne Probezeit" : null,
        };
        if (mitgliedschaft.ProbezeitBis < angaben.Ab)
        {
            throw new InvalidOperationException("Das Ende der Probezeit liegt vor der Aufnahme.");
        }

        mitglied.Mitgliedschaften.Add(mitgliedschaft);

        // Erziehungsberechtigte gibt es nur bei der Jugendfeuerwehr; in die Einsatzabteilung kommen nur Volljährige.
        foreach (var e in antrag.Erziehungsberechtigte.Where(e => e.Nummer is 1 or 2 && antrag.Art == AntragArt.JugendEintritt))
        {
            mitglied.Erziehungsberechtigte.Add(new Erziehungsberechtigter
            {
                Nummer = e.Nummer, Verhaeltnis = e.Verhaeltnis, Vorname = e.Vorname, Nachname = e.Nachname, Strasse = e.Strasse, Plz = e.Plz,
                Ort = e.Ort, Telefon = e.Telefon, Mobil = e.Mobil, Email = e.Email,
            });
        }

        var hinweise = new List<string>();
        if (antrag.Art == AntragArt.EinsatzEintritt && antrag.FrueherFeuerwehr)
        {
            if (antrag.FrueherVon is { } von && antrag.FrueherBis is { } bis && von <= bis && bis < angaben.Ab)
            {
                mitglied.Mitgliedschaften.Add(new Mitgliedschaft
                {
                    AbteilungId = ziel.Id, Status = MitgliedStatus.Aktiv, GueltigAb = von, GueltigBis = bis, Vordienstzeit = true,
                    Vordienstart = Vordienstart.AndereFeuerwehr, Austrittsgrund = AlsAustrittsgrund(antrag.FrueherAustrittsgrund),
                    Bemerkung = antrag.FrueherFeuerwehrName,
                });
                hinweise.Add("Vordienstzeit übernommen, Anrechnung in der Laufbahn prüfen");
            }
            else
            {
                hinweise.Add("Vordienstzeit ohne vollständige Daten, bitte in der Laufbahn eintragen");
            }

            if (!string.IsNullOrWhiteSpace(antrag.FrueherLehrgaenge) || !string.IsNullOrWhiteSpace(antrag.FrueherDienstgrad))
            {
                hinweise.Add("Lehrgänge und Dienstgrad nach Nachweis in Ausbildung und Laufbahn eintragen");
            }
        }

        db.Mitglieder.Add(mitglied);
        var text = $"{mitglied.Anzeigename} in die {ziel.Bezeichnung} zum {Anzeige.Datum(angaben.Ab)} aufgenommen";
        if (mitgliedschaft.Status == MitgliedStatus.Probezeit)
        {
            text += $", Probezeit bis {Anzeige.Datum(mitgliedschaft.ProbezeitBis)}";
        }

        return (mitglied, hinweise.Count == 0 ? text + "." : $"{text}. {string.Join("; ", hinweise)}.");
    }

    private async Task<Mitglied> MitgliedAsync(Antrag antrag, CancellationToken ct) =>
        await db.Mitglieder.SingleOrDefaultAsync(m => m.Id == antrag.MitgliedId, ct)
            ?? throw new InvalidOperationException("Das Mitglied zum Antrag gibt es nicht mehr.");

    private static Austrittsgrund? AlsAustrittsgrund(Ausscheidensgrund? grund) => grund switch
    {
        Ausscheidensgrund.Umzug => Austrittsgrund.Wohnortwechsel,
        Ausscheidensgrund.Wechsel => Austrittsgrund.Uebertritt,
        null => null,
        _ => Austrittsgrund.Austritt,
    };
}
