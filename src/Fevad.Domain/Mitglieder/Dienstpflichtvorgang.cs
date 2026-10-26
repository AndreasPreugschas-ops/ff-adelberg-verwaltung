using System.ComponentModel.DataAnnotations;
using Fevad.Domain.Gemeinsam;

namespace Fevad.Domain.Mitglieder;

/// <summary>Art eines Vorgangs zur Dienstpflicht (Satzung § 4 Abs. 5, § 5 Abs. 5 bis 7; V57).</summary>
public enum DienstpflichtArt
{
    /// <summary>Anzeige einer Abwesenheit von mehr als zwei Wochen an den Kommandanten.</summary>
    [Display(Name = "Abwesenheitsanzeige")] Abwesenheitsanzeige,
    /// <summary>Beurlaubung auf Antrag, z. B. aus beruflichen oder familiären Gründen.</summary>
    [Display(Name = "Beurlaubung")] Beurlaubung,
    /// <summary>Anhörung vor einer Maßnahme.</summary>
    [Display(Name = "Anhörung")] Anhoerung,
    /// <summary>Verweis durch den Kommandanten.</summary>
    [Display(Name = "Verweis")] Verweis,
    /// <summary>Antrag auf Geldbuße an den Bürgermeister.</summary>
    [Display(Name = "Antrag Geldbuße an den Bürgermeister")] AntragGeldbusse,
    /// <summary>Beschluss oder Entscheidung, z. B. des Bürgermeisters oder Gemeinderats.</summary>
    [Display(Name = "Beschluss / Entscheidung")] Beschluss,
}

/// <summary>
/// Vorgang zur Dienstpflicht in der Mitgliederakte (V57): Abwesenheit, Beurlaubung und Ordnungsmaßnahmen mit Anhörung. Nur Sachstand, keine
/// Gründe zur Gesundheit; gelöscht nach der Frist im Löschkonzept (L61).
/// </summary>
public class Dienstpflichtvorgang : Entitaet
{
    /// <summary>Schlüssel des Mitglieds.</summary>
    public Guid MitgliedId { get; set; }
    /// <summary>Das Mitglied.</summary>
    public Mitglied? Mitglied { get; set; }

    /// <summary>Art des Vorgangs.</summary>
    public DienstpflichtArt Art { get; set; }

    /// <summary>Tag der Anzeige, des Antrags oder der Entscheidung.</summary>
    public DateOnly Datum { get; set; }

    /// <summary>Beginn des Zeitraums (Abwesenheit, Beurlaubung).</summary>
    public DateOnly? Von { get; set; }

    /// <summary>Ende des Zeitraums (Abwesenheit, Beurlaubung).</summary>
    public DateOnly? Bis { get; set; }

    /// <summary>Kurzer Sachstand.</summary>
    public string? Sachverhalt { get; set; }

    /// <summary>Tag, an dem der Vorgang dem Bürgermeister mitgeteilt oder vorgelegt wurde.</summary>
    public DateOnly? BuergermeisterMitgeteiltAm { get; set; }

    /// <summary>Wer den Vorgang erfasst hat.</summary>
    public string? ErfasstVon { get; set; }

    /// <summary>Letzter Tag, auf den sich der Vorgang bezieht; ab da läuft die Löschfrist.</summary>
    public DateOnly Bezugstag => Bis is { } bis && bis > Datum ? bis : Datum;

    /// <summary>Am Stichtag beurlaubt oder abwesend gemeldet.</summary>
    /// <param name="tag">Der Tag.</param>
    /// <returns><c>true</c>, wenn der Zeitraum den Tag umfasst.</returns>
    public bool Laeuft(DateOnly tag) =>
        Art is DienstpflichtArt.Abwesenheitsanzeige or DienstpflichtArt.Beurlaubung && Von is { } von && von <= tag && (Bis is null || Bis >= tag);
}
