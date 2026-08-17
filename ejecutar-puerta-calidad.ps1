[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$raiz = Split-Path -Parent $PSCommandPath
$directorioLogs = Join-Path $raiz 'logs'
New-Item -ItemType Directory -Path $directorioLogs -Force | Out-Null

$marca = Get-Date -Format 'yyyyMMdd-HHmmss'
$log = Join-Path $directorioLogs "puerta-calidad-$marca.log"

Set-Location $raiz
Write-Host "Ejecutando la puerta de calidad. Registro: $log"

& node .\quality\verify.mjs 2>&1 | Tee-Object -FilePath $log
$codigo = $LASTEXITCODE

if ($codigo -eq 0) {
    Write-Host "Puerta de calidad verde. Registro: $log" -ForegroundColor Green
}
else {
    Write-Host "La puerta de calidad falló (código $codigo). Registro: $log" -ForegroundColor Red
}

exit $codigo
