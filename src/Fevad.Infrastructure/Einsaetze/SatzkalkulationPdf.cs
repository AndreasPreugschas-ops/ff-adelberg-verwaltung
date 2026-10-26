using System.Globalization;
using Fevad.Domain.Einsaetze;
using Fevad.Infrastructure.Antraege;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace Fevad.Infrastructure.Einsaetze;

/// <summary>Kalkulation des Personalstundensatzes nach § 34 Abs. 5 FwG als Anlage zur Gemeinderatsvorlage (V24).</summary>
public static class SatzkalkulationPdf
{
    static SatzkalkulationPdf()
    {
        QuestPDF.Settings.License = LicenseType.Community;
    }

    private static readonly CultureInfo Deutsch = new("de-DE");

    private static string Euro(decimal betrag) => betrag.ToString("N2", Deutsch) + " €";

    /// <summary>Erzeugt das PDF.</summary>
    /// <param name="kopf">Briefkopf der Gemeinde.</param>
    /// <param name="k">Die Kalkulation.</param>
    /// <param name="wiedervorlage">Tag der Wiedervorlage.</param>
    /// <returns>Das PDF als Bytes.</returns>
    public static byte[] Erzeugen(AntragPdf.Kopfdaten kopf, Satzkalkulation k, DateOnly wiedervorlage) =>
        Document.Create(dokument => dokument.Page(seite =>
        {
            AntragPdf.Seitenrahmen(seite);
            seite.Header().Element(c => AntragPdf.Briefkopf(c, kopf, "Kalkulation des Stundensatzes für Einsatzkräfte"));
            seite.Content().Column(spalte =>
            {
                spalte.Spacing(8);
                spalte.Item().Text($"Anlage zur Gemeinderatsvorlage – Stand {k.KalkuliertAm.ToString("dd.MM.yyyy", Deutsch)}").SemiBold();
                spalte.Item().Text("Nach § 34 Abs. 5 FwG werden die Kosten für eine Einsatzkraft je Stunde aus den durchschnittlichen jährlichen Kosten "
                    + "der Einsatzabteilung, geteilt durch die Zahl der Angehörigen und die Stunden je Angehörigem, zuzüglich der Entschädigung ermittelt.");
                spalte.Item().Table(t =>
                {
                    t.ColumnsDefinition(c =>
                    {
                        c.RelativeColumn(3);
                        c.RelativeColumn(1);
                    });
                    void Zeile(string text, string wert, bool fett = false)
                    {
                        t.Cell().BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten1).PaddingVertical(3).Text(text).SemiBold().FontColor(fett ? Colors.Black : Colors.Grey.Darken3);
                        t.Cell().BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten1).PaddingVertical(3).AlignRight().Text(wert).SemiBold();
                    }

                    Zeile($"Jahreskosten der Einsatzabteilung {k.ErstesJahr}", Euro(k.Kosten1));
                    Zeile($"Jahreskosten {k.ErstesJahr + 1}", Euro(k.Kosten2));
                    Zeile($"Jahreskosten {k.ErstesJahr + 2}", Euro(k.Kosten3));
                    Zeile($"Jahreskosten {k.ErstesJahr + 3}", Euro(k.Kosten4));
                    Zeile("Durchschnitt der vier Jahre", Euro(k.Mittelwert), true);
                    Zeile("Angehörige der Einsatzabteilung", k.Angehoerige.ToString(Deutsch));
                    Zeile("Stunden je Angehörigem und Jahr", k.StundenJeAngehoerigem.ToString("0.##", Deutsch));
                    Zeile("Kostenanteil je Stunde", Euro(k.KostenJeStunde));
                    Zeile("zuzüglich Entschädigungssatz je Stunde", Euro(k.Entschaedigungssatz));
                    Zeile("Stundensatz je Einsatzkraft", Euro(k.Stundensatz), true);
                });
                if (!string.IsNullOrWhiteSpace(k.Erlaeuterung))
                {
                    spalte.Item().PaddingTop(4).Text("Erläuterung").SemiBold();
                    spalte.Item().Text(k.Erlaeuterung);
                }

                spalte.Item().PaddingTop(4).Text($"Der Satz gilt erst, wenn der Gemeinderat ihn in der Kostenersatzsatzung beschlossen hat. "
                    + $"Wiedervorlage zur Neuberechnung: {wiedervorlage.ToString("dd.MM.yyyy", Deutsch)}.");
                if (!string.IsNullOrWhiteSpace(k.Bearbeiter))
                {
                    spalte.Item().Text($"Kalkuliert von {k.Bearbeiter}").FontSize(8.5f).FontColor(Colors.Grey.Darken1);
                }
            });
        })).AlsPdf();
}
