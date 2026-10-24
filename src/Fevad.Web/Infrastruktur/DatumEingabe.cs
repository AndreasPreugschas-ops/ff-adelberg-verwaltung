using System.Globalization;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Razor.TagHelpers;

namespace Fevad.Web.Infrastruktur;

/// <summary>
/// Datumsfelder: tippen statt klicken. Erlaubt "24.10.2026", "24.10.26", "24102026", "24.10." (laufendes Jahr), "h" für heute
/// und das ISO-Format der Kalenderauswahl. Andi (24.10.2026): Text eintippen plus Schnellauswahl, keine Zeiger-Uhr.
/// </summary>
public static class DatumEingabe
{
    private static readonly CultureInfo Deutsch = new("de-DE");
    private static readonly string[] Formate = ["yyyy-MM-dd", "d.M.yyyy", "d.M.yy", "ddMMyyyy", "ddMMyy", "d.M.", "dd.MM."];

    public static string Text(DateOnly? datum) => datum is null ? "" : datum.Value.ToString("dd.MM.yyyy", Deutsch);

    /// <summary>Text in der Oberfläche: ISO-Werte (aus Modell oder Kalender) deutsch, alles andere unverändert.</summary>
    public static string Anzeige(string? wert) =>
        TryLesen(wert, null, out var datum) && datum is not null ? Text(datum) : wert ?? "";

    /// <summary>Leerer Text ergibt null; "h"/"heute" das Tagesdatum (nur mit <paramref name="heute"/>). False, wenn der Text kein Datum ist.</summary>
    public static bool TryLesen(string? text, DateOnly? heute, out DateOnly? datum)
    {
        datum = null;
        if (string.IsNullOrWhiteSpace(text))
        {
            return true;
        }

        var eingabe = text.Trim();
        if (heute is { } h && (eingabe.Equals("h", StringComparison.OrdinalIgnoreCase) || eingabe.Equals("heute", StringComparison.OrdinalIgnoreCase)))
        {
            datum = h;
            return true;
        }

        if (!DateTime.TryParseExact(eingabe, Formate, Deutsch, DateTimeStyles.None, out var wert))
        {
            return false;
        }

        datum = DateOnly.FromDateTime(wert);
        return true;
    }
}

/// <summary>Bindet DateOnly aus allen Schreibweisen von <see cref="DatumEingabe"/>; die Kalenderauswahl liefert weiter ISO.</summary>
public sealed class DatumBinderProvider : IModelBinderProvider
{
    public IModelBinder? GetBinder(ModelBinderProviderContext context) =>
        context.Metadata.UnderlyingOrModelType == typeof(DateOnly) ? new DatumBinder() : null;
}

public sealed class DatumBinder : IModelBinder
{
    public Task BindModelAsync(ModelBindingContext context)
    {
        var ergebnis = context.ValueProvider.GetValue(context.ModelName);
        if (ergebnis == ValueProviderResult.None)
        {
            return Task.CompletedTask;
        }

        context.ModelState.SetModelValue(context.ModelName, ergebnis);
        var heute = Fevad.Web.Infrastruktur.Anzeige.LokalesDatum(context.HttpContext.RequestServices.GetRequiredService<TimeProvider>().GetUtcNow());
        if (!DatumEingabe.TryLesen(ergebnis.FirstValue, heute, out var datum))
        {
            context.ModelState.TryAddModelError(context.ModelName, "Kein gültiges Datum (TT.MM.JJJJ).");
            return Task.CompletedTask;
        }

        if (datum is null && !context.ModelMetadata.IsReferenceOrNullableType)
        {
            context.ModelState.TryAddModelError(context.ModelName, context.ModelMetadata.ModelBindingMessageProvider.ValueMustNotBeNullAccessor(ergebnis.FirstValue ?? ""));
            return Task.CompletedTask;
        }

        context.Result = ModelBindingResult.Success(datum);
        return Task.CompletedTask;
    }
}

