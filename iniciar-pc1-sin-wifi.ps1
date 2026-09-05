param(
    [switch]$Logs,
    [switch]$RecrearDatos,
    [switch]$ConfirmarBorradoDatos
)

$argumentos = @{
    Perfil = 'pc1'
    SoloLocal = $true
}
if ($Logs) { $argumentos.Logs = $true }
if ($RecrearDatos) { $argumentos.RecrearDatos = $true }
if ($ConfirmarBorradoDatos) { $argumentos.ConfirmarBorradoDatos = $true }

& (Join-Path $PSScriptRoot 'iniciar-pc.ps1') @argumentos
