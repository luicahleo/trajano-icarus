# Alta de una cuenta CAISY con la funcionalidad GestorRecepcionHuevos (spec
# SP9) en cualquier entorno de Trajano-Icarus: stack PC local, VPS, etc.
#
# GestorRecepcionHuevos es la función de oficina que publica precios de
# huevo y confirma la recepción de despachos desde Trajano.GestorCaisy
# (`/PreciosHuevo` y `/RecepcionesHuevo`); es independiente de
# GestorPedidoAlimento, que gestiona alimento. Las cuentas CAISY solo las
# crea el Administrador de plataforma y nunca desde la aplicación de oficina.
# Este script hace el login del admin, llama a POST /api/usuarios-caisy y
# reporta el resultado. No guarda credenciales: las pide de forma
# interactiva si no llegan por parámetro.
#
# Uso:
#   .\crear-usuario-gestor-recepcion-huevos.ps1 -BaseUrl https://localhost -Email recepcion@caisy.test -Inseguro
#   .\crear-usuario-gestor-recepcion-huevos.ps1 -BaseUrl https://icarusv2.trajano.online -Email recepcion@caisy.com
#
# Para una cuenta con ambas funciones (pedidos de alimento y recepción de
# huevo), pasar -Funcionalidades GestorPedidoAlimento,GestorRecepcionHuevos.
#
# -Inseguro acepta el certificado autofirmado de Caddy del stack PC local; no
# lo uses contra la VPS (allí el certificado es válido).

# Usar este comando, hay que cambiar la ip segun la pc  .\crear-usuario-gestor-recepcion-huevos.ps1 -BaseUrl "https://192.168.1.106.sslip.io" -Email "grh@icarus.test" -Inseguro
[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$BaseUrl,
    [Parameter(Mandatory)][string]$Email,
    [string]$AdminEmail = 'admin@icarus.test',
    [string[]]$Funcionalidades = @('GestorRecepcionHuevos'),
    # Opcionales para automatización; si faltan se piden de forma interactiva.
    [SecureString]$ClaveAdmin,
    [SecureString]$ClaveCaisy,
    [switch]$Inseguro
)

$ErrorActionPreference = 'Stop'

function Convertir-SecureString([System.Security.SecureString]$segura) {
    $bstr = [Runtime.InteropServices.Marshal]::SecureStringToBSTR($segura)
    try { [Runtime.InteropServices.Marshal]::PtrToStringBSTR($bstr) }
    finally { [Runtime.InteropServices.Marshal]::ZeroFreeBSTR($bstr) }
}

if ($Inseguro) {
    # Stack PC: certificado interno de Caddy. Solo PowerShell 5.1 lo necesita;
    # en PS 7+ Invoke-RestMethod tiene -SkipCertificateCheck.
    if ($PSVersionTable.PSVersion.Major -ge 7) {
        $saltoCertificado = @{ SkipCertificateCheck = $true }
    } else {
        [System.Net.ServicePointManager]::ServerCertificateValidationCallback = { $true }
        # PS 5.1 negocia TLS 1.0 por defecto y Caddy exige TLS 1.2+.
        [System.Net.ServicePointManager]::SecurityProtocol = [System.Net.SecurityProtocolType]::Tls12
        $saltoCertificado = @{}
    }
} else {
    $saltoCertificado = @{}
}

$base = $BaseUrl.TrimEnd('/')
if (-not $ClaveAdmin) { $ClaveAdmin = Read-Host "Contraseña de $AdminEmail" -AsSecureString }
if (-not $ClaveCaisy) { $ClaveCaisy = Read-Host "Contraseña para la cuenta $Email" -AsSecureString }
# OJO: PowerShell no distingue mayúsculas: un local $claveAdmin sería la MISMA
# variable tipada que el parámetro $ClaveAdmin y la asignación reventaría.
$textoAdmin = Convertir-SecureString $ClaveAdmin
$textoCaisy = Convertir-SecureString $ClaveCaisy

# 1. Sesión del Administrador de plataforma.
try {
    $sesion = Invoke-RestMethod -Method Post -Uri "$base/api/identidad/sesion" @saltoCertificado `
        -ContentType 'application/json; charset=utf-8' `
        -Body (@{ email = $AdminEmail; contrasena = $textoAdmin } | ConvertTo-Json)
} catch {
    throw "No se pudo autenticar el administrador ($($_.Exception.Message))."
}

# 2. Alta de la cuenta CAISY con sus funcionalidades.
$cuerpo = @{ email = $Email; contrasena = $textoCaisy; funcionalidades = $Funcionalidades } | ConvertTo-Json
try {
    $creada = Invoke-RestMethod -Method Post -Uri "$base/api/usuarios-caisy" @saltoCertificado `
        -ContentType 'application/json; charset=utf-8' `
        -Headers @{ Authorization = "Bearer $($sesion.accessToken)" } `
        -Body $cuerpo
    Write-Host "Cuenta de recepción de huevo creada: $Email (id $($creada.id)) en $base" -ForegroundColor Green
} catch {
    $respuesta = $_.Exception.Response
    if ($respuesta -and [int]$respuesta.StatusCode -eq 409) {
        Write-Host "La cuenta $Email ya existe en $base; no se modificó nada." -ForegroundColor Yellow
        Write-Host 'Para cambiar sus funcionalidades: PUT /api/usuarios-caisy/{id}/funcionalidades.'
    } else {
        throw "No se pudo crear la cuenta CAISY ($($_.Exception.Message))."
    }
}
