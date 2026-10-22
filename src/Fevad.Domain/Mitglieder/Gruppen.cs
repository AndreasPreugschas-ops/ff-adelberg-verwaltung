using Fevad.Domain.Gemeinsam;

namespace Fevad.Domain.Mitglieder;

/// <summary>Gruppe innerhalb einer Abteilung (z. B. Gruppe 1, Führungsgruppe), etwa als Zielgruppe für Übungen.</summary>
public class Gruppe : Katalogeintrag
{
    public List<GruppenMitgliedschaft> Mitglieder { get; set; } = [];
}

public class GruppenMitgliedschaft : Entitaet
{
    public Guid GruppeId { get; set; }
    public Gruppe? Gruppe { get; set; }

    public Guid MitgliedId { get; set; }
    public Mitglied? Mitglied { get; set; }
}
