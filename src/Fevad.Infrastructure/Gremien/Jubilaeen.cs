using Fevad.Domain.Gemeinsam;
using Fevad.Domain.Gremien;
using Fevad.Domain.Mitglieder;
using Fevad.Infrastructure.Persistenz;
using Microsoft.EntityFrameworkCore;

namespace Fevad.Infrastructure.Gremien;

/// <summary>Jubiläumsvorschau (V56): wer in einem Jahr ein Mitgliedsjubiläum oder einen runden Geburtstag hat, z. B. für Ehrungen in der Hauptversammlung.</summary>
/// <param name="parameter">Jubiläumsstufen.</param>
public sealed class Jubilaeen(ParameterLeser parameter)
{
    /// <summary>Art des Jubiläums.</summary>
    public enum Art
    {
        /// <summary>Jahre seit dem Eintritt in die Feuerwehr (ohne Vordienstzeiten anderswo).</summary>
        Mitgliedschaft,
        /// <summary>Runder Geburtstag.</summary>
        Geburtstag,
    }

    /// <summary>Ein Jubiläum.</summary>
    /// <param name="Tag">Tag im Jahr.</param>
    /// <param name="Name">„Nachname, Vorname“.</param>
    /// <param name="Art">Mitgliedschaft oder Geburtstag.</param>
    /// <param name="Jahre">Jahre.</param>
    public sealed record Eintrag(DateOnly Tag, string Name, Art Art, int Jahre)
    {
        /// <summary>Anzeigetext, z. B. „25 Jahre Mitglied“.</summary>
        public string Text => Art == Art.Geburtstag ? $"{Jahre}. Geburtstag" : $"{Jahre} Jahre Mitglied";
    }

    /// <summary>Jubiläen heutiger Mitglieder in einem Jahr, nach Tag.</summary>
    /// <param name="sichtbar">Die Mitglieder, die der Benutzer mit <c>Mitglieder.Lesen</c> sehen darf; so gelten Abteilungsbezug und Abschirmung der Jugendfeuerwehr.</param>
    /// <param name="jahr">Das Jahr.</param>
    /// <param name="heute">Heutiges Datum: wer heute Mitglied ist.</param>
    /// <param name="ct">Abbruchsignal.</param>
    /// <returns>Die Einträge.</returns>
    public async Task<IReadOnlyList<Eintrag>> ImJahrAsync(IQueryable<Mitglied> sichtbar, int jahr, DateOnly heute, CancellationToken ct = default)
    {
        var mitgliedsjahre = Jubilaeum.Stufen(await parameter.WertAsync(ParameterSchluessel.JubilaeumMitgliedsjahre, heute, ct) ?? "25|40|50|60|70");
        var geburtstage = Jubilaeum.Stufen(await parameter.WertAsync(ParameterSchluessel.JubilaeumGeburtstage, heute, ct) ?? "50|60|70|75|80|85|90");
        var personen = await sichtbar.AsNoTracking()
            .Where(m => m.Mitgliedschaften.Any(s => !s.Vordienstzeit && s.GueltigAb <= heute && (s.GueltigBis == null || s.GueltigBis >= heute)))
            .Select(m => new
            {
                Name = m.Nachname + ", " + m.Vorname,
                m.Geburtsdatum,
                Eintritt = m.Mitgliedschaften.Where(s => !s.Vordienstzeit).Min(s => s.GueltigAb),
                // Runde Geburtstage nur für die Einsatz- und die Altersabteilung, nie für Jugend und Kinder.
                GeburtstagZeigen = m.Mitgliedschaften.Any(s => (s.Abteilung!.Art == AbteilungArt.Einsatz || s.Abteilung.Art == AbteilungArt.Alters)
                    && s.GueltigAb <= heute && (s.GueltigBis == null || s.GueltigBis >= heute)),
            })
            .ToListAsync(ct);

        var liste = new List<Eintrag>();
        foreach (var p in personen)
        {
            if (Jubilaeum.Im(p.Eintritt, jahr, mitgliedsjahre) is { } m)
            {
                liste.Add(new Eintrag(m.Tag, p.Name, Art.Mitgliedschaft, m.Jahre));
            }

            if (p.GeburtstagZeigen && Jubilaeum.Im(p.Geburtsdatum, jahr, geburtstage) is { } g)
            {
                liste.Add(new Eintrag(g.Tag, p.Name, Art.Geburtstag, g.Jahre));
            }
        }

        return liste.OrderBy(e => e.Tag).ThenBy(e => e.Name).ToList();
    }
}
