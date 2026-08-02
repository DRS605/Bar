<#
    Arranca ALXOR Core SIN Docker en Windows.

    Requisitos (se instalan una sola vez, sin virtualizacion ni reiniciar):
      1) .NET 8 SDK       ->  winget install --id Microsoft.DotNet.SDK.8 -e
      2) PostgreSQL 16    ->  winget install --id PostgreSQL.PostgreSQL.16 -e `
                                --custom "--mode unattended --unattendedmodeui none --superpassword postgres --serverport 5432 --disable-stackbuilder 1"

    Uso (en PowerShell, dentro de la carpeta del proyecto):
        .\scripts\arrancar.ps1                 # ERP en http://localhost:3400
        .\scripts\arrancar.ps1 -Puerto 8080    # otro puerto

    La primera vez tarda un poco (descarga paquetes y compila). La base de datos
    se crea sola. Para parar el ERP: Ctrl+C en esta ventana.
#>
param([int]$Puerto = 3400)

$ErrorActionPreference = "Stop"

# Ir a la raiz del repo (carpeta padre de \scripts).
$raiz = Split-Path -Parent $PSScriptRoot
Set-Location $raiz

Write-Host "ALXOR Core - arranque sin Docker`n"

# 1) .NET 8.
if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
    Write-Host "Falta .NET 8. Instalalo con este comando y vuelve a intentarlo:" -ForegroundColor Yellow
    Write-Host "    winget install --id Microsoft.DotNet.SDK.8 -e"
    Write-Host "(cierra y abre PowerShell despues de instalar)"
    exit 1
}

# 2) PostgreSQL: servicio instalado y en marcha.
$svc = Get-Service -Name "postgresql*" -ErrorAction SilentlyContinue | Select-Object -First 1
if ($null -eq $svc) {
    Write-Host "No encuentro PostgreSQL. Instalalo con este comando y vuelve a intentarlo:" -ForegroundColor Yellow
    Write-Host '    winget install --id PostgreSQL.PostgreSQL.16 -e --custom "--mode unattended --unattendedmodeui none --superpassword postgres --serverport 5432 --disable-stackbuilder 1"'
    Write-Host "Si aparece un asistente en vez de instalarse solo, pon la contrasena 'postgres' y el puerto 5432."
    exit 1
}
if ($svc.Status -ne "Running") {
    Write-Host "Arrancando PostgreSQL ($($svc.Name))..."
    Start-Service $svc.Name
}
Write-Host "PostgreSQL en marcha ($($svc.Name))."

# 3) Configuracion y arranque.
$env:ASPNETCORE_ENVIRONMENT = "Development"
$env:ASPNETCORE_URLS = "http://localhost:$Puerto"
$env:ConnectionStrings__AlxorCore = "Host=localhost;Port=5432;Database=alxor;Username=postgres;Password=postgres"
$env:Jwt__ClaveSecreta = "clave-de-desarrollo-cambiar-en-produccion-32+"

Write-Host "`nArrancando el ERP en http://localhost:$Puerto  (Ctrl+C para parar)`n" -ForegroundColor Green
dotnet run --project src/AlxorCore.Api --no-launch-profile
