#!/usr/bin/env python3
"""
Datos de demostración para ALXOR Core.

Rellena una instancia recién arrancada con una empresa, clientes, artículos,
facturas repartidas por el año, cobros (algunos parciales), gastos y una factura
recurrente, para que el panel y los informes tengan contenido nada más entrar.

Uso:
    python3 scripts/datos-demo.py                       # contra http://localhost:3400
    python3 scripts/datos-demo.py http://localhost:8080 # otra URL base

Solo usa la biblioteca estándar (urllib): no requiere instalar nada.
Pensado para una base de datos de desarrollo/demo, no para producción.
"""
import json
import sys
import urllib.request
import urllib.error
from datetime import date

BASE = (sys.argv[1] if len(sys.argv) > 1 else "http://localhost:3400").rstrip("/")

# Credenciales de la cuenta de demo (te servirán para entrar en la interfaz).
EMAIL = "demo@alxorcore.es"
NOMBRE = "Dueño Demo"
PASS = "Demo1234!"

_token = None


def llamar(metodo, ruta, cuerpo=None, auth=True):
    datos = json.dumps(cuerpo).encode() if cuerpo is not None else None
    req = urllib.request.Request(BASE + ruta, data=datos, method=metodo)
    req.add_header("Content-Type", "application/json")
    if auth and _token:
        req.add_header("Authorization", "Bearer " + _token)
    try:
        with urllib.request.urlopen(req) as r:
            texto = r.read().decode()
            return json.loads(texto) if texto else {}
    except urllib.error.HTTPError as e:
        detalle = e.read().decode()
        raise RuntimeError(f"{metodo} {ruta} -> {e.code}: {detalle}") from None


def paso(msg):
    print("  " + msg)


