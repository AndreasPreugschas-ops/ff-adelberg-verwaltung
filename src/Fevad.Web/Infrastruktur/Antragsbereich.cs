using Fevad.Domain.Gemeinsam;
using Fevad.Domain.Mitglieder;
using Fevad.Infrastructure.Antraege;
using Fevad.Infrastructure.Identitaet;
using Fevad.Infrastructure.Laufbahn;
using Fevad.Infrastructure.Persistenz;
using Microsoft.EntityFrameworkCore;

namespace Fevad.Web.Infrastruktur;

/// <summary>
/// Welche Anträge jemand sieht und was das Formular braucht. Sichtbar ist ein Antrag, wenn Antraege.Bearbeiten seine
/// Zielabteilung umfasst oder das Mitglied dazu derzeit in einer freigegebenen Abteilung ist (der Jugendwart sieht so den
/// Übertritt seiner Jugendlichen). Entscheiden darf nur, wessen Freigabe die Zielabteilung umfasst.
/// </summary>
public sealed class Antragsbereich(AppDbContext db, Datenbereich datenbereich, Laufbahnabfrage laufbahn, ParameterLeser parameter)
{
    public Task<Freigabe> FreigabeAsync(System.Security.Claims.ClaimsPrincipal benutzer) =>
        datenbereich.FreigabeAsync(benutzer, Berechtigungen.AntraegeBearbeiten);

    public static IQueryable<Antrag> Sichtbar(IQueryable<Antrag> abfrage, Freigabe freigabe, DateOnly stichtag)
    {
        if (freigabe.Alle)
        {
            return abfrage;
        }

        var ids = freigabe.Abteilungen.ToList();
        return abfrage.Where(a => ids.Contains(a.AbteilungId)
            || (a.MitgliedId != null && a.Mitglied!.Mitgliedschaften.Any(s => !s.Vordienstzeit && ids.Contains(s.AbteilungId)
                && s.GueltigAb <= stichtag && (s.GueltigBis == null || s.GueltigBis >= stichtag))));
    }

    /// <summary>Briefkopf, Regeln und Datenschutztexte; <paramref name="abteilungId"/> ist die Zielabteilung, falls bekannt.</summary>
    public async Task<AntragPdf.Rahmen> RahmenAsync(Guid? abteilungId, DateOnly heute, CancellationToken ct)
    {
        async Task<string> Text(string schluessel) => await parameter.WertAsync(schluessel, heute, ct) ?? "[fehlt]";
        async Task<int> Zahl(string schluessel, int ersatz) => await parameter.ZahlAsync(schluessel, heute, ersatz, ct);

        var organisation = await db.OrganisationAsync(ct);
        var abteilung = abteilungId is { } id ? await db.Abteilungen.AsNoTracking().FirstOrDefaultAsync(a => a.Id == id, ct) : null;
        var kopf = new AntragPdf.Kopfdaten(
            organisation.FeuerwehrName, organisation.FeuerwehrAnschrift, abteilung?.Bezeichnung, abteilung?.Anschrift, organisation.Logo);
        var gemeinde = new AntragPdf.Verantwortliche(
            organisation.GemeindeName, organisation.GemeindeVertretung, organisation.GemeindeAnschrift, organisation.GemeindeTelefon, organisation.GemeindeEmail);

        return new AntragPdf.Rahmen(
            kopf, await laufbahn.RegelnAsync(heute, ct), await Zahl(ParameterSchluessel.VolljaehrigkeitAlter, 18), heute,
            new AntragPdf.Datenschutztexte(
                gemeinde,
                await Text(ParameterSchluessel.DatenschutzBeauftragter),
                await Text(ParameterSchluessel.FotoKanaele),
                await Text(ParameterSchluessel.Art9Grundlage),
                await Text(ParameterSchluessel.AlarmierungBetreiber),
                await Text(ParameterSchluessel.Auftragsverarbeiter),
                await Zahl(ParameterSchluessel.AntraegeLoeschenNachMonate, 12),
                await Zahl(ParameterSchluessel.ErziehungsberechtigteLoeschenAbAlter, 18),
                await Zahl(ParameterSchluessel.KontaktdatenLoeschenNachAustrittMonate, 3),
                await Zahl(ParameterSchluessel.RestdatensatzAufbewahrungJahre, 10),
                await Zahl(ParameterSchluessel.EinsatzberichteAufbewahrungJahre, 10),
                await Zahl(ParameterSchluessel.AlarmdatenAufbewahrungMonate, 6)));
    }
}
