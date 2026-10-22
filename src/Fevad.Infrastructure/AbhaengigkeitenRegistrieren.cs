using Fevad.Infrastructure.Auswertungen;
using Fevad.Infrastructure.Einsaetze;
using Fevad.Infrastructure.Fristen;
using Fevad.Infrastructure.Entschaedigung;
using Fevad.Infrastructure.Mail;
using Fevad.Infrastructure.Persistenz;
using Fevad.Infrastructure.Persistenz.Interceptoren;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Fevad.Infrastructure;

public static class AbhaengigkeitenRegistrieren
{
    public static IServiceCollection AddInfrastruktur(this IServiceCollection services, IConfiguration konfiguration)
    {
        var connectionString = konfiguration.GetConnectionString("Datenbank")
            ?? throw new InvalidOperationException("ConnectionStrings:Datenbank fehlt.");

        services.TryAddSingleton(TimeProvider.System);
        services.TryAddScoped<IAktuellerBenutzer, KeinBenutzer>();
        services.AddScoped<AuditInterceptor>();
        services.AddScoped<ParameterLeser>();
        services.Configure<MailOptionen>(konfiguration.GetSection(MailOptionen.Abschnitt));
        services.AddSingleton<IMailVersand, SmtpMailVersand>();
        services.AddScoped<Einsatzberichte>();
        services.AddScoped<Personalfristenabfrage>();
        services.AddScoped<IFristQuelle>(sp => sp.GetRequiredService<Personalfristenabfrage>());
        services.AddScoped<FristErinnerung>();
        services.AddScoped<Abrechnungen>();
        services.AddScoped<AbrechnungExport>();
        services.AddScoped<Jahresstatistik>();

        services.AddDbContext<AppDbContext>((sp, options) => options
            .UseNpgsql(connectionString, npgsql => npgsql.MigrationsAssembly(typeof(AppDbContext).Assembly.FullName))
            .UseSnakeCaseNamingConvention()
            .AddInterceptors(sp.GetRequiredService<AuditInterceptor>()));

        return services;
    }
}
