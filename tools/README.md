# Werkzeuge

Hier liegt das Tailwind-CSS-Standalone-Binary (wird nicht eingecheckt). Ist es vorhanden, erzeugt
der Build von `Fevad.Web` automatisch `wwwroot/css/app.css` aus `Styles/app.css`.

Einmalig holen:

- Windows (PowerShell): `./tools/tailwind-holen.ps1`
- Linux/macOS: `./tools/tailwind-holen.sh`

Ohne Binary nutzt der Build die zuletzt eingecheckte `app.css`. Nach Änderungen an Klassen in den
Seiten deshalb einmal mit Binary bauen und die neue `app.css` mit einchecken.
