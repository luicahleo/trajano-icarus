param(
    [switch]$Logs,
    [switch]$RecrearDatos,
    [switch]$ConfirmarBorradoDatos,
    [switch]$ConArgos
)

$argumentos = @{
    Perfil = 'pc1'
    SoloLocal = $true
}
if ($Logs) { $argumentos.Logs = $true }
if ($RecrearDatos) { $argumentos.RecrearDatos = $true }
if ($ConfirmarBorradoDatos) { $argumentos.ConfirmarBorradoDatos = $true }
if ($ConArgos) {
    # ARGOS carga el modelo ArcFace en memoria al arrancar; "web" espera a que
    # "argos" pase su healthcheck, así que el sondeo de salud necesita más
    # margen que el resto del stack.
    $argumentos.ComposeExtra = @('docker-compose.argos.yml')
    $argumentos.EsperaSaludSegundosExtra = 90
    $argumentos.ServiciosLogsExtra = @('argos')
}

& (Join-Path $PSScriptRoot 'iniciar-pc.ps1') @argumentos
if ($ConArgos) {
    Write-Host 'ARGOS local activo: ArgosControlAcceso apunta al contenedor interno argos (clave de desarrollo, no la de producción).' -ForegroundColor Yellow
}
