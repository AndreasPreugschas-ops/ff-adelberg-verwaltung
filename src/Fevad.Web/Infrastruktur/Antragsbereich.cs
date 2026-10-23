using Fevad.Domain.Gemeinsam;
using Fevad.Domain.Mitglieder;
using Fevad.Infrastructure.Antraege;
using Fevad.Infrastructure.Identitaet;
using Fevad.Infrastructure.Laufbahn;
using Fevad.Infrastructure.Persistenz;

namespace Fevad.Web.Infrastruktur;

/// <summary>
/// Welche Anträge jemand sieht und was das Formular braucht. Sichtbar ist ein Antrag, wenn Antraege.Bearbeiten seine
/// Zielabteilung umfasst oder das Mitglied dazu derzeit in einer freigegebenen Abteilung ist (der Jugendwart sieht so den
/// Übertritt seiner Jugendlichen). Entscheiden darf nur, wessen Freigabe die Zielabteilung umfasst.
/// </summary>
public sealed class Antragsbereich(Datenbereich datenbereich, Laufbahnabfrage laufbahn, ParameterLeser parameter)
{
    public const string Organisation = "Freiwillige Feuerwehr Adelberg";

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

    public async Task<AntragPdf.Rahmen> RahmenAsync(string? abteilung, DateOnly heute, CancellationToken ct)
    {
        async Task<string> Text(string schluessel) => await parameter.WertAsync(schluessel, heute, ct) ?? "[fehlt]";
        async Task<int> Zahl(string schluessel, int ersatz) => await parameter.ZahlAsync(schluessel, heute, ersatz, ct);

        return new AntragPdf.Rahmen(
            Organisation, abteilung, await laufbahn.RegelnAsync(heute, ct), await Zahl(ParameterSchluessel.VolljaehrigkeitAlter, 18), heute,
            new AntragPdf.Datenschutztexte(
                await Text(ParameterSchluessel.DatenschutzVerantwortlicher),
                await Text(ParameterSchluessel.DatenschutzBeauftragter),
                await Text(ParameterSchluessel.FotoKanaele),
                await Zahl(ParameterSchluessel.AntraegeLoeschenNachMonate, 12),
                await Zahl(ParameterSchluessel.ErziehungsberechtigteLoeschenAbAlter, 18),
                await Zahl(ParameterSchluessel.KontaktdatenLoeschenNachAustrittMonate, 3)));
    }
}
