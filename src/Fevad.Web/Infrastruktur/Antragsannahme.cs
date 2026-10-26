using Fevad.Domain.Gemeinsam;
using Fevad.Domain.Laufbahn;
using Fevad.Domain.Mitglieder;
using Fevad.Infrastructure.Laufbahn;
using Fevad.Infrastructure.Mitglieder;
using Fevad.Infrastructure.Persistenz;
using Fevad.Web.Pages.Laufbahn;
using Fevad.Web.Pages.Mitglieder;
using Microsoft.EntityFrameworkCore;

namespace Fevad.Web.Infrastruktur;

/// <summary>
/// Annahme eines Antrags: Aufnahmen legen das Mitglied mit Mitgliedschaft, Erziehungsberechtigten und Vordienstzeit an,
/// der Übertritt wechselt aus der Jugendfeuerwehr in die Einsatzabteilung, die Volljährigkeitserklärung wird vermerkt. Der Wechsel in
/// die Altersabteilung beendet die Einsatzabteilung, das Ausscheiden die Mitgliedschaft in der Abteilung des Antrags (Satzung § 4, § 6).
/// Die Foto-Einwilligung geht je Foto-Kanal ins Einwilligungsregister. Gespeichert wird beim Aufrufer.
/// </summary>
public sealed class Antragsannahme(AppDbContext db, Laufbahnabfrage laufbahn, ParameterLeser parameter, Uebertritt uebertritt, Einwilligungsregister einwilligungen)
{
    /// <summary>Angaben bei der Annahme.</summary>
    /// <param name="Ab">Wirksam ab.</param>
    /// <param name="Personalnummer">Personalnummer bei Aufnahme.</param>
    /// <param name="Probezeit">Wahl der Probezeit.</param>
    /// <param name="ProbezeitBis">Ende einer verkürzten Probezeit.</param>
    /// <param name="ProbezeitBemerkung">Begründung zur Probezeit.</param>
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
            case AntragArt.JugendEintritt or AntragArt.KinderEintritt or AntragArt.EinsatzEintritt when antrag.MitgliedId is null:
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

            case AntragArt.Altersabteilung:
                mitglied = await MitgliedAsync(antrag, ct);
                var einsatz = await OffeneMitgliedschaftAsync(mitglied, antrag.AbteilungId, ct);
                meldung = (await uebertritt.AusfuehrenAsync(einsatz, (await AltersabteilungAsync(ct)).Id, angaben.Ab, ProbezeitWahl.Voll, null, null, ct))
                    .Meldung(angaben.Ab);
                break;

            case AntragArt.Ausscheiden:
                mitglied = await MitgliedAsync(antrag, ct);
                meldung = await AusscheidenAsync(antrag, mitglied, angaben.Ab, ct);
                break;

