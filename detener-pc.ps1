param(
    [ValidateSet('pc1', 'pc2', 'pc3')]
    [string]$Perfil,
    [switch]$BorrarDatos,
    [switch]$ConfirmarBorradoDatos
)

$ErrorActionPreference = 'Stop'
$rutaIp = Join-Path $PSScriptRoot ".local/$Perfil/ip.txt"
$ipLan = if (Test-Path $rutaIp) { (Get-Content $rutaIp -Raw).Trim() } else { '127.0.0.1' }
$env:ICARUS_LAN_IP = $ipLan
$env:ICARUS_LAN_HOST = "$ipLan.sslip.io"
$archivosCompose = @(
    '-f', 'docker-compose.prodlocal.yml',
    '-f', "docker-compose.$Perfil.yml"
)

if ($BorrarDatos -and -not $ConfirmarBorradoDatos) {
    throw 'El borrado elimina la base y volúmenes locales. Repite con -BorrarDatos -ConfirmarBorradoDatos.'
}

$argumentos = @('down')
if ($BorrarDatos) { $argumentos += '--volumes' }

$preferenciaErrores = $ErrorActionPreference
try {
    $ErrorActionPreference = 'Continue'
    & docker compose @archivosCompose @argumentos
    $codigo = $LASTEXITCODE
}
finally {
    $ErrorActionPreference = $preferenciaErrores
}
if ($codigo -ne 0) { throw "No se pudo detener el entorno $Perfil." }

if ($BorrarDatos) {
    Write-Host 'Se eliminaron los volúmenes Docker locales; no son recuperables desde este script.' -ForegroundColor Yellow
}
