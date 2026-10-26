using Fevad.Infrastructure.Identitaet;
using Fevad.Infrastructure.Dokumente;
using Fevad.Infrastructure.Auswertungen;
using Fevad.Infrastructure.Datenschutz;
using Fevad.Infrastructure.Dienstbetrieb;
using Fevad.Infrastructure.Einsaetze;
using Fevad.Infrastructure.Gremien;
using Fevad.Infrastructure.Entschaedigung;
using Fevad.Infrastructure.Fristen;
using Fevad.Infrastructure.Laufbahn;
using Fevad.Infrastructure.Kleiderkammer;
using Fevad.Infrastructure.Mail;
using Fevad.Infrastructure.Persistenz;
using Fevad.Infrastructure.Persistenz.Interceptoren;
using Fevad.Infrastructure.Technik;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Fevad.Infrastructure;

/// <summary>Registriert die Dienste der Infrastrukturschicht im Dependency-Injection-Container.</summary>
public static class AbhaengigkeitenRegistrieren
{
    /// <summary>
    /// Registriert Datenbank, Identität und Rechte, Mail, Fachdienste und Hintergrunddienste (Fristerinnerung, Löschlauf, Personalprüfung).
    /// </summary>
    /// <param name="services">Die Dienstsammlung der Anwendung.</param>
    /// <param name="konfiguration">Konfiguration mit Verbindungszeichenfolge <c>ConnectionStrings:Datenbank</c> und Mail-Abschnitt.</param>
    /// <returns>Dieselbe Dienstsammlung für weitere Aufrufe.</returns>
    /// <exception cref="InvalidOperationException">Die Verbindungszeichenfolge fehlt.</exception>
    public static IServiceCollection AddInfrastruktur(this IServiceCollection services, IConfiguration konfiguration)
    {
        var connectionString = konfiguration.GetConnectionString("Datenbank")
            ?? throw new InvalidOperationException("ConnectionStrings:Datenbank fehlt.");

        services.TryAddSingleton(TimeProvider.System);
        services.TryAddScoped<IAktuellerBenutzer, KeinBenutzer>();
        services.AddScoped<AuditInterceptor>();
        services.AddScoped<ParameterLeser>();
        services.AddSingleton<Rechteverzeichnis>();
        services.AddSingleton<Microsoft.AspNetCore.Authorization.IAuthorizationHandler, RechtHandler>();
        services.Configure<MailOptionen>(konfiguration.GetSection(MailOptionen.Abschnitt));
        services.AddSingleton<IMailVersand, SmtpMailVersand>();
        services.AddScoped<Einsatzberichte>();
        services.AddScoped<Kostenersatzrechner>();
        services.AddScoped<Atemschutz.Atemschutzuebernahme>();
        services.AddScoped<Atemschutz.Atemschutzabfrage>();
        services.AddScoped<IDateiablage, DatenbankDateiablage>();
        services.AddScoped<Dokumentablage>();
        services.AddScoped<Gremienarbeit>();
        services.AddScoped<Sitzungseinladungen>();
        services.AddScoped<Jahreskreis>();
        services.AddScoped<Jubilaeen>();
        services.AddScoped<Serienbriefe>();
        services.AddScoped<Fuehrungskennzahlen>();
        services.AddScoped<Ausbildung.Lehrgangswuensche>();
        services.AddScoped<Kassen.Kassenfuehrung>();
        services.AddScoped<Kassen.Steuerauswertung>();
        services.AddScoped<Kassen.Bankimport>();
        services.AddScoped<Kassen.Kassenabschluss>();
        services.AddScoped<Personalfristenabfrage>();
        services.AddScoped<IFristQuelle>(sp => sp.GetRequiredService<Personalfristenabfrage>());
        services.AddScoped<FristErinnerung>();
        services.AddScoped<EigeneErinnerung>();
        services.AddScoped<Import.Importdienst>();
        services.AddScoped<Loeschlauf>();
        services.AddScoped<Leseprotokoll>();
        services.AddScoped<Archivierung>();
        services.AddScoped<Archivpaket>();
        services.AddScoped<Selbstauskunft>();
        services.AddScoped<Abzeichenabfrage>();
        services.AddScoped<Abrechnungen>();
        services.AddScoped<AbrechnungExport>();
        services.AddScoped<Finanzen.Finanzaustausch>();
        services.AddScoped<Arbeitgeberbescheinigungen>();
        services.AddScoped<Jahresstatistik>();
        services.AddScoped<Jugendstatistik>();
        services.AddScoped<Mitglieder.Einwilligungsregister>();
        services.AddScoped<Mitglieder.Betreuerregister>();
        services.AddScoped<Dienstbetrieb.Jugendveranstaltungen>();
        services.AddScoped<Stichtagsliste>();
        services.AddScoped<Laufbahnabfrage>();
        services.AddScoped<IFristQuelle>(sp => sp.GetRequiredService<Laufbahnabfrage>());
        services.AddScoped<Personalpruefung>();
        services.AddSingleton<Pruefwarteschlange>();
        services.AddScoped<PruefAusloeser>();
        services.AddScoped<Dienstplanung>();
        services.AddScoped<Unterweisungen>();
        services.AddScoped<Ausbildungsplaene>();
        services.AddScoped<Fevad.Infrastructure.Einsaetze.Massnahmenliste>();
        services.AddScoped<Pruefstaende>();
        services.AddScoped<Pruefungserfassung>();
        services.AddScoped<Anlagennachweis>();
        services.AddScoped<IFristQuelle>(sp => sp.GetRequiredService<Pruefstaende>());
        services.AddScoped<Kleiderkammerbuchungen>();
        services.AddScoped<Kleiderkammerstand>();
        services.AddScoped<IFristQuelle>(sp => sp.GetRequiredService<Kleiderkammerstand>());

        services.AddDbContext<AppDbContext>((sp, options) => options
            .UseNpgsql(connectionString, npgsql => npgsql.MigrationsAssembly(typeof(AppDbContext).Assembly.FullName))
            .UseSnakeCaseNamingConvention()
            .AddInterceptors(sp.GetRequiredService<AuditInterceptor>(), sp.GetRequiredService<PruefAusloeser>()));

        return services;
    }
}
