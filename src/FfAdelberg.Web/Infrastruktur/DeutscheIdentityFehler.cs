using Microsoft.AspNetCore.Identity;

namespace FfAdelberg.Web.Infrastruktur;

/// <summary>Fehlermeldungen von Identity auf Deutsch.</summary>
public sealed class DeutscheIdentityFehler : IdentityErrorDescriber
{
    private static IdentityError Fehler(string code, string text) => new() { Code = code, Description = text };

    public override IdentityError DefaultError() => Fehler(nameof(DefaultError), "Unbekannter Fehler.");
    public override IdentityError ConcurrencyFailure() => Fehler(nameof(ConcurrencyFailure), "Das Konto wurde inzwischen geändert. Bitte neu laden.");
    public override IdentityError PasswordMismatch() => Fehler(nameof(PasswordMismatch), "Das Passwort ist falsch.");
    public override IdentityError InvalidToken() => Fehler(nameof(InvalidToken), "Der Link ist ungültig oder abgelaufen.");
    public override IdentityError RecoveryCodeRedemptionFailed() => Fehler(nameof(RecoveryCodeRedemptionFailed), "Der Wiederherstellungscode ist ungültig.");
    public override IdentityError LoginAlreadyAssociated() => Fehler(nameof(LoginAlreadyAssociated), "Diese Anmeldung ist schon einem Konto zugeordnet.");
    public override IdentityError InvalidUserName(string? userName) => Fehler(nameof(InvalidUserName), $"Der Benutzername „{userName}“ ist ungültig.");
    public override IdentityError InvalidEmail(string? email) => Fehler(nameof(InvalidEmail), $"Die E-Mail-Adresse „{email}“ ist ungültig.");
    public override IdentityError DuplicateUserName(string userName) => Fehler(nameof(DuplicateUserName), $"Es gibt schon ein Konto für „{userName}“.");
    public override IdentityError DuplicateEmail(string email) => Fehler(nameof(DuplicateEmail), $"Es gibt schon ein Konto mit der E-Mail-Adresse „{email}“.");
    public override IdentityError InvalidRoleName(string? role) => Fehler(nameof(InvalidRoleName), $"Der Rollenname „{role}“ ist ungültig.");
    public override IdentityError DuplicateRoleName(string role) => Fehler(nameof(DuplicateRoleName), $"Die Rolle „{role}“ gibt es schon.");
    public override IdentityError UserAlreadyHasPassword() => Fehler(nameof(UserAlreadyHasPassword), "Für dieses Konto ist schon ein Passwort gesetzt.");
    public override IdentityError UserLockoutNotEnabled() => Fehler(nameof(UserLockoutNotEnabled), "Sperren ist für dieses Konto nicht möglich.");
    public override IdentityError UserAlreadyInRole(string role) => Fehler(nameof(UserAlreadyInRole), $"Das Konto hat die Rolle „{role}“ schon.");
    public override IdentityError UserNotInRole(string role) => Fehler(nameof(UserNotInRole), $"Das Konto hat die Rolle „{role}“ nicht.");
    public override IdentityError PasswordTooShort(int length) => Fehler(nameof(PasswordTooShort), $"Das Passwort muss mindestens {length} Zeichen lang sein.");
    public override IdentityError PasswordRequiresUniqueChars(int uniqueChars) => Fehler(nameof(PasswordRequiresUniqueChars), $"Das Passwort muss mindestens {uniqueChars} verschiedene Zeichen enthalten.");
    public override IdentityError PasswordRequiresNonAlphanumeric() => Fehler(nameof(PasswordRequiresNonAlphanumeric), "Das Passwort muss ein Sonderzeichen enthalten.");
    public override IdentityError PasswordRequiresDigit() => Fehler(nameof(PasswordRequiresDigit), "Das Passwort muss eine Ziffer enthalten.");
    public override IdentityError PasswordRequiresLower() => Fehler(nameof(PasswordRequiresLower), "Das Passwort muss einen Kleinbuchstaben enthalten.");
    public override IdentityError PasswordRequiresUpper() => Fehler(nameof(PasswordRequiresUpper), "Das Passwort muss einen Großbuchstaben enthalten.");
}
