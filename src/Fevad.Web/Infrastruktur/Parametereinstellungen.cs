using System.Globalization;
using Fevad.Domain.Gemeinsam;
using Fevad.Infrastructure.Persistenz;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.EntityFrameworkCore;

namespace Fevad.Web.Infrastruktur;

/// <summary>Eine Zahl aus <c>Parameter</c>, die in der Oberfläche einstellbar ist. <see cref="JaNein"/>: 1 = ja, 0 = nein.</summary>
public sealed record Einstellung(
    string Gruppe, string Schluessel, string Bezeichnung, string Einheit, int Standard, int Min, int Max, string? Hinweis = null, bool JaNein = false);

/// <summary>
/// Liest und speichert einstellbare Parameter. Eine Änderung legt einen neuen Wert ab heute an; der alte bleibt mit seinem
/// Gültigkeitsbeginn erhalten (nachvollziehbar, wer wann was galt).
/// </summary>
public sealed class Parametereinstellungen(AppDbContext db, ParameterLeser parameter)
{
    /// <summary>Liest die am Tag gültigen Werte, sonst die Vorgabe.</summary>
    /// <param name="einstellungen">Die Einstellungen.</param>
    /// <param name="heute">Stichtag.</param>
    /// <param name="ct">Abbruchsignal.</param>
    /// <returns>Werte je Parameterschlüssel.</returns>
    public async Task<Dictionary<string, int>> LadenAsync(IEnumerable<Einstellung> einstellungen, DateOnly heute, CancellationToken ct)
    {
        var werte = new Dictionary<string, int>();
        foreach (var e in einstellungen)
        {
            werte[e.Schluessel] = await parameter.ZahlAsync(e.Schluessel, heute, e.Standard, ct);
        }

        return werte;
    }

    /// <summary>Prüft die Grenzen und speichert geänderte Werte; liefert die Zahl der Änderungen oder null bei Fehlern.</summary>
    public async Task<int?> SpeichernAsync(
        IReadOnlyList<Einstellung> einstellungen, IDictionary<string, int> werte, ModelStateDictionary modelState, DateOnly heute, CancellationToken ct)
    {
        foreach (var e in einstellungen)
        {
            if (!werte.TryGetValue(e.Schluessel, out var wert) || wert < e.Min || wert > e.Max)
            {
                modelState.AddModelError(string.Empty, e.JaNein
                    ? $"{e.Bezeichnung}: bitte ja oder nein wählen."
                    : $"{e.Bezeichnung}: erlaubt sind {e.Min} bis {e.Max} {e.Einheit}.");
            }
        }

        if (!modelState.IsValid)
        {
            return null;
        }

        var geaendert = 0;
        foreach (var e in einstellungen)
        {
            var neu = werte[e.Schluessel].ToString(CultureInfo.InvariantCulture);
            if (await parameter.WertAsync(e.Schluessel, heute, ct) == neu)
            {
                continue;
            }

            // Mehrere Änderungen am selben Tag überschreiben den Wert von heute.
            var vonHeute = await db.Parameter.FirstOrDefaultAsync(p => p.Schluessel == e.Schluessel && p.GueltigAb == heute, ct);
            if (vonHeute is null)
            {
                db.Parameter.Add(new Parameter { Schluessel = e.Schluessel, Wert = neu, GueltigAb = heute, Quelle = "Einstellung in FEVAD" });
            }
            else
            {
                vonHeute.Wert = neu;
            }

            geaendert++;
        }

        await db.SaveChangesAsync(ct);
        return geaendert;
    }
}
