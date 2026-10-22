using FfAdelberg.Infrastructure.Mail;
using FfAdelberg.Infrastructure.Persistenz;
using FfAdelberg.Infrastructure.Persistenz.Interceptoren;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace FfAdelberg.Infrastructure;

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

        services.AddDbContext<AppDbContext>((sp, options) => options
            .UseNpgsql(connectionString, npgsql => npgsql.MigrationsAssembly(typeof(AppDbContext).Assembly.FullName))
            .UseSnakeCaseNamingConvention()
            .AddInterceptors(sp.GetRequiredService<AuditInterceptor>()));

        return services;
    }
}
