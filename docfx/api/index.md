# API-Dokumentation

Klassen und Member von FEVAD, erzeugt aus den deutschen XML-Kommentaren im Quelltext.

| Projekt | Inhalt |
|---|---|
| `Fevad.Domain` | Entitäten und Regeln der Fachdomäne, ohne Abhängigkeiten |
| `Fevad.Infrastructure` | EF Core, Identity, Rechte, Löschlauf, PDF, Mail, Import, Hintergrunddienste |
| `Fevad.Web` | Razor Pages (Seitenmodelle), HTMX-Partials, Schnittstellen (FE2, iCal) |

Die Migrationen unter `Fevad.Infrastructure.Persistenz.Migrationen` sind ausgeblendet.
