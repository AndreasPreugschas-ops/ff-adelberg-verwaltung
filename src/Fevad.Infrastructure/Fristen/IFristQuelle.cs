using Fevad.Domain.Gemeinsam;

namespace Fevad.Infrastructure.Fristen;

/// <summary>
/// Eine Frist für die Erinnerungsmail. <see cref="Empfaenger"/> ist eine Policy aus <c>Berechtigungen</c>;
/// die Mail geht an alle aktiven Konten mit einer Rolle dieser Policy.
/// </summary>
public sealed record Fristhinweis(
    string Bereich,
    string Gegenstand,
    string Bezeichnung,
    DateOnly? FaelligAm,
    Fristampel Ampel,
    string Empfaenger);

/// <summary>Liefert ablaufende und abgelaufene Fristen für die Erinnerung. Jedes Modul (Personal, Technik) registriert eine eigene.</summary>
public interface IFristQuelle
{
    Task<IReadOnlyList<Fristhinweis>> FaelligeAsync(DateOnly stichtag, CancellationToken ct);
}
