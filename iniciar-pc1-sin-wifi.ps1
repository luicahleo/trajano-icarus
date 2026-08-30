param(
    [ValidateSet('dev', 'prod')]
    [string]$Modo = 'dev',
    [switch]$Logs,
    [switch]$RecrearDatos,
    [switch]$ConfirmarBorradoDatos
)

$argumentos = @{
    Perfil = 'pc1'
    Modo = $Modo
    SoloLocal = $true
}
if ($Logs) { $argumentos.Logs = $true }
if ($RecrearDatos) { $argumentos.RecrearDatos = $true }
if ($ConfirmarBorradoDatos) { $argumentos.ConfirmarBorradoDatos = $true }

& (Join-Path $PSScriptRoot 'iniciar-pc.ps1') @argumentos
