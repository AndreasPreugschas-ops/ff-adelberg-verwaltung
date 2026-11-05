using Microsoft.AspNetCore.Identity;

namespace Fevad.Web.Infrastruktur;

/// <summary>Fehlermeldungen von Identity auf Deutsch.</summary>
public sealed class DeutscheIdentityFehler : IdentityErrorDescriber
{
    /// <summary>Erzeugt einen Identity-Fehler mit Code und deutschem Text.</summary>
    private static IdentityError Fehler(string code, string text) => new() { Code = code, Description = text };

    /// <summary>Deutsche Meldung für <c>DefaultError</c>: „Unbekannter Fehler.“</summary>
    /// <returns>Fehler mit Code und deutschem Text.</returns>
    public override IdentityError DefaultError() => Fehler(nameof(DefaultError), "Unbekannter Fehler.");
    /// <summary>Deutsche Meldung für <c>ConcurrencyFailure</c>: „Das Konto wurde inzwischen geändert. Bitte neu laden.“</summary>
    /// <returns>Fehler mit Code und deutschem Text.</returns>
    public override IdentityError ConcurrencyFailure() => Fehler(nameof(ConcurrencyFailure), "Das Konto wurde inzwischen geändert. Bitte neu laden.");
    /// <summary>Deutsche Meldung für <c>PasswordMismatch</c>: „Das Passwort ist falsch.“</summary>
    /// <returns>Fehler mit Code und deutschem Text.</returns>
    public override IdentityError PasswordMismatch() => Fehler(nameof(PasswordMismatch), "Das Passwort ist falsch.");
    /// <summary>Deutsche Meldung für <c>InvalidToken</c>: „Der Link ist ungültig oder abgelaufen.“</summary>
    /// <returns>Fehler mit Code und deutschem Text.</returns>
    public override IdentityError InvalidToken() => Fehler(nameof(InvalidToken), "Der Link ist ungültig oder abgelaufen.");
    /// <summary>Deutsche Meldung für <c>RecoveryCodeRedemptionFailed</c>: „Der Wiederherstellungscode ist ungültig.“</summary>
    /// <returns>Fehler mit Code und deutschem Text.</returns>
    public override IdentityError RecoveryCodeRedemptionFailed() => Fehler(nameof(RecoveryCodeRedemptionFailed), "Der Wiederherstellungscode ist ungültig.");
    /// <summary>Deutsche Meldung für <c>LoginAlreadyAssociated</c>: „Diese Anmeldung ist schon einem Konto zugeordnet.“</summary>
    /// <returns>Fehler mit Code und deutschem Text.</returns>
    public override IdentityError LoginAlreadyAssociated() => Fehler(nameof(LoginAlreadyAssociated), "Diese Anmeldung ist schon einem Konto zugeordnet.");
    /// <summary>Deutsche Meldung für <c>InvalidUserName</c>: „Der Benutzername „<paramref name="userName"/>“ ist ungültig.“</summary>
    /// <param name="userName">Der betroffene Benutzername.</param>
    /// <returns>Fehler mit Code und deutschem Text.</returns>
    public override IdentityError InvalidUserName(string? userName) => Fehler(nameof(InvalidUserName), $"Der Benutzername „{userName}“ ist ungültig.");
    /// <summary>Deutsche Meldung für <c>InvalidEmail</c>: „Die E-Mail-Adresse „<paramref name="email"/>“ ist ungültig.“</summary>
    /// <param name="email">Die betroffene E-Mail-Adresse.</param>
    /// <returns>Fehler mit Code und deutschem Text.</returns>
    public override IdentityError InvalidEmail(string? email) => Fehler(nameof(InvalidEmail), $"Die E-Mail-Adresse „{email}“ ist ungültig.");
    /// <summary>Deutsche Meldung für <c>DuplicateUserName</c>: „Es gibt schon ein Konto für „<paramref name="userName"/>“.“</summary>
    /// <param name="userName">Der betroffene Benutzername.</param>
    /// <returns>Fehler mit Code und deutschem Text.</returns>
    public override IdentityError DuplicateUserName(string userName) => Fehler(nameof(DuplicateUserName), $"Es gibt schon ein Konto für „{userName}“.");
    /// <summary>Deutsche Meldung für <c>DuplicateEmail</c>: „Es gibt schon ein Konto mit der E-Mail-Adresse „<paramref name="email"/>“.“</summary>
    /// <param name="email">Die betroffene E-Mail-Adresse.</param>
    /// <returns>Fehler mit Code und deutschem Text.</returns>
    public override IdentityError DuplicateEmail(string email) => Fehler(nameof(DuplicateEmail), $"Es gibt schon ein Konto mit der E-Mail-Adresse „{email}“.");
    /// <summary>Deutsche Meldung für <c>InvalidRoleName</c>: „Der Rollenname „<paramref name="role"/>“ ist ungültig.“</summary>
    /// <param name="role">Die betroffene Rolle.</param>
    /// <returns>Fehler mit Code und deutschem Text.</returns>
    public override IdentityError InvalidRoleName(string? role) => Fehler(nameof(InvalidRoleName), $"Der Rollenname „{role}“ ist ungültig.");
    /// <summary>Deutsche Meldung für <c>DuplicateRoleName</c>: „Die Rolle „<paramref name="role"/>“ gibt es schon.“</summary>
    /// <param name="role">Die betroffene Rolle.</param>
    /// <returns>Fehler mit Code und deutschem Text.</returns>
    public override IdentityError DuplicateRoleName(string role) => Fehler(nameof(DuplicateRoleName), $"Die Rolle „{role}“ gibt es schon.");
    /// <summary>Deutsche Meldung für <c>UserAlreadyHasPassword</c>: „Für dieses Konto ist schon ein Passwort gesetzt.“</summary>
    /// <returns>Fehler mit Code und deutschem Text.</returns>
    public override IdentityError UserAlreadyHasPassword() => Fehler(nameof(UserAlreadyHasPassword), "Für dieses Konto ist schon ein Passwort gesetzt.");
    /// <summary>Deutsche Meldung für <c>UserLockoutNotEnabled</c>: „Sperren ist für dieses Konto nicht möglich.“</summary>
    /// <returns>Fehler mit Code und deutschem Text.</returns>
    public override IdentityError UserLockoutNotEnabled() => Fehler(nameof(UserLockoutNotEnabled), "Sperren ist für dieses Konto nicht möglich.");
    /// <summary>Deutsche Meldung für <c>UserAlreadyInRole</c>: „Das Konto hat die Rolle „<paramref name="role"/>“ schon.“</summary>
    /// <param name="role">Die betroffene Rolle.</param>
    /// <returns>Fehler mit Code und deutschem Text.</returns>
    public override IdentityError UserAlreadyInRole(string role) => Fehler(nameof(UserAlreadyInRole), $"Das Konto hat die Rolle „{role}“ schon.");
    /// <summary>Deutsche Meldung für <c>UserNotInRole</c>: „Das Konto hat die Rolle „<paramref name="role"/>“ nicht.“</summary>
    /// <param name="role">Die betroffene Rolle.</param>
    /// <returns>Fehler mit Code und deutschem Text.</returns>
    public override IdentityError UserNotInRole(string role) => Fehler(nameof(UserNotInRole), $"Das Konto hat die Rolle „{role}“ nicht.");
    /// <summary>Deutsche Meldung für <c>PasswordTooShort</c>: „Das Passwort muss mindestens <paramref name="length"/> Zeichen lang sein.“</summary>
    /// <param name="length">Geforderte Mindestlänge.</param>
    /// <returns>Fehler mit Code und deutschem Text.</returns>
    public override IdentityError PasswordTooShort(int length) => Fehler(nameof(PasswordTooShort), $"Das Passwort muss mindestens {length} Zeichen lang sein.");
    /// <summary>Deutsche Meldung für <c>PasswordRequiresUniqueChars</c>: „Das Passwort muss mindestens <paramref name="uniqueChars"/> verschiedene Zeichen enthalten.“</summary>
    /// <param name="uniqueChars">Geforderte Zahl verschiedener Zeichen.</param>
    /// <returns>Fehler mit Code und deutschem Text.</returns>
    public override IdentityError PasswordRequiresUniqueChars(int uniqueChars) => Fehler(nameof(PasswordRequiresUniqueChars), $"Das Passwort muss mindestens {uniqueChars} verschiedene Zeichen enthalten.");
    /// <summary>Deutsche Meldung für <c>PasswordRequiresNonAlphanumeric</c>: „Das Passwort muss ein Sonderzeichen enthalten.“</summary>
    /// <returns>Fehler mit Code und deutschem Text.</returns>
    public override IdentityError PasswordRequiresNonAlphanumeric() => Fehler(nameof(PasswordRequiresNonAlphanumeric), "Das Passwort muss ein Sonderzeichen enthalten.");
    /// <summary>Deutsche Meldung für <c>PasswordRequiresDigit</c>: „Das Passwort muss eine Ziffer enthalten.“</summary>
    /// <returns>Fehler mit Code und deutschem Text.</returns>
    public override IdentityError PasswordRequiresDigit() => Fehler(nameof(PasswordRequiresDigit), "Das Passwort muss eine Ziffer enthalten.");
    /// <summary>Deutsche Meldung für <c>PasswordRequiresLower</c>: „Das Passwort muss einen Kleinbuchstaben enthalten.“</summary>
    /// <returns>Fehler mit Code und deutschem Text.</returns>
    public override IdentityError PasswordRequiresLower() => Fehler(nameof(PasswordRequiresLower), "Das Passwort muss einen Kleinbuchstaben enthalten.");
    /// <summary>Deutsche Meldung für <c>PasswordRequiresUpper</c>: „Das Passwort muss einen Großbuchstaben enthalten.“</summary>
    /// <returns>Fehler mit Code und deutschem Text.</returns>
    public override IdentityError PasswordRequiresUpper() => Fehler(nameof(PasswordRequiresUpper), "Das Passwort muss einen Großbuchstaben enthalten.");
}
