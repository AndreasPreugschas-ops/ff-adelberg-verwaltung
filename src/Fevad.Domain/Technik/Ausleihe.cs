using System.ComponentModel.DataAnnotations;
using Fevad.Domain.Gemeinsam;

namespace Fevad.Domain.Technik;

/// <summary>Richtung einer Ausleihe (V63).</summary>
public enum AusleiheRichtung
{
    /// <summary>Eigenes Gerät an eine andere Stelle verliehen.</summary>
    [Display(Name = "verliehen")] Verliehen,
    /// <summary>Fremdgerät von einer anderen Stelle geliehen.</summary>
    [Display(Name = "geliehen (Fremdgerät)")] Geliehen,
}

/// <summary>
/// Ausleihe eines Geräts an eine andere Feuerwehr, Firma oder die Gemeinde, oder ein geliehenes Fremdgerät (V63).
/// Partner sind Stellen, keine Privatpersonen.
/// </summary>
public class Ausleihe : Entitaet
{
    /// <summary>Verliehen oder geliehen.</summary>
    public AusleiheRichtung Richtung { get; set; }

    /// <summary>Eigenes Gerät; bei Fremdgeräten nur, wenn sie als Gerät mit fremdem Eigentümer geführt werden.</summary>
    public Guid? GeraetId { get; set; }
    /// <summary>Das Gerät.</summary>
    public Geraet? Geraet { get; set; }

    /// <summary>Bezeichnung des Fremdgeräts, wenn es kein eigenes Gerät gibt.</summary>
    public string? Bezeichnung { get; set; }

    /// <summary>Andere Stelle, z. B. „FF Schorndorf“ oder „Bauhof Adelberg“.</summary>
    public required string Partner { get; set; }

    /// <summary>Beginn der Ausleihe.</summary>
    public DateOnly Von { get; set; }

    /// <summary>Vereinbarte Rückgabe.</summary>
    public DateOnly? BisGeplant { get; set; }

    /// <summary>Tag der Rückgabe; leer, solange ausgeliehen.</summary>
    public DateOnly? ZurueckAm { get; set; }

    /// <summary>Zustand bei der Rückgabe (Sachstand).</summary>
    public string? Rueckgabevermerk { get; set; }

    /// <summary>Bemerkung (Sachstand).</summary>
    public string? Bemerkung { get; set; }

    /// <summary>Noch nicht zurück.</summary>
    public bool Offen => ZurueckAm is null;

    /// <summary>Die vereinbarte Rückgabe ist vorbei.</summary>
    /// <param name="heute">Stichtag.</param>
    /// <returns>Wahr, wenn überfällig.</returns>
    public bool Ueberfaellig(DateOnly heute) => Offen && BisGeplant is { } bis && bis < heute;

    /// <summary>Anzeigename des Gegenstands.</summary>
    public string Gegenstand => Geraet is { } g ? $"{g.Inventarnummer} {g.Bezeichnung}" : Bezeichnung ?? "–";
}
