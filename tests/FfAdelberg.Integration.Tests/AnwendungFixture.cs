using FfAdelberg.Infrastructure.Persistenz.Seed;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Testcontainers.PostgreSql;

namespace FfAdelberg.Integration.Tests;

/// <summary>Startet einmal je Testlauf einen PostgreSQL-Container und die Anwendung dagegen.</summary>
public sealed class AnwendungFixture : IAsyncLifetime
{
    public const string Fe2Token = "test-token";

    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:17").Build();

    public WebApplicationFactory<Program> Factory { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();

        Factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Test");
            builder.UseSetting("ConnectionStrings:Datenbank", _postgres.GetConnectionString());
            builder.UseSetting("Fe2:WebhookToken", Fe2Token);
            builder.UseSetting("Seed:Admin:Email", "admin@ff-adelberg.test");
            builder.UseSetting("Seed:Admin:Passwort", "TestPasswort2026");
            // Sitzungen bei jeder Anfrage prüfen, damit Deaktivieren in Tests sofort greift.
            builder.UseSetting("Anmeldung:SitzungPruefenMinuten", "0");
        });

        await DatenbankInitialisierung.InitialisierenAsync(Factory.Services);
    }

    public async Task DisposeAsync()
    {
        await Factory.DisposeAsync();
        await _postgres.DisposeAsync();
    }
}

[CollectionDefinition(Name)]
public sealed class AnwendungCollection : ICollectionFixture<AnwendungFixture>
{
    public const string Name = "Anwendung";
}
