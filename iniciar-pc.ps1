param(
    [ValidateSet('pc1', 'pc2', 'pc3')]
    [string]$Perfil,
    [string]$Ip,
    [string]$SsidMobil,
    [switch]$Logs,
    [switch]$RecrearDatos,
    [switch]$ConfirmarBorradoDatos
)

$ErrorActionPreference = 'Stop'
$raiz = $PSScriptRoot
$nombreEquipo = $Perfil.ToUpperInvariant()

if ($RecrearDatos -and -not $ConfirmarBorradoDatos) {
    throw 'La recreación elimina la base y volúmenes locales. Repite con -RecrearDatos -ConfirmarBorradoDatos.'
}

function Obtener-IpWifi([string]$Ssid) {
    # netsh wlan reporta el SSID real de la conexión; Get-NetConnectionProfile
    # puede devolver el nombre de red (p. ej. el dominio) en su lugar.
    $salidaNetsh = $null
    try {
        $ErrorActionPreference = 'Continue'
        $salidaNetsh = & netsh wlan show interfaces 2>$null
    }
    finally {
        $ErrorActionPreference = $preferenciaErrores
    }
    $ssidConectado = $salidaNetsh | Where-Object { $_ -match '^\s*SSID\s*:\s*(.+)$' -and $Matches[1].Trim() -eq $Ssid }
    if (-not $ssidConectado) { return $null }

    $indicesWifi = @(Get-NetAdapter -ErrorAction SilentlyContinue |
        Where-Object { $_.PhysicalMediaType -eq 'Native 802.11' } |
        Select-Object -ExpandProperty InterfaceIndex)
    foreach ($indice in $indicesWifi) {
        $direccion = Get-NetIPAddress -InterfaceIndex $indice -AddressFamily IPv4 -ErrorAction SilentlyContinue |
            Where-Object { $_.IPAddress -notlike '169.254.*' } |
            Select-Object -First 1
        if ($direccion) { return $direccion.IPAddress }
    }
    $perfil = Get-NetConnectionProfile -ErrorAction SilentlyContinue |
        Where-Object { $_.Name -eq $Ssid } |
        Select-Object -First 1
    if ($perfil) {
        $direccion = Get-NetIPAddress -InterfaceIndex $perfil.InterfaceIndex -AddressFamily IPv4 -ErrorAction SilentlyContinue |
            Where-Object { $_.IPAddress -notlike '169.254.*' } |
            Select-Object -First 1
        if ($direccion) { return $direccion.IPAddress }
    }
    throw "El WiFi '$Ssid' está conectado pero no tiene una IPv4 LAN asignada."
}

function Obtener-IpLan([string]$IpSolicitada) {
    if ($IpSolicitada) {
        $direccion = $null
        if (-not [System.Net.IPAddress]::TryParse($IpSolicitada, [ref]$direccion) -or
            $direccion.AddressFamily -ne [System.Net.Sockets.AddressFamily]::InterNetwork -or
            [System.Net.IPAddress]::IsLoopback($direccion)) {
            throw 'La IP indicada no es una IPv4 LAN válida.'
        }
        return $direccion.IPAddressToString
    }

    if ($SsidMobil) {
        $ipWifi = Obtener-IpWifi $SsidMobil
        if ($ipWifi) { return $ipWifi }
        throw "No se encontró el WiFi '$SsidMobil' conectado. Conéctate a ese SSID para exponer el contenedor a los móviles."
    }

    $ruta = Get-NetRoute -DestinationPrefix '0.0.0.0/0' -ErrorAction Stop |
        Where-Object { $_.NextHop -ne '0.0.0.0' } |
        Sort-Object RouteMetric, InterfaceMetric |
        Select-Object -First 1
    if (-not $ruta) { throw 'No se encontró una ruta de red activa. Usa -Ip <IPv4-WiFi>.' }
    $direccion = Get-NetIPAddress -InterfaceIndex $ruta.InterfaceIndex -AddressFamily IPv4 |
        Where-Object { $_.IPAddress -notlike '169.254.*' } |
        Select-Object -First 1
    if (-not $direccion) { throw 'No se encontró una IPv4 LAN. Usa -Ip <IPv4-WiFi>.' }
    return $direccion.IPAddress
}

