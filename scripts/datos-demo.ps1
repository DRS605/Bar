<#
    Datos de demostracion para ALXOR Core (version PowerShell, para Windows).

    Rellena una instancia recien arrancada con una empresa, clientes, articulos,
    facturas repartidas por el anio, cobros (alguno parcial), gastos y una factura
    recurrente, para que el panel y los informes tengan contenido nada mas entrar.

    Uso (en PowerShell, dentro de la carpeta del proyecto):
        .\scripts\datos-demo.ps1                            # contra http://localhost:3400
        .\scripts\datos-demo.ps1 -BaseUrl http://localhost:8080

    No requiere instalar nada: usa Invoke-RestMethod, incluido en Windows.
    Pensado para una base de datos de desarrollo/demo, no para produccion.
#>
param([string]$BaseUrl = "http://localhost:3400")

$ErrorActionPreference = "Stop"
$BaseUrl = $BaseUrl.TrimEnd("/")
$script:token = $null

# Credenciales de la cuenta de demo (te serviran para entrar en la interfaz).
$Email = "demo@alxorcore.es"
$Nombre = "Dueno Demo"
$Pass = "Demo1234!"

function Call {
    param([string]$Method, [string]$Path, $Body = $null, [bool]$Auth = $true)
    $headers = @{}
    if ($Auth -and $script:token) { $headers["Authorization"] = "Bearer $script:token" }
    $params = @{ Method = $Method; Uri = "$BaseUrl$Path"; Headers = $headers }
    if ($null -ne $Body) {
        $params.Body = ($Body | ConvertTo-Json -Depth 6)
        $params.ContentType = "application/json; charset=utf-8"
    }
    return Invoke-RestMethod @params
}

function Paso($msg) { Write-Host "  $msg" }

function Credenciales {
    Write-Host ("`n" + ("-" * 48))
    Write-Host "  Entra en:   $BaseUrl"
    Write-Host "  Usuario:    $Email"
    Write-Host "  Contrasena: $Pass"
    Write-Host ("-" * 48)
}

Write-Host "ALXOR Core - sembrando datos de demo en $BaseUrl`n"

# Comprobacion de vida.
try { Call GET "/salud" -Auth $false | Out-Null }
catch {
    Write-Host "No se llega a la API en $BaseUrl. Esta arrancada? (docker compose up)"
    exit 1
}

# 1) Cuenta: registrar (o reutilizar si ya existe) e iniciar sesion.
Write-Host "Cuenta"
try {
    Call POST "/auth/registro" @{ email = $Email; nombre = $Nombre; contrasena = $Pass } -Auth $false | Out-Null
    Paso "usuario de demo creado"
} catch {
    Paso "el usuario de demo ya existia, reutilizandolo"
}
$login = Call POST "/auth/login" @{ email = $Email; contrasena = $Pass } -Auth $false
$script:token = $login.token
Paso "sesion iniciada"

# 2) Empresa: crear si no hay ninguna, y seleccionarla.
Write-Host "Empresa"
$empresas = Call GET "/empresas"
if ($empresas -and $empresas.Count -gt 0) {
    $emp = $empresas[0]
    Paso "reutilizando la empresa existente"
} else {
    Call POST "/empresas" @{
        nif = "B44531218"; razonSocial = "Estudio Demo ALXOR SL"
        calle = "Calle Mayor 1"; codigoPostal = "44600"
        poblacion = "Alcaniz"; provincia = "Teruel"
    } | Out-Null
    $emp = (Call GET "/empresas")[0]
    Paso "creada Estudio Demo ALXOR SL"
}
$script:token = (Call POST "/empresas/$($emp.id)/seleccionar").token

# Si ya hay facturas, no volvemos a sembrar (evita duplicados al reejecutar).
$existentes = Call GET "/facturas"
if ($existentes -and $existentes.Count -gt 0) {
    Write-Host "`nLa empresa ya tiene facturas: no se vuelve a sembrar para no duplicar."
    Credenciales
    return
}

# 3) Clientes.
Write-Host "Clientes"
$clientesDef = @(
    @{ nombre = "Consultoria Nebula SL"; nif = "B12345674"; email = "hola@nebula.example"; irpf = 0 },
    @{ nombre = "Marta Ruiz (autonoma)"; nif = "12345678Z"; email = "marta@ruizdiseno.example"; irpf = 15 },
    @{ nombre = "Talleres Ebro SA"; nif = "A58818501"; email = "compras@talleresebro.example"; irpf = 0 },
    @{ nombre = "Bar La Plaza"; nif = "77889900X"; email = "laplaza@example.com"; irpf = 0 },
    @{ nombre = "Ayuntamiento de Alcaniz"; nif = "P4400900J"; email = "registro@alcaniz.example"; irpf = 0 }
)
$clientes = @()
foreach ($c in $clientesDef) {
    $r = Call POST "/clientes" @{ nombre = $c.nombre; nifFiscal = $c.nif; email = $c.email; porcentajeIrpfDefecto = $c.irpf }
    $clientes += $r.id
}
Paso "$($clientes.Count) clientes"

