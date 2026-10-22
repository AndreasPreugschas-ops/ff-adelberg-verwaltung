using System.Net;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using FfAdelberg.Infrastructure.Identitaet;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace FfAdelberg.Integration.Tests;

/// <summary>HTTP-Client mit Cookies, der Formulare samt Antiforgery-Token abschickt.</summary>
public sealed partial class Browser(AnwendungFixture fixture)
{
    public HttpClient Client { get; } = fixture.Factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

    public async Task<string> SeiteAsync(string pfad)
    {
        var antwort = await Client.GetAsync(pfad);
        Assert.Equal(HttpStatusCode.OK, antwort.StatusCode);
        return await antwort.Content.ReadAsStringAsync();
    }

    /// <summary>Lädt die Seite, nimmt das Token aus dem ersten Formular und schickt die Felder ab.</summary>
    public async Task<HttpResponseMessage> AbschickenAsync(string pfad, IDictionary<string, string> felder, string? ziel = null)
    {
        var html = await SeiteAsync(pfad);
        var token = TokenMuster().Match(html).Groups[1].Value;
        var inhalt = new List<KeyValuePair<string, string>>(felder) { new("__RequestVerificationToken", token) };
        return await Client.PostAsync(ziel ?? pfad, new FormUrlEncodedContent(inhalt));
    }

    public async Task<HttpResponseMessage> AnmeldenAsync(string email, string passwort) =>
        await AbschickenAsync("/Konto/Anmelden", new Dictionary<string, string> { ["Formular.Email"] = email, ["Formular.Passwort"] = passwort });

    /// <summary>Anmeldung mit Passwort und, falls verlangt, mit Code aus dem Authenticator-Schlüssel.</summary>
    public async Task VollstaendigAnmeldenAsync(string email, string passwort, string? schluessel = null)
    {
        var antwort = await AnmeldenAsync(email, passwort);
        Assert.Equal(HttpStatusCode.Redirect, antwort.StatusCode);
        if (antwort.Headers.Location!.ToString().Contains("/Konto/AnmeldenCode"))
        {
            antwort = await AbschickenAsync(antwort.Headers.Location.ToString(), new Dictionary<string, string> { ["Code"] = Totp(schluessel!) });
            Assert.Equal(HttpStatusCode.Redirect, antwort.StatusCode);
        }

        Assert.DoesNotContain("/Konto/Anmelden", antwort.Headers.Location!.ToString());
    }

    /// <summary>Legt ein Konto mit Passwort an; mit zweiFaktor=true auch mit eingerichtetem Authenticator.</summary>
    public static async Task<(Benutzer Konto, string? Schluessel)> KontoAnlegenAsync(AnwendungFixture fixture, string passwort, bool zweiFaktor, params string[] rollen)
    {
        await using var scope = fixture.Factory.Services.CreateAsyncScope();
        var benutzer = scope.ServiceProvider.GetRequiredService<UserManager<Benutzer>>();
        var email = $"t-{Guid.NewGuid():N}"[..14] + "@ff-adelberg.test";
        var konto = new Benutzer { UserName = email, Email = email };
        Assert.True((await benutzer.CreateAsync(konto, passwort)).Succeeded);
        if (rollen.Length > 0)
        {
            Assert.True((await benutzer.AddToRolesAsync(konto, rollen)).Succeeded);
        }

        string? schluessel = null;
        if (zweiFaktor)
        {
            await benutzer.ResetAuthenticatorKeyAsync(konto);
            schluessel = await benutzer.GetAuthenticatorKeyAsync(konto);
            await benutzer.SetTwoFactorEnabledAsync(konto, true);
        }

        return (konto, schluessel);
    }

    /// <summary>TOTP nach RFC 6238 (SHA-1, 30 s, 6 Stellen) wie in der Authenticator-App.</summary>
    public static string Totp(string base32Schluessel)
    {
        var schluessel = Base32(base32Schluessel.Replace(" ", string.Empty).ToUpperInvariant());
        var zaehler = BitConverter.GetBytes(DateTimeOffset.UtcNow.ToUnixTimeSeconds() / 30);
        if (BitConverter.IsLittleEndian)
        {
            Array.Reverse(zaehler);
        }

        var hash = HMACSHA1.HashData(schluessel, zaehler);
        var versatz = hash[^1] & 0x0F;
        var wert = ((hash[versatz] & 0x7F) << 24) | (hash[versatz + 1] << 16) | (hash[versatz + 2] << 8) | hash[versatz + 3];
        return (wert % 1_000_000).ToString("D6");
    }

    private static byte[] Base32(string text)
    {
        const string alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";
        var bytes = new List<byte>();
        int puffer = 0, bits = 0;
        foreach (var zeichen in text.TrimEnd('='))
        {
            puffer = (puffer << 5) | alphabet.IndexOf(zeichen);
            bits += 5;
            if (bits >= 8)
            {
                bits -= 8;
                bytes.Add((byte)(puffer >> bits));
                puffer &= (1 << bits) - 1;
            }
        }

        return [.. bytes];
    }

    [GeneratedRegex("name=\"__RequestVerificationToken\" type=\"hidden\" value=\"([^\"]+)\"")]
    private static partial Regex TokenMuster();
}
