using Fevad.Domain.Dienstbetrieb;
using Fevad.Domain.Mitglieder;

namespace Fevad.Domain.Tests;

public class DienstplanungTests
{
    private static readonly Guid Einsatzabteilung = Guid.CreateVersion7();
    private static readonly Guid Jugend = Guid.CreateVersion7();
    private static readonly Guid Gruppe1 = Guid.CreateVersion7();
    private static readonly Guid Agt = Guid.CreateVersion7();
    private static readonly DateOnly Tag = new(2026, 11, 5);

    private static Dienst Dienst(Guid? abteilung = null, Guid? gruppe = null, Guid? qualifikation = null) =>
        new() { Titel = "Übung", AbteilungId = abteilung, GruppeId = gruppe, QualifikationId = qualifikation };

    private static Teilnehmerprofil Profil(Guid abteilung, DateOnly ab, DateOnly? bis = null, Guid[]? gruppen = null, (Guid, DateOnly, DateOnly?)[]? qualifikationen = null) =>
        new([new Mitgliedschaft { AbteilungId = abteilung, Status = MitgliedStatus.Aktiv, GueltigAb = ab, GueltigBis = bis }],
            (gruppen ?? []).ToHashSet(), qualifikationen ?? []);

    [Fact]
    public void Woechentliche_Serie_laeuft_bis_einschliesslich_Enddatum()
    {
        var tage = DienstSerie.Tage(Wiederholung.Woechentlich, new(2026, 11, 5), new(2026, 11, 26)).ToList();
        Assert.Equal([new(2026, 11, 5), new(2026, 11, 12), new(2026, 11, 19), new(2026, 11, 26)], tage);
    }

    [Fact]
    public void Zweiwoechentliche_Serie_ueberspringt_jede_zweite_Woche()
    {
        var tage = DienstSerie.Tage(Wiederholung.Zweiwoechentlich, new(2026, 11, 5), new(2026, 12, 31)).ToList();
        Assert.Equal([new(2026, 11, 5), new(2026, 11, 19), new(2026, 12, 3), new(2026, 12, 17), new(2026, 12, 31)], tage);
    }

    [Fact]
    public void Monatliche_Serie_bleibt_beim_gleichen_Wochentag()
    {
        // 12.11.2026 ist der 2. Donnerstag im November.
        var tage = DienstSerie.Tage(Wiederholung.MonatlichWochentag, new(2026, 11, 12), new(2027, 2, 28)).ToList();
        Assert.Equal([new(2026, 11, 12), new(2026, 12, 10), new(2027, 1, 14), new(2027, 2, 11)], tage);
        Assert.All(tage, t => Assert.Equal(DayOfWeek.Thursday, t.DayOfWeek));
    }

    [Fact]
    public void Fuenfter_Wochentag_wird_zum_letzten_im_Monat()
    {
        // 29.10.2026 ist der 5. Donnerstag; im November gibt es nur vier, also der letzte (26.11.).
        var tage = DienstSerie.Tage(Wiederholung.MonatlichWochentag, new(2026, 10, 29), new(2026, 12, 31)).ToList();
        Assert.Equal([new(2026, 10, 29), new(2026, 11, 26), new(2026, 12, 31)], tage);
    }

    [Fact]
    public void Dienst_fuer_alle_betrifft_jedes_aktive_Mitglied()
    {
        Assert.True(Profil(Jugend, new(2020, 1, 1)).Betrifft(Dienst(), Tag));
        Assert.False(Profil(Einsatzabteilung, new(2020, 1, 1), bis: new(2026, 10, 31)).Betrifft(Dienst(), Tag));
    }

    [Fact]
    public void Abteilung_als_Zielgruppe()
    {
        Assert.True(Profil(Einsatzabteilung, new(2020, 1, 1)).Betrifft(Dienst(Einsatzabteilung), Tag));
        Assert.False(Profil(Jugend, new(2020, 1, 1)).Betrifft(Dienst(Einsatzabteilung), Tag));
    }

    [Fact]
    public void Gruppe_und_Qualifikation_schraenken_weiter_ein()
    {
        var mitGruppe = Profil(Einsatzabteilung, new(2020, 1, 1), gruppen: [Gruppe1]);
        Assert.True(mitGruppe.Betrifft(Dienst(Einsatzabteilung, Gruppe1), Tag));
        Assert.False(Profil(Einsatzabteilung, new(2020, 1, 1)).Betrifft(Dienst(gruppe: Gruppe1), Tag));

        Assert.True(Profil(Einsatzabteilung, new(2020, 1, 1), qualifikationen: [(Agt, new(2020, 1, 1), null)]).Betrifft(Dienst(qualifikation: Agt), Tag));
        Assert.False(Profil(Einsatzabteilung, new(2020, 1, 1), qualifikationen: [(Agt, new(2020, 1, 1), new(2026, 10, 1))]).Betrifft(Dienst(qualifikation: Agt), Tag));
        // Erst nach dem Diensttag erworben: zählt noch nicht.
        Assert.False(Profil(Einsatzabteilung, new(2020, 1, 1), qualifikationen: [(Agt, new(2026, 11, 20), null)]).Betrifft(Dienst(qualifikation: Agt), Tag));
    }

    [Fact]
    public void Rueckmeldung_nur_bis_zum_Schluss_und_nicht_bei_Absage()
    {
        var beginn = new DateTimeOffset(2026, 11, 5, 18, 30, 0, TimeSpan.Zero);
        var dienst = new Dienst { Titel = "Übung", Beginn = beginn, Ende = beginn.AddHours(2), Status = DienstStatus.Geplant };

        Assert.True(dienst.RueckmeldungMoeglich(beginn.AddMinutes(-1), 0));
        Assert.False(dienst.RueckmeldungMoeglich(beginn, 0));
        Assert.False(dienst.RueckmeldungMoeglich(beginn.AddHours(-23), 24));

        dienst.Status = DienstStatus.Abgesagt;
        Assert.False(dienst.RueckmeldungMoeglich(beginn.AddDays(-7), 0));

        dienst.Status = DienstStatus.Geplant;
        dienst.RueckmeldungErbeten = false;
        Assert.False(dienst.RueckmeldungMoeglich(beginn.AddDays(-7), 0));
    }
}