# 4) Articulos.
Write-Host "Articulos"
$articulos = @(
    @{ n = "Hora de consultoria"; ref = "CONS-H"; p = 60; iva = "IVA21" },
    @{ n = "Diseno de marca"; ref = "DIS-MARCA"; p = 900; iva = "IVA21" },
    @{ n = "Mantenimiento mensual"; ref = "MANT-MES"; p = 150; iva = "IVA21" },
    @{ n = "Curso de formacion"; ref = "CURSO"; p = 300; iva = "IVA21" },
    @{ n = "Libro tecnico"; ref = "LIBRO"; p = 24; iva = "IVA4" },
    @{ n = "Menu del dia"; ref = "MENU"; p = 13; iva = "IVA10" }
)
foreach ($a in $articulos) {
    # Precio de compra ~55% del de venta (para que el informe de beneficio muestre margen).
    Call POST "/productos" @{ nombre = $a.n; referencia = $a.ref; precioUnitario = $a.p; precioCompra = [math]::Round($a.p * 0.55, 2); codigoIva = $a.iva; tipo = "Servicio" } | Out-Null
}
Paso "6 articulos"

$anio = (Get-Date).Year
$mesActual = (Get-Date).Month

# 5) Facturas repartidas por el anio (para poblar informes y trimestres).
Write-Host "Facturas"
# mes, dia, indiceCliente, descripcion, cantidad, precio, iva, irpf, diasVencimiento
$plan = @(
    @(1, 12, 0, "Consultoria enero", 20, 60, "IVA21", 0, 30),
    @(1, 28, 1, "Diseno de marca", 1, 900, "IVA21", 15, 15),
    @(2, 10, 2, "Mantenimiento Q1", 3, 150, "IVA21", 0, 30),
    @(3, 5, 3, "Curso de formacion", 1, 300, "IVA21", 0, 0),
    @(3, 22, 0, "Consultoria marzo", 12, 60, "IVA21", 0, 30),
    @(4, 8, 4, "Servicios abril", 25, 60, "IVA21", 0, 60),
    @(5, 15, 1, "Diseno folleto", 1, 450, "IVA21", 15, 30),
    @(6, 3, 2, "Mantenimiento Q2", 3, 150, "IVA21", 0, 30),
    @(6, 27, 3, "Menus evento", 40, 13, "IVA10", 0, 15),
    @(7, 9, 0, "Consultoria julio", 18, 60, "IVA21", 0, 30),
    @(8, 4, 4, "Formacion equipo", 2, 300, "IVA21", 0, 30),
    @(9, 1, 1, "Rediseno web", 1, 1200, "IVA21", 15, 30)
)
$facturas = @()
foreach ($f in $plan) {
    if ($f[0] -gt $mesActual) { continue }  # no sembramos facturas con fecha futura
    $fecha = "{0:D4}-{1:D2}-{2:D2}" -f $anio, $f[0], $f[1]
    $r = Call POST "/facturas" @{
        clienteId = $clientes[$f[2]]
        diasVencimiento = $f[8]
        fechaEmision = $fecha
        porcentajeIrpf = $f[7]
        lineas = @(@{ descripcion = $f[3]; cantidad = $f[4]; precioUnitario = $f[5]; codigoIva = $f[6] })
    }
    $facturas += $r
}
Paso "$($facturas.Count) facturas emitidas"

# 6) Cobros: casi todas menos las 2 ultimas (cartera pendiente); una parcial.
Write-Host "Cobros"
$cobradas = 0
for ($i = 0; $i -lt $facturas.Count; $i++) {
    if ($i -ge $facturas.Count - 2) { continue }
    $importe = $facturas[$i].total
    if ($i -eq $facturas.Count - 3) { $importe = [math]::Round($facturas[$i].total / 2, 2) }
    Call POST "/cobros" @{ facturaId = $facturas[$i].id; importe = $importe; metodo = "Transferencia" } | Out-Null
    $cobradas++
}
Paso "$cobradas cobros (1 parcial; 2 facturas quedan pendientes en cartera)"

# 7) Gastos repartidos por el anio.
Write-Host "Gastos"
$gastos = @(
    @(1, "Alquiler oficina enero", 400),
    @(2, "Suministros", 120),
    @(3, "Software (suscripciones)", 90),
    @(4, "Alquiler oficina abril", 400),
    @(5, "Material de oficina", 65),
    @(6, "Asesoria trimestral", 180),
    @(7, "Publicidad online", 250),
    @(8, "Formacion externa", 300)
)
$nGastos = 0
foreach ($g in $gastos) {
    if ($g[0] -gt $mesActual) { continue }
    $fecha = "{0:D4}-{1:D2}-05" -f $anio, $g[0]
    Call POST "/gastos" @{ concepto = $g[1]; baseImponible = $g[2]; codigoIva = "IVA21"; fecha = $fecha } | Out-Null
    $nGastos++
}
Paso "$nGastos gastos"

# 8) Una factura recurrente (para la vista de facturacion periodica).
#    La primera emision se fija al mes que viene para que aparezca como proxima.
Write-Host "Facturacion periodica"
if ($mesActual -eq 12) { $proxAnio = $anio + 1; $proxMes = 1 } else { $proxAnio = $anio; $proxMes = $mesActual + 1 }
$proxFecha = "{0:D4}-{1:D2}-01" -f $proxAnio, $proxMes
try {
    Call POST "/facturas-recurrentes" @{
        nombre = "Mantenimiento mensual - Talleres Ebro"
        clienteId = $clientes[2]
        periodicidad = "Mensual"
        primeraEmision = $proxFecha
        lineas = @(@{ descripcion = "Mantenimiento mensual"; cantidad = 1; precioUnitario = 150; codigoIva = "IVA21" })
    } | Out-Null
    Paso "1 suscripcion mensual (proxima emision el mes que viene)"
} catch {
    Paso "omitida ($($_.Exception.Message))"
}

Write-Host "`nListo! Datos de demo cargados."
Credenciales