/// <summary>
/// Macht aus jedem <c>&lt;input type="date"&gt;</c> ein Textfeld (TT.MM.JJJJ) mit Kalenderknopf: tippen geht immer, der Kalender
/// ist die Schnellauswahl. Läuft nach dem eingebauten Input-TagHelper, der Wert bleibt am Textfeld (Name und Alpine-Bindungen auch).
/// </summary>
[HtmlTargetElement("input", Attributes = "[type=date]")]
public sealed class DatumTagHelper : TagHelper
{
    public override int Order => 1000;

    public override void Process(TagHelperContext context, TagHelperOutput output)
    {
        if (output.Attributes.TryGetAttribute("type", out var typ) && !string.Equals(typ.Value?.ToString(), "date", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        var wert = output.Attributes.TryGetAttribute("value", out var w) ? w.Value?.ToString() : null;
        output.Attributes.SetAttribute("type", "text");
        output.Attributes.SetAttribute("value", DatumEingabe.Anzeige(wert));
        output.Attributes.SetAttribute("inputmode", "numeric");
        output.Attributes.SetAttribute("autocomplete", "off");
        output.Attributes.SetAttribute("data-datum", "");
        if (!output.Attributes.ContainsName("placeholder"))
        {
            output.Attributes.SetAttribute("placeholder", "TT.MM.JJJJ");
        }

        var grenzen = string.Concat(new[] { "min", "max" }.Select(n => output.Attributes.TryGetAttribute(n, out var g) ? $" {n}=\"{g.Value}\"" : ""));
        output.Attributes.RemoveAll("min");
        output.Attributes.RemoveAll("max");

        output.PreElement.AppendHtml("<span class=\"fevad-datum\">");
        output.PostElement.AppendHtml(
            $"<input type=\"date\" class=\"fevad-datum-nativ\" tabindex=\"-1\" aria-hidden=\"true\"{grenzen} />" +
            "<button type=\"button\" class=\"fevad-datum-knopf\" title=\"Kalender öffnen\" aria-label=\"Kalender öffnen\">" +
            "<svg xmlns=\"http://www.w3.org/2000/svg\" viewBox=\"0 0 20 20\" fill=\"currentColor\" class=\"h-5 w-5\" aria-hidden=\"true\"><path fill-rule=\"evenodd\" d=\"M5.75 2a.75.75 0 0 1 .75.75V4h7V2.75a.75.75 0 0 1 1.5 0V4h.25A2.75 2.75 0 0 1 18 6.75v8.5A2.75 2.75 0 0 1 15.25 18H4.75A2.75 2.75 0 0 1 2 15.25v-8.5A2.75 2.75 0 0 1 4.75 4H5V2.75A.75.75 0 0 1 5.75 2Zm-1 5.5c-.69 0-1.25.56-1.25 1.25v6.5c0 .69.56 1.25 1.25 1.25h10.5c.69 0 1.25-.56 1.25-1.25v-6.5c0-.69-.56-1.25-1.25-1.25H4.75Z\" clip-rule=\"evenodd\"/></svg>" +
            "</button></span>");
    }
}

/// <summary>Uhrzeitfelder (HH:MM, 24 Stunden): Textfeld mit Zifferntastatur und Viertelstunden-Liste zur Schnellauswahl, keine Zeiger-Uhr.</summary>
[HtmlTargetElement("input", Attributes = "fevad-uhrzeit")]
public sealed class UhrzeitTagHelper : TagHelper
{
    public override int Order => 1000;

    public override void Process(TagHelperContext context, TagHelperOutput output)
    {
        output.Attributes.RemoveAll("fevad-uhrzeit");
        output.Attributes.SetAttribute("inputmode", "numeric");
        output.Attributes.SetAttribute("autocomplete", "off");
        output.Attributes.SetAttribute("list", "fevad-uhrzeiten");
        output.Attributes.SetAttribute("maxlength", "5");
        if (!output.Attributes.ContainsName("placeholder"))
        {
            output.Attributes.SetAttribute("placeholder", "HH:MM");
        }
    }
}