            default:
                throw new InvalidOperationException("Der Antrag gehört schon zu einem Mitglied; die Aufnahme bitte in der Laufbahn eintragen.");
        }

        // Beim Übertritt und bei der Volljährigkeit gibt es das Mitglied schon: geänderte Angaben aus dem Antrag übernehmen.
        if (Antragsregeln.MitMitglied(antrag.Art))
        {
            StammdatenUebernehmen(antrag, mitglied);
        }

        await einwilligungen.AusAntragAsync(antrag, mitglied, unterschrieben, ct);

        antrag.Status = AntragStatus.Angenommen;
        antrag.EntschiedenAm = heute;
        antrag.Mitglied = mitglied;
        return meldung;
    }

    /// <summary>Anschrift und Erreichbarkeit aus dem Antrag; leere Felder überschreiben nichts.</summary>
    private static void StammdatenUebernehmen(Antrag antrag, Mitglied mitglied)
    {
        mitglied.Vorname = antrag.Vorname;
        mitglied.Nachname = antrag.Nachname;
        mitglied.Geschlecht = antrag.Geschlecht ?? mitglied.Geschlecht;
        mitglied.Strasse = antrag.Strasse ?? mitglied.Strasse;
        mitglied.Plz = antrag.Plz ?? mitglied.Plz;
        mitglied.Ort = antrag.Ort ?? mitglied.Ort;
        mitglied.TelefonMobil = antrag.Mobil ?? mitglied.TelefonMobil;
        mitglied.TelefonFestnetz = antrag.Telefon ?? mitglied.TelefonFestnetz;
        mitglied.Email = antrag.Email ?? mitglied.Email;
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
        var ziel = await db.Abteilungen.SingleAsync(a => a.Id == antrag.AbteilungId, ct);
        var regeln = await laufbahn.RegelnAsync(angaben.Ab, ct);
        if (Mitgliedschaftswechsel.Pruefen(antrag.Geburtsdatum, ziel.Art, angaben.Ab, regeln) is { } fehler)
        {
            throw new InvalidOperationException(fehler);
        }

        Mitglied mitglied;
        var wieder = antrag.WiedereintrittVonId is not null;
        var bisherige = new HashSet<Guid>();
        if (antrag.WiedereintrittVonId is { } frueherId)
        {
            // Wiedereintritt: dasselbe Mitglied mit Personalnummer, Laufbahn und Lehrgängen, nur eine neue Mitgliedschaft.
            mitglied = await db.Mitglieder.Include(m => m.Mitgliedschaften).Include(m => m.Erziehungsberechtigte)
                .SingleOrDefaultAsync(m => m.Id == frueherId, ct)
                ?? throw new InvalidOperationException("Das frühere Mitglied gibt es nicht mehr; bitte den Antrag ohne Wiedereintritt neu erfassen.");
            if (Wiedereintritt.Hindernis(mitglied, angaben.Ab) is { } hindernis)
            {
                throw new InvalidOperationException(hindernis);
            }

            StammdatenUebernehmen(antrag, mitglied);
            bisherige.UnionWith(mitglied.Mitgliedschaften.Select(x => x.Id));
            if (Antragsregeln.IstJugendaufnahme(antrag.Art) && antrag.Erziehungsberechtigte.Any(e => e.Nummer is 1 or 2))
            {
                db.RemoveRange(mitglied.Erziehungsberechtigte);
                mitglied.Erziehungsberechtigte.Clear();
            }
        }
        else
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

            mitglied = new Mitglied
            {
                Personalnummer = personalnummer, Vorname = antrag.Vorname, Nachname = antrag.Nachname, Geburtsdatum = antrag.Geburtsdatum,
                Geschlecht = antrag.Geschlecht, Strasse = antrag.Strasse, Plz = antrag.Plz, Ort = antrag.Ort,
                TelefonMobil = antrag.Mobil, TelefonFestnetz = antrag.Telefon, Email = antrag.Email,
            };
        }

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

        // Erziehungsberechtigte gibt es nur bei Jugend- und Kinderfeuerwehr; in die Einsatzabteilung kommen nur Volljährige.
        foreach (var e in antrag.Erziehungsberechtigte.Where(e => e.Nummer is 1 or 2 && Antragsregeln.IstJugendaufnahme(antrag.Art)))
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

        if (wieder)
        {
            // Die Schlüssel entstehen schon im Konstruktor; ohne ausdrückliches Add hielte EF neue Einträge für geänderte.
            foreach (var neu in mitglied.Mitgliedschaften.Where(x => !bisherige.Contains(x.Id)))
            {
                db.Entry(neu).State = EntityState.Added;
            }

            foreach (var neu in mitglied.Erziehungsberechtigte)
            {
                db.Entry(neu).State = EntityState.Added;
            }
        }
        else
        {
            db.Mitglieder.Add(mitglied);
        }

        var text = $"{mitglied.Anzeigename} in die {ziel.Bezeichnung} zum {Anzeige.Datum(angaben.Ab)} {(wieder ? "wieder aufgenommen" : "aufgenommen")}";
        if (mitgliedschaft.Status == MitgliedStatus.Probezeit)
        {
            text += $", Probezeit bis {Anzeige.Datum(mitgliedschaft.ProbezeitBis)}";
        }

        return (mitglied, hinweise.Count == 0 ? text + "." : $"{text}. {string.Join("; ", hinweise)}.");
    }

    /// <summary>
    /// Beendet die Mitgliedschaft in der Abteilung des Antrags mit dem letzten Diensttag <paramref name="bis"/>. Aus der
    /// Einsatzabteilung geht es ohne gegenteilige Erklärung am Tag danach in der Altersabteilung weiter (Satzung § 6 Abs. 1).
    /// </summary>
    private async Task<string> AusscheidenAsync(Antrag antrag, Mitglied mitglied, DateOnly bis, CancellationToken ct)
    {
        var alt = await OffeneMitgliedschaftAsync(mitglied, antrag.AbteilungId, ct);
        await db.Entry(alt).Reference(m => m.Abteilung).LoadAsync(ct);
        var art = alt.Abteilung!.Art;
        if (antrag.Entlassungsgrund is not { } grund || !Antragsregeln.Gruende(art, alt.Status == MitgliedStatus.Probezeit).Contains(grund))
        {
            throw new InvalidOperationException("Der Grund passt nicht zur Abteilung; bitte im Antrag korrigieren.");
        }

        var gruppen = await db.GruppenMitgliedschaften.Where(g => g.MitgliedId == mitglied.Id && g.Gruppe!.AbteilungId == alt.AbteilungId).ToListAsync(ct);
        db.GruppenMitgliedschaften.RemoveRange(gruppen);

        if (antrag.InAltersabteilung && Antragsregeln.AltersabteilungMoeglich(art, grund))
        {
            var alters = await AltersabteilungAsync(ct);
            var neu = Mitgliedschaftswechsel.Uebertreten(alt, alters.Id, MitgliedStatus.Aktiv, bis.AddDays(1));
            await db.SaveChangesAsync(ct);
            db.Mitgliedschaften.Add(neu);
            return $"{mitglied.Anzeigename} scheidet zum {Anzeige.Datum(bis)} aus der {alt.Abteilung.Bezeichnung} aus und ist ab {Anzeige.Datum(bis.AddDays(1))} in der {alters.Bezeichnung}.";
        }

        Mitgliedschaftswechsel.Beenden(alt, bis, grund is Entlassungsgrund.Wohnung or Entlassungsgrund.Arbeitsstaette ? Austrittsgrund.Wohnortwechsel : Austrittsgrund.Austritt);
        return $"{mitglied.Anzeigename} scheidet zum {Anzeige.Datum(bis)} aus der {alt.Abteilung.Bezeichnung} aus. Ausgegebene Ausrüstung zurücknehmen.";
    }

    private async Task<Mitgliedschaft> OffeneMitgliedschaftAsync(Mitglied mitglied, Guid abteilungId, CancellationToken ct) =>
        await db.Mitgliedschaften.FirstOrDefaultAsync(s => s.MitgliedId == mitglied.Id && s.AbteilungId == abteilungId && !s.Vordienstzeit && s.GueltigBis == null, ct)
            ?? throw new InvalidOperationException($"{mitglied.Anzeigename} ist nicht mehr in dieser Abteilung.");

    private async Task<Abteilung> AltersabteilungAsync(CancellationToken ct) =>
        await db.Abteilungen.Where(a => a.Aktiv && a.Art == AbteilungArt.Alters).OrderBy(a => a.Sortierung).FirstOrDefaultAsync(ct)
            ?? throw new InvalidOperationException("Es gibt keine aktive Altersabteilung; bitte unter Stammdaten anlegen.");

    private async Task<Mitglied> MitgliedAsync(Antrag antrag, CancellationToken ct) =>
        await db.Mitglieder.SingleOrDefaultAsync(m => m.Id == antrag.MitgliedId, ct)
            ?? throw new InvalidOperationException("Das Mitglied zum Antrag gibt es nicht mehr.");

    private static Austrittsgrund? AlsAustrittsgrund(Ausscheidensgrund? grund) => grund switch
    {
        Ausscheidensgrund.Umzug => Austrittsgrund.Wohnortwechsel,
        Ausscheidensgrund.Wechsel => Austrittsgrund.Uebertritt,
        Ausscheidensgrund.Persoenlich => Austrittsgrund.PersoenlicheGruende,
        null => null,
        _ => Austrittsgrund.Austritt,
    };
}
