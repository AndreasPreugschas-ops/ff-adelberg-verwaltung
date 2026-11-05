using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Fevad.Web.Infrastruktur;

/// <summary>
/// Bearbeiten im Dialog: Ein Dialog schickt im Feld <c>zurueck</c> die Adresse der aufrufenden Seite mit. Nach dem Speichern
/// geht es dorthin zurück, ohne Anker und ohne Springen.
/// </summary>
public static class Rueckweg
{
    /// <summary>Name des Formularfelds.</summary>
    public const string Feld = "zurueck";

    /// <summary>Adresse der gerade angezeigten Seite samt Abfrage, als Wert für das Feld <c>zurueck</c>.</summary>
    /// <param name="kontext">Der laufende Aufruf.</param>
    /// <returns>Lokale Adresse.</returns>
    public static string Aktuell(HttpContext kontext) =>
        $"{kontext.Request.PathBase}{kontext.Request.Path}{kontext.Request.QueryString}";

    /// <summary>Leitet auf die mitgeschickte Rücksprungadresse weiter, sonst auf <paramref name="sonst"/>.</summary>
    /// <param name="seite">Die bearbeitende Seite.</param>
    /// <param name="sonst">Weiterleitung ohne (gültige) Rücksprungadresse.</param>
    /// <returns>Die Weiterleitung.</returns>
    public static IActionResult ZurueckOder(this PageModel seite, IActionResult sonst)
    {
        var ziel = Ziel(seite);
        return ziel is null ? sonst : seite.LocalRedirect(ziel);
    }

    /// <summary>Die mitgeschickte Rücksprungadresse (Formular oder Abfrage), nur wenn sie lokal ist.</summary>
    /// <param name="seite">Die Seite.</param>
    /// <returns>Lokale Adresse oder <c>null</c>.</returns>
    public static string? Ziel(PageModel seite)
    {
        var anfrage = seite.Request;
        var wert = anfrage.HasFormContentType ? anfrage.Form[Feld].ToString() : "";
        if (wert.Length == 0)
        {
            wert = anfrage.Query[Feld].ToString();
        }

        return wert.Length > 0 && seite.Url.IsLocalUrl(wert) ? wert : null;
    }
}
