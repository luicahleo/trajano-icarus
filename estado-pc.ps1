param(
    [ValidateSet('pc1', 'pc2', 'pc3')]
    [string]$Perfil,
    [switch]$Logs
)

$ErrorActionPreference = 'Stop'
$rutaIp = Join-Path $PSScriptRoot ".local/$Perfil/ip.txt"
$ipLan = if (Test-Path $rutaIp) { (Get-Content $rutaIp -Raw).Trim() } else { '127.0.0.1' }
$env:ICARUS_LAN_IP = $ipLan
$env:ICARUS_LAN_HOST = "$ipLan.sslip.io"
$archivosCompose = @('-f', 'docker-compose.dev.yml', '-f', "docker-compose.$Perfil.yml")

$preferenciaErrores = $ErrorActionPreference
try {
    $ErrorActionPreference = 'Continue'
    & docker compose @archivosCompose ps
    $codigo = $LASTEXITCODE
}
finally {
    $ErrorActionPreference = $preferenciaErrores
}
if ($codigo -ne 0) { throw "No se pudo consultar el entorno $Perfil." }

if ($Logs) {
    try {
        $ErrorActionPreference = 'Continue'
        & docker compose @archivosCompose logs --tail 100
    }
    finally {
        $ErrorActionPreference = $preferenciaErrores
    }
}
