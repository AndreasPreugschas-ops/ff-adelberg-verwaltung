# Dokumentationsrichtlinie

Gilt für allen C#-Code in FEVAD (Regel Andi, 25.10.2026, bestätigt 02.11.2026; Vorbild ist die Richtlinie im
ATLAS-Repository). Ziel: Wer eine Klasse oder Methode öffnet, versteht ohne Rückfrage, **was** sie fachlich tut und
**warum**.

## Was wird kommentiert

- **Alles**, was neu entsteht oder geändert wird, auch `private` und `internal`: Typen, Methoden, Eigenschaften,
  Felder, Konstanten, Konstruktoren, Records samt Parametern.
- Ausgenommen sind nur Testprojekte (`*.Tests`) und von EF Core erzeugte Migrationen (dort genügt der Kommentar an
  der Migrationsklasse; in der API-Dokumentation sind Migrationen ausgeblendet).
- Sprache Deutsch, Fachbegriffe der Feuerwehr (Mitglied, Einsatz, Prüfung). Umlaute in Kommentaren echt, im
  Bezeichner ausgeschrieben (`ae`, `oe`, `ue`, `ss`). Zeiten im 24-Stunden-Format, Datum TT.MM.JJJJ.
- Nie „Hauptamt“ für die Stelle im Rathaus, sondern „Gemeindeverwaltung“ (Ausnahme: der technische Rollenname).

## Wie wird kommentiert

1. `<summary>` in einem Satz: was es ist, nicht wie es gebaut ist. Keine Wiederholung des Namens („Setzt den Namen“).
2. `<param>` und `<returns>`, sobald der Name nicht alles sagt (Einheit, erlaubte Werte, Bedeutung von `null`).
3. `<remarks>` für Regeln und Begründungen: Rechtsquelle (FwG BW, Satzung, DGUV), Datenschutzgrund,
   Vorschlags- oder Löschregelnummer (V…, L…), Randfälle.
4. `<exception>` für bewusst geworfene Ausnahmen, `<see cref="…"/>` für Verweise. Für Überladungsgruppen fremder
   Bibliotheken keinen `cref` mit `O:` verwenden (DocFX kann ihn nicht auflösen), sondern `<c>SaveChanges</c>`.
5. Fristen, Sätze und Grenzwerte nie fest im Code: `Parameter` verwenden und im Kommentar den Schlüssel nennen.

```csharp
/// <summary>Berechnet den Atemschutzstatus eines Mitglieds aus Vorsorge, Belastungsübung und Unterweisung.</summary>
/// <param name="heute">Stichtag in Europe/Berlin.</param>
/// <returns>Status mit der nächsten fälligen Frist; nie <c>null</c>.</returns>
/// <remarks>Nur Status und Frist, nie Befunde (Art. 9 DSGVO). Warnung ab Monat <c>Atemschutz.Jahrespflicht.WarnungAbMonat</c>.</remarks>
```

### Razor Pages

- Das Seitenmodell (`*.cshtml.cs`) wird wie jede Klasse kommentiert: Zweck der Seite, nötiges Recht, Handler.
- In der `.cshtml` genügt ein Kopfkommentar `@* … *@`, wenn die Seite nicht selbsterklärend ist.

## Prüfung im Build

- `Directory.Build.props` erzeugt in allen Projekten außer `*.Tests` die XML-Datei. Fehlende Kommentare an
  öffentlichen Membern (CS1591) brechen wegen `TreatWarningsAsErrors` den Build.
- Nicht öffentliche Member prüft der Compiler nicht; dort gilt die Richtlinie per Review.
- Änderung an bestehendem Code: Kommentar im selben Zug anpassen; veraltete Kommentare sind Fehler.

## DocFX

```powershell
pwsh docfx/bauen.ps1            # Release-Build, dann Site unter http://localhost:8090
pwsh docfx/bauen.ps1 -NurBauen  # nur nach _site/ schreiben
```

Unter Linux ohne PowerShell: `dotnet tool restore && dotnet build src/Fevad.Web -c Release && dotnet docfx docfx/docfx.json --warningsAsErrors`.

- DocFX liest die übersetzten Bibliotheken (`src/*/bin/Release/net10.0`); ohne Release-Build bleibt die API leer.
- Handbuch: alle Dateien in `docs/`, Reihenfolge im Inhaltsverzeichnis `docs/toc.yml`. Ein neuer Artikel braucht
  einen Eintrag dort, sonst fehlt er in der Navigation.
- Ausgeblendet sind die Migrationen (`docfx/filter.yml`).
- Die Site liegt in `_site/` und wird nicht eingecheckt. Die CI baut sie bei jedem Lauf (Job „Dokumentation“) mit
  `--warningsAsErrors`; ungültige Verweise oder Links brechen den Lauf. Ergebnis als Artefakt `fevad-dokumentation`.
