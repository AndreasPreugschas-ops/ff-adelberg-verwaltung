namespace FfAdelberg.Web.Infrastruktur;

public sealed class BetriebOptionen
{
    public const string Abschnitt = "Betrieb";

    /// <summary>Notbetrieb im Gerätehaus (Stufe 2). Zeigt bisher nur den Hinweisbalken.</summary>
    public bool Notbetrieb { get; set; }
}

public sealed class Fe2Optionen
{
    public const string Abschnitt = "Fe2";

    /// <summary>Geheimer Wert im Header X-Fe2-Token. Leer = Webhook abgeschaltet.</summary>
    public string? WebhookToken { get; set; }
}
