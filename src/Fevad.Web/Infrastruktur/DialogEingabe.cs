using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.ViewFeatures;

namespace Fevad.Web.Infrastruktur;

/// <summary>
/// Bearbeiten im Dialog: Unlesbare Eingaben (z. B. „31.02.2026“ im Datumsfeld) brechen ab, bevor der Handler etwas ändert.
/// Statt zu speichern geht es auf die aufrufende Seite zurück; dort öffnet sich derselbe Dialog wieder mit allen Eingaben,
/// der Fehler steht am Feld (fevad.js). Gilt für jedes Formular in einem <c>&lt;dialog class="dialog"&gt;</c>; fevad.js
/// schickt dafür das Feld <see cref="Feld"/> mit. Prüfbefund BF-01 vom 06.11.2026.
/// </summary>
public sealed class DialogEingabeFilter(ITempDataDictionaryFactory tempDataFabrik) : IAsyncPageFilter
{
    /// <summary>Formularfeld mit der Kennung des Dialogs, aus dem das Formular kommt.</summary>
    public const string Feld = "_dialog";

    /// <summary>Schlüssel im TempData für den Entwurf, den fevad.js wieder in den Dialog füllt.</summary>
    public const string Entwurf = "DialogEntwurf";

    /// <summary>Felder, die nicht in den Entwurf gehören.</summary>
    private static readonly HashSet<string> Ausgelassen = new(StringComparer.OrdinalIgnoreCase) { "__RequestVerificationToken", Feld };

    /// <summary>Nichts zu tun bei der Auswahl des Handlers.</summary>
    /// <param name="context">Kontext.</param>
    /// <returns>Erledigte Aufgabe.</returns>
    public Task OnPageHandlerSelectionAsync(PageHandlerSelectedContext context) => Task.CompletedTask;

    /// <summary>Bricht einen Dialog-Post mit unlesbaren Eingaben ab und leitet mit Entwurf und Fehlern zurück.</summary>
    /// <param name="context">Kontext mit gebundenen Werten und ModelState.</param>
    /// <param name="next">Der Handler.</param>
    /// <returns>Erledigte Aufgabe.</returns>
    public async Task OnPageHandlerExecutionAsync(PageHandlerExecutingContext context, PageHandlerExecutionDelegate next)
    {
        var anfrage = context.HttpContext.Request;
        if (!HttpMethods.IsPost(anfrage.Method) || !anfrage.HasFormContentType)
        {
            await next();
            return;
        }

        var formular = await anfrage.ReadFormAsync();
        var dialog = formular[Feld].ToString();
        if (dialog.Length == 0)
        {
            await next();
            return;
        }

        // Nur Fehler an tatsächlich eingegebenen Feldern zählen: Pflichtangaben anderer Formulare derselben Seite bleiben außen vor.
        var fehler = context.ModelState
            .Where(e => e.Value is { Errors.Count: > 0 } && !string.IsNullOrWhiteSpace(e.Value.AttemptedValue) && formular.ContainsKey(e.Key))
            .ToDictionary(e => e.Key, e => e.Value!.Errors.Select(f => f.ErrorMessage).FirstOrDefault(m => !string.IsNullOrWhiteSpace(m)) ?? "Ungültige Eingabe.");
        if (fehler.Count == 0)
        {
            await next();
            return;
        }

        var werte = formular.Where(f => !Ausgelassen.Contains(f.Key)).ToDictionary(f => f.Key, f => f.Value.ToArray());
        var tempData = tempDataFabrik.GetTempData(context.HttpContext);
        tempData[Entwurf] = JsonSerializer.Serialize(new { dialog, werte, fehler });
        tempData["Fehler"] = "Nicht gespeichert: Bitte die markierte Eingabe im Dialog korrigieren.";
        context.Result = new LocalRedirectResult(Ziel(context.HttpContext));
    }

    /// <summary>Rücksprung: Feld <c>zurueck</c>, sonst die aufrufende Seite, sonst dieselbe Adresse.</summary>
    /// <param name="kontext">Der laufende Aufruf.</param>
    /// <returns>Lokale Adresse.</returns>
    private static string Ziel(HttpContext kontext)
    {
        var anfrage = kontext.Request;
        static bool Lokal(string adresse) => adresse.StartsWith('/') && !adresse.StartsWith("//", StringComparison.Ordinal) && !adresse.StartsWith("/\\", StringComparison.Ordinal);

        var zurueck = anfrage.Form[Rueckweg.Feld].ToString();
        if (zurueck.Length > 0 && Lokal(zurueck))
        {
            return zurueck;
        }

        if (Uri.TryCreate(anfrage.Headers.Referer.ToString(), UriKind.Absolute, out var herkunft)
            && string.Equals(herkunft.Authority, anfrage.Host.Value, StringComparison.OrdinalIgnoreCase))
        {
            return herkunft.PathAndQuery;
        }

        return $"{anfrage.PathBase}{anfrage.Path}{anfrage.QueryString}";
    }
}
