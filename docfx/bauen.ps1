# Baut FEVAD im Modus Release und erzeugt daraus die DocFX-Site (Handbuch aus docs/ und API aus den XML-Kommentaren).
# Aufruf aus dem Repository-Ordner:  pwsh docfx/bauen.ps1          (Site unter http://localhost:8090)
#                                    pwsh docfx/bauen.ps1 -NurBauen (nur nach _site schreiben)
# Gesperrte Skriptausführung unter Windows: powershell -ExecutionPolicy Bypass -File .\docfx\bauen.ps1
param([switch]$NurBauen)
$ErrorActionPreference = 'Stop'
Set-Location (Join-Path $PSScriptRoot '..')
dotnet tool restore
# Fevad.Web zieht Domain und Infrastructure mit; DocFX liest die drei DLLs samt XML-Datei aus bin/Release.
dotnet build src/Fevad.Web -c Release
if ($LASTEXITCODE -ne 0) { throw 'Build fehlgeschlagen.' }
if ($NurBauen) { dotnet docfx docfx/docfx.json --warningsAsErrors }
else { dotnet docfx docfx/docfx.json --warningsAsErrors --serve --port 8090 }
if ($LASTEXITCODE -ne 0) { throw 'DocFX meldet Warnungen oder Fehler.' }
