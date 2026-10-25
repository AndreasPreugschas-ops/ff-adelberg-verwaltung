namespace Fevad.Web.Infrastruktur;

/// <summary>Betriebseinstellungen aus dem Konfigurationsabschnitt <c>Betrieb</c>.</summary>
public sealed class BetriebOptionen
{
    /// <summary>Name des Konfigurationsabschnitts.</summary>
    public const string Abschnitt = "Betrieb";

    /// <summary>Notbetrieb im Gerätehaus (Stufe 2). Zeigt bisher nur den Hinweisbalken.</summary>
    public bool Notbetrieb { get; set; }
}

/// <summary>Einstellungen des FE2-Empfangs aus dem Konfigurationsabschnitt <c>Fe2</c>.</summary>
public sealed class Fe2Optionen
{
    /// <summary>Name des Konfigurationsabschnitts.</summary>
    public const string Abschnitt = "Fe2";

    /// <summary>Geheimer Wert im Header X-Fe2-Token. Leer = Webhook abgeschaltet.</summary>
    public string? WebhookToken { get; set; }
}