$preferenciaErrores = $ErrorActionPreference
try {
    # Windows PowerShell convierte las advertencias de stderr de Docker en
    # NativeCommandError cuando la preferencia global es Stop.
    $ErrorActionPreference = 'Continue'
    docker info *> $null
    $codigoDocker = $LASTEXITCODE
}
finally {
    $ErrorActionPreference = $preferenciaErrores
}
if ($codigoDocker -ne 0) { throw 'Docker Desktop no está iniciado o no responde.' }

$ipLan = Obtener-IpLan $Ip
$hostLan = "$ipLan.sslip.io"
$env:ICARUS_LAN_IP = $ipLan
$env:ICARUS_LAN_HOST = $hostLan
New-Item -ItemType Directory -Force (Join-Path $raiz ".local/$Perfil/caddy-data") | Out-Null
New-Item -ItemType Directory -Force (Join-Path $raiz ".local/$Perfil/caddy-config") | Out-Null
Set-Content -Path (Join-Path $raiz ".local/$Perfil/ip.txt") -Value $ipLan -Encoding ascii

$archivosCompose = @(
    '-f', 'docker-compose.dev.yml',
    '-f', "docker-compose.$Perfil.yml"
)

if ($RecrearDatos) {
    try {
        $ErrorActionPreference = 'Continue'
        & docker compose @archivosCompose down --volumes
        $codigoDown = $LASTEXITCODE
    }
    finally {
        $ErrorActionPreference = $preferenciaErrores
    }
    if ($codigoDown -ne 0) { throw "No se pudieron eliminar los datos locales de $nombreEquipo." }
    Write-Host 'Se eliminaron los volúmenes Docker locales; se recrearán al iniciar.' -ForegroundColor Yellow
}

try {
    $ErrorActionPreference = 'Continue'
    & docker compose @archivosCompose up -d --build --renew-anon-volumes
    $codigoUp = $LASTEXITCODE
}
finally {
    $ErrorActionPreference = $preferenciaErrores
}
if ($codigoUp -ne 0) { throw "No se pudo levantar el entorno $nombreEquipo." }

$certificado = Join-Path $raiz ".local/$Perfil/caddy-data/caddy/pki/authorities/local/root.crt"
for ($intento = 0; $intento -lt 30 -and -not (Test-Path $certificado); $intento++) {
    Start-Sleep -Seconds 1
}

$urlSalud = "https://$hostLan/api/health"
$saludable = $false
for ($intento = 0; $intento -lt 30 -and -not $saludable; $intento++) {
    try {
        # Un handshake puede fallar mientras Caddy arranca. curl lo informa por
        # stderr, pero aquí debe provocar un reintento, no detener el script.
        $ErrorActionPreference = 'Continue'
        & curl.exe -k -f -sS --max-time 5 --resolve "${hostLan}:443:$ipLan" $urlSalud -o NUL 2>$null
        $codigoSalud = $LASTEXITCODE
    }
    finally {
        $ErrorActionPreference = $preferenciaErrores
    }
    $saludable = $codigoSalud -eq 0
    if (-not $saludable) { Start-Sleep -Seconds 1 }
}
if (-not $saludable) {
    try {
        $ErrorActionPreference = 'Continue'
        & docker compose @archivosCompose logs --tail 50 gateway web api
    }
    finally {
        $ErrorActionPreference = $preferenciaErrores
    }
    throw 'El gateway HTTPS no alcanzó un estado saludable.'
}

Write-Host ''
Write-Host "Icarus ${nombreEquipo}: https://$hostLan" -ForegroundColor Green
if (Test-Path $certificado) {
    Write-Host "CA pública para instalar en el móvil: $certificado" -ForegroundColor Yellow
} else {
    Write-Warning 'Caddy aún no creó la CA. Revisa: docker compose ... logs gateway'
}
Write-Host 'Si el móvil no conecta, permite los puertos TCP 80 y 443 para redes privadas en Firewall de Windows.'

if ($Logs) {
    try {
        $ErrorActionPreference = 'Continue'
        & docker compose @archivosCompose logs -f
    }
    finally {
        $ErrorActionPreference = $preferenciaErrores
    }
}
