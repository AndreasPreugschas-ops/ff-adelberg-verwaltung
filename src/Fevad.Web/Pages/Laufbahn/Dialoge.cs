using Fevad.Domain.Laufbahn;
using Fevad.Domain.Mitglieder;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace Fevad.Web.Pages.Laufbahn;

/// <summary>Daten für den Dialog „Mitgliedschaft ändern“ (Partial <c>_MitgliedschaftDialog</c>).</summary>
/// <param name="Mitgliedschaft">Der Abschnitt, mit geladener Abteilung.</param>
/// <param name="Zurueck">Seite, auf die nach dem Speichern zurückgeleitet wird.</param>
/// <param name="DarfBefreiungsgrund">Der Benutzer darf den Befreiungsgrund sehen und ändern.</param>
public sealed record MitgliedschaftDialog(Mitgliedschaft Mitgliedschaft, string Zurueck, bool DarfBefreiungsgrund);

/// <summary>Daten für den Dialog „Dienstgrad ändern“ (Partial <c>_DienstgradDialog</c>).</summary>
/// <param name="Zuordnung">Der erfasste Dienstgrad, mit geladenem Dienstgrad.</param>
/// <param name="Auswahl">Aktive Dienstgrade zur Auswahl.</param>
/// <param name="Zurueck">Seite, auf die nach dem Speichern zurückgeleitet wird.</param>
public sealed record DienstgradDialog(DienstgradZuordnung Zuordnung, IReadOnlyList<SelectListItem> Auswahl, string Zurueck);

/// <summary>Daten für den Dialog „Ehrung ändern“ (Partial <c>_EhrungDialog</c>).</summary>
/// <param name="Ehrung">Die erfasste Ehrung, mit geladener Ehrungsart.</param>
/// <param name="Zurueck">Seite, auf die nach dem Speichern zurückgeleitet wird.</param>
public sealed record EhrungDialog(Ehrung Ehrung, string Zurueck);
