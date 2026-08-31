param(
    [ValidateSet('pc1', 'pc2', 'pc3')]
    [string]$Perfil,
    [string]$Ip,
    [string]$SsidMobil,
    [switch]$SoloLocal,
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
    if ($SoloLocal) { return '127.0.0.1' }

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

# Modo prod: arma el artefacto de producción en .local/payload (API publicada
# en Release + PWA compilada en wwwroot + Dockerfile de producción), igual que
# el pipeline de despliegue. Requiere el SDK de .NET y Node en el host.
function Construir-ContenidoProduccion {
    $payload = Join-Path $PSScriptRoot '.local/payload'
    if (Test-Path $payload) { Remove-Item -Recurse -Force $payload }
    New-Item -ItemType Directory -Force (Join-Path $payload 'web/wwwroot') | Out-Null

    # npm (y a veces dotnet) escribe avisos por stderr; PowerShell 5.1 los
    # convierte en NativeCommandError cuando la preferencia global es Stop.
    $preferenciaErrores = $ErrorActionPreference
    try {
        $ErrorActionPreference = 'Continue'
        & dotnet publish (Join-Path $PSScriptRoot 'Icarus/src/Host/Icarus.Host/Icarus.Host.csproj') -c Release -o (Join-Path $payload 'web') --nologo
        $codigoPublish = $LASTEXITCODE

        Push-Location (Join-Path $PSScriptRoot 'web')
        try {
            & npm ci --no-audit --no-fund
            $codigoNpmCi = $LASTEXITCODE
            & npm run build
            $codigoBuild = $LASTEXITCODE
        }
        finally {
            Pop-Location
        }
    }
    finally {
        $ErrorActionPreference = $preferenciaErrores
    }
    if ($codigoPublish -ne 0) { throw 'No se pudo publicar la API (modo producción).' }
    if ($codigoNpmCi -ne 0) { throw 'No se pudieron instalar las dependencias web (modo producción).' }
    if ($codigoBuild -ne 0) { throw 'No se pudo compilar la PWA (modo producción).' }

    Copy-Item -Recurse -Force (Join-Path $PSScriptRoot 'web/dist/*') (Join-Path $payload 'web/wwwroot')
    Copy-Item -Force (Join-Path $PSScriptRoot 'Dockerfile.web') (Join-Path $payload 'Dockerfile.web')
    Copy-Item -Force (Join-Path $PSScriptRoot 'deploy/.dockerignore') (Join-Path $payload '.dockerignore')
    Write-Host "Contenido de producción listo en $payload" -ForegroundColor Cyan
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
$hostLan = if ($SoloLocal) { 'localhost' } else { "$ipLan.sslip.io" }
$env:ICARUS_LAN_IP = $ipLan
$env:ICARUS_LAN_HOST = $hostLan
New-Item -ItemType Directory -Force (Join-Path $raiz ".local/$Perfil/caddy-data") | Out-Null
New-Item -ItemType Directory -Force (Join-Path $raiz ".local/$Perfil/caddy-config") | Out-Null
Set-Content -Path (Join-Path $raiz ".local/$Perfil/ip.txt") -Value $ipLan -Encoding ascii

# Un solo entorno local: el artefacto de producción (API + PWA en un contenedor,
# como en la VPS) con SQL y Seq locales. No hay modo dev para el stack PC: los
# cambios se prueban como se desplegarían, sin esperar el deploy.
$archivosCompose = @(
    '-f', 'docker-compose.prodlocal.yml',
    '-f', "docker-compose.$Perfil.yml"
)
Construir-ContenidoProduccion

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

# En PC1/PC2/PC3 la API no tiene bind mount: sin esta reconstrucción Docker
# puede reutilizar una imagen previa aunque el usuario haya cambiado C#.
# Se prioriza ejecutar el código actual sobre el tiempo de arranque.
$serviciosBuild = @('web')
try {
    $ErrorActionPreference = 'Continue'
    & docker compose @archivosCompose build --no-cache @serviciosBuild
    $codigoBuild = $LASTEXITCODE
}
finally {
    $ErrorActionPreference = $preferenciaErrores
}
if ($codigoBuild -ne 0) { throw "No se pudieron reconstruir las imágenes para $nombreEquipo." }

try {
    $ErrorActionPreference = 'Continue'
    & docker compose @archivosCompose up -d --build --force-recreate --renew-anon-volumes --remove-orphans
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
        $serviciosLogs = @('gateway', 'web')
        & docker compose @archivosCompose logs --tail 50 @serviciosLogs
    }
    finally {
        $ErrorActionPreference = $preferenciaErrores
    }
    throw 'El gateway HTTPS no alcanzó un estado saludable.'
}

Write-Host ''
Write-Host "Icarus ${nombreEquipo}: https://$hostLan" -ForegroundColor Green
Write-Host "Seq local (solo desarrollo): http://localhost:5341" -ForegroundColor Green
if (Test-Path $certificado) {
    Write-Host "CA pública para instalar en el móvil: $certificado" -ForegroundColor Yellow
} else {
    Write-Warning 'Caddy aún no creó la CA. Revisa: docker compose ... logs gateway'
}
if ($SoloLocal) {
    Write-Host 'Modo sin WiFi: el entorno solo se anuncia en este equipo.'
} else {
    Write-Host 'Si el móvil no conecta, permite los puertos TCP 80 y 443 para redes privadas en Firewall de Windows.'
}

if ($Logs) {
    try {
        $ErrorActionPreference = 'Continue'
        & docker compose @archivosCompose logs -f
    }
    finally {
        $ErrorActionPreference = $preferenciaErrores
    }
}
