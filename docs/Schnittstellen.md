# Schnittstellen zu Drittsystemen (Konzept, V18)

Stand 26.10.2026. **Nur Konzept, nicht gebaut.** Der Einsatzbericht geht heute als PDF per Mail an die
Standardempfänger aus den Stammdaten (Stammdaten → Feuerwehr) oder wird als PDF heruntergeladen. Eine
maschinelle Schnittstelle lohnt erst, wenn ein Empfänger sie wirklich abnimmt (Kreisbrandmeister,
Gemeinde-SAP, Landesstatistik). Bis dahin bleibt es beim PDF.

## Was es schon gibt

| Richtung | Weg | Inhalt |
|---|---|---|
| alamos FE2 → FEVAD | Abruf über die FE2-API | Alarm als Entwurf des Einsatzberichts |
| FEVAD → FE2 | Personenabgleich (osFunctions) | Name, Qualifikationen als Funktionen |
| FEVAD → Kalender | iCal-Abo je Konto | Termine und Dienste |
| FEVAD → Gemeinde | PDF und CSV | Einsatzbericht, Kostenaufstellung, Kennzahlen, Entschädigung |

## Vorschlag, falls ein Empfänger eine Schnittstelle verlangt

1. **Abruf statt Versand (Export-API).** Lesender Endpunkt `GET /api/v1/einsaetze?seit=…` mit einem
   eigenen API-Schlüssel je Empfänger (in der Verwaltung angelegt, jederzeit widerrufbar, mit
   Ablaufdatum). Liefert nur freigegebene Berichte und nur die Felder, die der Empfänger braucht:
   - Standard: Nummer, Stichwort, Einsatzart, Ort ohne Hausnummer, Zeiten, Fahrzeuge, Zahl der Kräfte;
   - Teilnehmer und Beteiligte nur, wenn der Schlüssel dafür freigeschaltet ist (Zweck dokumentieren).
2. **Webhook nur als Hinweis.** Bei Freigabe ein `POST` an eine hinterlegte HTTPS-Adresse mit
   Einsatznummer und Link, ohne Inhalt; der Empfänger holt die Daten über die Export-API. So geht
   kein Personendatum an eine Adresse, die niemand mehr pflegt.
3. **Format.** JSON mit festem Schema und Versionsnummer; für die Statistik zusätzlich CSV.
4. **Nachweis.** Jeder Abruf wird wie der Mailversand vermerkt (wer, wann, welche Berichte).
5. **Datenschutz.** Jede neue Schnittstelle ist eine Übermittlung: Eintrag im Verzeichnis der
   Verarbeitungstätigkeiten, Rechtsgrundlage und Zweck je Empfänger, Freigabe durch DSB.

## Nicht vorgesehen

- Alarmierung oder Rückmeldung an die Leitstelle (Scope: Verwaltung und Dokumentation).
- Schreibender Zugriff von außen.
- Export nach MP-FEUER (F28: nicht möglich).
