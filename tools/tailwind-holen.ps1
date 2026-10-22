# Lädt das Tailwind-CSS-Standalone-Binary für Windows nach tools/tailwindcss.exe.
$version = "v4.1.14"
$ziel = Join-Path $PSScriptRoot "tailwindcss.exe"
Invoke-WebRequest -Uri "https://github.com/tailwindlabs/tailwindcss/releases/download/$version/tailwindcss-windows-x64.exe" -OutFile $ziel
Write-Host "Tailwind $version liegt unter $ziel"
