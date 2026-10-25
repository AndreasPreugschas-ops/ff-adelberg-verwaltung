namespace Fevad.Domain.Laufbahn;

/// <summary>
/// Altersgrenzen und Fristen aus der Satzung. Kommen aus <c>Parameter</c>, damit eine Satzungsänderung
/// (etwa Dienstende 67) ohne neue Programmversion geht.
/// </summary>
/// <param name="ProbezeitMonate">Dauer der Probezeit in der Einsatzabteilung.</param>
/// <param name="AufnahmeEinsatzAbAlter">Mindestalter für den Übertritt in die Einsatzabteilung (Satzung: 17).</param>
/// <param name="EinsatzdienstAbAlter">Alter, ab dem Einsatzdienst geleistet werden darf (18).</param>
/// <param name="JugendEndeAlter">Alter, mit dem die Jugendfeuerwehr endet.</param>
/// <param name="AltersabteilungAbAlter">Alter, ab dem der Wechsel in die Altersabteilung auf Antrag möglich ist.</param>
/// <param name="AltersgrenzeEinsatzdienst">Alter, mit dem der Dienst in der Einsatzabteilung endet.</param>
/// <param name="VorlaufMonate">So viele Monate vor einem Laufbahnereignis erscheint der Hinweis.</param>
/// <param name="JugendAufnahmeBisAlter">Aufnahme in die Jugendfeuerwehr nur bis vor Vollendung dieses Lebensjahres.</param>
/// <param name="JugendAufnahmeAbAlter">Mindestalter für die Aufnahme in die Jugendfeuerwehr; 0 heißt keine Grenze.</param>
/// <param name="KinderAufnahmeAbAlter">Mindestalter für die Kinderfeuerwehr.</param>
/// <param name="KinderEndeAlter">Alter, mit dem die Kinderfeuerwehr endet; 0 heißt kein Ende.</param>
public sealed record Laufbahnregeln(
    int ProbezeitMonate,
    int AufnahmeEinsatzAbAlter,
    int EinsatzdienstAbAlter,
    int JugendEndeAlter,
    int AltersabteilungAbAlter,
    int AltersgrenzeEinsatzdienst,
    int VorlaufMonate,
    int JugendAufnahmeBisAlter,
    int JugendAufnahmeAbAlter,
    int KinderAufnahmeAbAlter = 6,
    int KinderEndeAlter = 10)
{
    /// <summary>
    /// Werte der Feuerwehrsatzung Adelberg; nur als Ersatz, wenn ein Parameter fehlt. Die Kinderfeuerwehr kennt die Satzung
    /// von 2015 nicht; 6 bis 10 Jahre sind in BW üblich.
    /// </summary>
    public static Laufbahnregeln Satzung { get; } = new(12, 17, 18, 18, 55, 65, 3, 17, 10);
}

/// <summary>Parameter-Schlüssel der Laufbahn. Probezeit, Altersgrenze und Altersabteilung stehen in <c>ParameterSchluessel</c>.</summary>
public static class LaufbahnParameter
{
    /// <summary>Mindestalter für den Übertritt in die Einsatzabteilung.</summary>
    public const string AufnahmeEinsatzAbAlter = "Mitglied.Aufnahme.AbAlter";
    /// <summary>Alter, ab dem Einsatzdienst geleistet werden darf.</summary>
    public const string EinsatzdienstAbAlter = "Mitglied.Einsatzdienst.AbAlter";
    /// <summary>Alter, mit dem die Jugendfeuerwehr endet.</summary>
    public const string JugendEndeAlter = "Jugend.Ende.Alter";
    /// <summary>Aufnahme in die Jugendfeuerwehr nur bis vor Vollendung dieses Lebensjahres.</summary>
    public const string JugendAufnahmeBisAlter = "Jugend.Aufnahme.BisAlter";
    /// <summary>Mindestalter für die Aufnahme in die Jugendfeuerwehr.</summary>
    public const string JugendAufnahmeAbAlter = "Jugend.Aufnahme.AbAlter";
    /// <summary>Mindestalter für die Aufnahme in die Kinderfeuerwehr.</summary>
    public const string KinderAufnahmeAbAlter = "Kinder.Aufnahme.AbAlter";
    /// <summary>Alter, mit dem die Kinderfeuerwehr endet.</summary>
    public const string KinderEndeAlter = "Kinder.Ende.Alter";
    /// <summary>So viele Monate vorher kündigen die Laufbahnhinweise Probezeitende, Übertritte und Altersgrenzen an.</summary>
    public const string VorlaufMonate = "Laufbahn.Vorlauf.Monate";
    /// <summary>Jugendfeuerwehr zählt für das Ehrenzeichen ab diesem Lebensjahr (VwV Feuerwehr-Ehrenzeichen Nr. 4: 14).</summary>
    public const string EhrungJugendAbAlter = "Ehrung.Jugend.AnrechnenAbAlter";
    /// <summary>Bestätigte Zeit in der Altersabteilung zählt für das Ehrenzeichen bis zu diesem Lebensjahr.</summary>
    public const string EhrungAltersabteilungBisAlter = "Ehrung.Altersabteilung.AnrechnenBisAlter";
    /// <summary>Höchstens so viele Monate je Befreiung wegen Kindererziehung werden angerechnet.</summary>
    public const string EhrungKindererziehungMonate = "Ehrung.Kindererziehung.AnrechnenMonate";
    /// <summary>Gemeinsamer Antragsschluss der Verbandsehrungen als „TT.MM.“.</summary>
    public const string EhrungAntragsschlussVerband = "Ehrung.Verband.Antragsschluss";
}