def main():
    global _token
    print(f"ALXOR Core · sembrando datos de demo en {BASE}\n")

    # Comprobación de vida.
    try:
        llamar("GET", "/salud", auth=False)
    except Exception as e:
        print(f"No se llega a la API en {BASE}. ¿Está arrancada? ({e})")
        sys.exit(1)

    # 1) Cuenta: registrar (o reutilizar si ya existe) e iniciar sesión.
    print("Cuenta")
    try:
        llamar("POST", "/auth/registro", {"email": EMAIL, "nombre": NOMBRE, "contrasena": PASS}, auth=False)
        paso("usuario de demo creado")
    except RuntimeError:
        paso("el usuario de demo ya existía, reutilizándolo")
    login = llamar("POST", "/auth/login", {"email": EMAIL, "contrasena": PASS}, auth=False)
    _token = login["token"]
    paso("sesión iniciada")

    # 2) Empresa: crear si no hay ninguna, y seleccionarla.
    print("Empresa")
    empresas = llamar("GET", "/empresas")
    if empresas:
        emp = empresas[0]
        paso(f"reutilizando «{emp.get('razonSocial', emp['id'])}»")
    else:
        llamar("POST", "/empresas", {
            "nif": "B44531218", "razonSocial": "Estudio Demo ALXOR SL",
            "calle": "Calle Mayor 1", "codigoPostal": "44600",
            "poblacion": "Alcañiz", "provincia": "Teruel",
        })
        emp = llamar("GET", "/empresas")[0]
        paso(f"creada «{emp['razonSocial']}»")
    _token = llamar("POST", f"/empresas/{emp['id']}/seleccionar")["token"]

    # Si ya hay facturas, no volvemos a sembrar (evita duplicados al reejecutar).
    if llamar("GET", "/facturas"):
        print("\nLa empresa ya tiene facturas: no se vuelve a sembrar para no duplicar.")
        credenciales()
        return

    # 3) Clientes.
    print("Clientes")
    clientes = []
    for nombre, nif, email, irpf in [
        ("Consultoría Nébula SL", "B12345674", "hola@nebula.example", 0),
        ("Marta Ruiz (autónoma)", "12345678Z", "marta@ruizdiseno.example", 15),
        ("Talleres Ebro SA", "A58818501", "compras@talleresebro.example", 0),
        ("Bar La Plaza", "77889900X", "laplaza@example.com", 0),
        ("Ayuntamiento de Alcañiz", "P4400900J", "registro@alcaniz.example", 0),
    ]:
        c = llamar("POST", "/clientes", {"nombre": nombre, "nifFiscal": nif, "email": email, "porcentajeIrpfDefecto": irpf})
        clientes.append(c["id"])
    paso(f"{len(clientes)} clientes")

    # 4) Artículos.
    print("Artículos")
    for nombre, ref, precio, iva in [
        ("Hora de consultoría", "CONS-H", 60, "IVA21"),
        ("Diseño de marca", "DIS-MARCA", 900, "IVA21"),
        ("Mantenimiento mensual", "MANT-MES", 150, "IVA21"),
        ("Curso de formación", "CURSO", 300, "IVA21"),
        ("Libro técnico", "LIBRO", 24, "IVA4"),
        ("Menú del día", "MENU", 13, "IVA10"),
    ]:
        llamar("POST", "/productos", {"nombre": nombre, "referencia": ref, "precioUnitario": precio, "codigoIva": iva, "tipo": "Servicio"})
    paso("6 artículos")

    anio = date.today().year
    mes_actual = date.today().month

    # 5) Facturas repartidas por el año (para poblar informes y trimestres).
    print("Facturas")
    plan = [
        # (mes, día, cliente, descripción, cantidad, precio, iva, irpf, díasVenc)
        (1, 12, 0, "Consultoría enero", 20, 60, "IVA21", 0, 30),
        (1, 28, 1, "Diseño de marca", 1, 900, "IVA21", 15, 15),
        (2, 10, 2, "Mantenimiento Q1", 3, 150, "IVA21", 0, 30),
        (3, 5, 3, "Curso de formación", 1, 300, "IVA21", 0, 0),
        (3, 22, 0, "Consultoría marzo", 12, 60, "IVA21", 0, 30),
        (4, 8, 4, "Servicios abril", 25, 60, "IVA21", 0, 60),
        (5, 15, 1, "Diseño folleto", 1, 450, "IVA21", 15, 30),
        (6, 3, 2, "Mantenimiento Q2", 3, 150, "IVA21", 0, 30),
        (6, 27, 3, "Menús evento", 40, 13, "IVA10", 0, 15),
        (7, 9, 0, "Consultoría julio", 18, 60, "IVA21", 0, 30),
        (8, 4, 4, "Formación equipo", 2, 300, "IVA21", 0, 30),
        (9, 1, 1, "Rediseño web", 1, 1200, "IVA21", 15, 30),
    ]
    facturas = []
    for mes, dia, ci, desc, cant, precio, iva, irpf, venc in plan:
        if mes > mes_actual:
            continue  # no sembramos facturas con fecha futura
        f = llamar("POST", "/facturas", {
            "clienteId": clientes[ci],
            "diasVencimiento": venc,
            "fechaEmision": f"{anio}-{mes:02d}-{dia:02d}",
            "porcentajeIrpf": irpf,
            "lineas": [{"descripcion": desc, "cantidad": cant, "precioUnitario": precio, "codigoIva": iva}],
        })
        facturas.append(f)
    paso(f"{len(facturas)} facturas emitidas")

    # 6) Cobros: cobramos casi todas menos las 2 últimas (para dejar cartera pendiente),
    #    y una la dejamos a medias (cobro parcial).
    print("Cobros")
    cobradas = 0
    for i, f in enumerate(facturas):
        pendientes = i >= len(facturas) - 2
        if pendientes:
            continue
        importe = f["total"]
        metodo = "Transferencia"
        if i == len(facturas) - 3:  # una parcial
            importe = round(f["total"] / 2, 2)
        llamar("POST", "/cobros", {"facturaId": f["id"], "importe": importe, "metodo": metodo})
        cobradas += 1
    paso(f"{cobradas} cobros (1 parcial; 2 facturas quedan pendientes en cartera)")

    # 7) Gastos repartidos por el año.
    print("Gastos")
    gastos = [
        (1, "Alquiler oficina enero", 400, "IVA21"),
        (2, "Suministros", 120, "IVA21"),
        (3, "Software (suscripciones)", 90, "IVA21"),
        (4, "Alquiler oficina abril", 400, "IVA21"),
        (5, "Material de oficina", 65, "IVA21"),
        (6, "Asesoría trimestral", 180, "IVA21"),
        (7, "Publicidad online", 250, "IVA21"),
        (8, "Formación externa", 300, "IVA21"),
    ]
    n_gastos = 0
    for mes, concepto, base, iva in gastos:
        if mes > mes_actual:
            continue
        llamar("POST", "/gastos", {"concepto": concepto, "baseImponible": base, "codigoIva": iva, "fecha": f"{anio}-{mes:02d}-05"})
        n_gastos += 1
    paso(f"{n_gastos} gastos")

    # 8) Una factura recurrente (para la vista de facturación periódica).
    #    La primera emisión se fija al mes que viene para que aparezca como próxima
    #    (y no la emita al vuelo el proceso automático en segundo plano).
    print("Facturación periódica")
    prox_anio, prox_mes = (anio + 1, 1) if mes_actual == 12 else (anio, mes_actual + 1)
    try:
        llamar("POST", "/facturas-recurrentes", {
            "nombre": "Mantenimiento mensual · Talleres Ebro",
            "clienteId": clientes[2],
            "periodicidad": "Mensual",
            "primeraEmision": f"{prox_anio}-{prox_mes:02d}-01",
            "lineas": [{"descripcion": "Mantenimiento mensual", "cantidad": 1, "precioUnitario": 150, "codigoIva": "IVA21"}],
        })
        paso("1 suscripción mensual (próxima emisión el mes que viene)")
    except RuntimeError as e:
        paso(f"omitida ({e})")

    print("\n¡Listo! Datos de demo cargados.")
    credenciales()


def credenciales():
    print("\n" + "─" * 48)
    print(f"  Entra en:   {BASE}")
    print(f"  Usuario:    {EMAIL}")
    print(f"  Contraseña: {PASS}")
    print("─" * 48)


if __name__ == "__main__":
    main()
