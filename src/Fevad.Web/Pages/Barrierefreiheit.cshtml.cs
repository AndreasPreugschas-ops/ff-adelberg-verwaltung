using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Fevad.Web.Pages;

/// <summary>
/// Erklärung zur Barrierefreiheit (V66, BITV 2.0 § 7 entsprechend über § 10 L-BGG BW): Stand der Vereinbarkeit, bekannte Einschränkungen,
/// Kontakt für Rückmeldungen und das Durchsetzungsverfahren. Ohne Anmeldung erreichbar, wie es die Verordnung verlangt.
/// </summary>
[AllowAnonymous]
public class BarrierefreiheitModel(IConfiguration konfiguration) : PageModel
{
    /// <summary>Kontakt für Rückmeldungen zur Barrierefreiheit (Einstellung <c>Barrierefreiheit:Kontakt</c>, z. B. eine E-Mail-Adresse).</summary>
    public string? Kontakt { get; } = konfiguration["Barrierefreiheit:Kontakt"];

    /// <summary>Datum der letzten Prüfung (Einstellung <c>Barrierefreiheit:Stand</c>, TT.MM.JJJJ).</summary>
    public string? Stand { get; } = konfiguration["Barrierefreiheit:Stand"];
}
