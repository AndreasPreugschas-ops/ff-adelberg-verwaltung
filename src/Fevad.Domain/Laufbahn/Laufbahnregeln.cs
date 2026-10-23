namespace Fevad.Domain.Laufbahn;

/// <summary>
/// Altersgrenzen und Fristen aus der Satzung. Kommen aus <c>Parameter</c>, damit eine Satzungsänderung
/// (etwa Dienstende 67) ohne neue Programmversion geht.
/// </summary>
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
    public const string AufnahmeEinsatzAbAlter = "Mitglied.Aufnahme.AbAlter";
    public const string EinsatzdienstAbAlter = "Mitglied.Einsatzdienst.AbAlter";
    public const string JugendEndeAlter = "Jugend.Ende.Alter";
    public const string JugendAufnahmeBisAlter = "Jugend.Aufnahme.BisAlter";
    public const string JugendAufnahmeAbAlter = "Jugend.Aufnahme.AbAlter";
    public const string KinderAufnahmeAbAlter = "Kinder.Aufnahme.AbAlter";
    public const string KinderEndeAlter = "Kinder.Ende.Alter";
    public const string VorlaufMonate = "Laufbahn.Vorlauf.Monate";
    public const string EhrungJugendAbAlter = "Ehrung.Jugend.AnrechnenAbAlter";
    public const string EhrungAltersabteilungBisAlter = "Ehrung.Altersabteilung.AnrechnenBisAlter";
    public const string EhrungKindererziehungMonate = "Ehrung.Kindererziehung.AnrechnenMonate";
    public const string EhrungAntragsschlussVerband = "Ehrung.Verband.Antragsschluss";
}
