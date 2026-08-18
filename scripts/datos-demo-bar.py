#!/usr/bin/env python3
"""
Datos de demostración de BAR para Bar Query.

Rellena una instancia recién arrancada como si fuera un bar en marcha: carta por
categorías (cervezas, vinos, refrescos, cafés, tapas), mesas por zonas (salón,
terraza, barra), turnos con aforo, reservas para hoy y una comanda abierta, para
que la Barra/Salón, Reservas, el Plano y el TPV se vean vivos nada más entrar.

Uso:
    python3 scripts/datos-demo-bar.py                       # contra http://localhost:3400
    python3 scripts/datos-demo-bar.py http://localhost:8080 # otra URL base

Solo usa la biblioteca estándar (urllib): no requiere instalar nada.
Es idempotente: si el bar ya tiene mesas, no vuelve a sembrar. Para desarrollo/demo.
"""
import json
import sys
import urllib.request
import urllib.error
from datetime import date

BASE = (sys.argv[1] if len(sys.argv) > 1 else "http://localhost:3400").rstrip("/")

EMAIL = "bar@barquery.es"
NOMBRE = "Dueño del bar"
PASS = "Demo1234!"
DIAS_TODOS = 127  # DiasSemana.Todos (L..D)

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
        raise RuntimeError(f"{metodo} {ruta} -> {e.code}: {e.read().decode()}") from None


def paso(msg):
    print("  " + msg)


def credenciales():
    print("\nListo. Entra en la interfaz con:")
    print(f"  {BASE}")
    print(f"  Usuario:    {EMAIL}")
    print(f"  Contraseña: {PASS}")


def main():
    global _token
    print(f"Bar Query · sembrando datos de bar en {BASE}\n")

    try:
        llamar("GET", "/salud", auth=False)
    except Exception as e:
        print(f"No se llega a la API en {BASE}. ¿Está arrancada? ({e})")
        sys.exit(1)

    # 1) Cuenta.
    print("Cuenta")
    try:
        llamar("POST", "/auth/registro", {"email": EMAIL, "nombre": NOMBRE, "contrasena": PASS}, auth=False)
        paso("usuario de demo creado")
    except RuntimeError:
        paso("el usuario ya existía, reutilizándolo")
    _token = llamar("POST", "/auth/login", {"email": EMAIL, "contrasena": PASS}, auth=False)["token"]

    # 2) Empresa (el bar).
    print("Local")
    empresas = llamar("GET", "/empresas")
    if empresas:
        emp = empresas[0]
        paso(f"reutilizando «{emp.get('razonSocial', emp['id'])}»")
    else:
        llamar("POST", "/empresas", {
            "nif": "44112233G", "razonSocial": "Bar Sol de Levante",
            "calle": "Calle Mayor 8", "codigoPostal": "41001",
            "poblacion": "Sevilla", "provincia": "Sevilla",
        })
        emp = llamar("GET", "/empresas")[0]
        paso(f"creado «{emp['razonSocial']}»")
    _token = llamar("POST", f"/empresas/{emp['id']}/seleccionar")["token"]

    # Idempotencia: si ya hay mesas, no re-sembramos el bar.
    if llamar("GET", "/mesas"):
        print("\nEl bar ya tiene mesas: no se vuelve a sembrar para no duplicar.")
        credenciales()
        return

    # 3) Carta por categorías.
    print("Carta")
    carta = [
        ("Cervezas", [("Caña", 1.50), ("Doble", 2.20), ("Tercio", 2.50), ("Sin alcohol", 2.00)]),
        ("Vinos", [("Vino tinto", 2.20), ("Vino blanco", 2.20), ("Copa de cava", 3.00)]),
        ("Refrescos", [("Agua", 1.20), ("Coca-Cola", 1.80), ("Tónica", 2.00)]),
        ("Cafés", [("Café", 1.30), ("Cortado", 1.30), ("Carajillo", 2.00)]),
        ("Tapas", [("Tortilla", 4.50), ("Croquetas", 5.00), ("Ensaladilla", 6.00), ("Jamón", 12.00), ("Patatas bravas", 5.50)]),
    ]
    productos = {}
    n = 0
    for categoria, items in carta:
        for nombre, precio in items:
            p = llamar("POST", "/productos", {
                "nombre": nombre, "precioUnitario": precio, "precioCompra": round(precio * 0.35, 2),
                "codigoIva": "IVA10", "tipo": "Bien", "unidad": "ud", "categoria": categoria, "controlarStock": False,
            })
            productos[nombre] = p["id"]
            n += 1
    paso(f"{n} artículos en {len(carta)} categorías")

    # 4) Mesas por zonas.
    print("Mesas")
    mesas = {}
    plano = [
        ("Salón", ["Mesa 1", "Mesa 2", "Mesa 3", "Mesa 4"], "Cuadrada"),
        ("Terraza", ["Terraza 1", "Terraza 2", "Terraza 3"], "Redonda"),
        ("Barra", ["Barra"], "Rectangular"),
    ]
    for zona, nombres, forma in plano:
        for nombre in nombres:
            m = llamar("POST", "/mesas", {"nombre": nombre, "zona": zona, "capacidad": 4, "forma": forma})
            mesas[nombre] = m["id"]
    paso(f"{len(mesas)} mesas en {len(plano)} zonas")

    # 5) Turnos con aforo.
    print("Turnos")
    for nombre, ini, fin, aforo in [("Comida", "13:00", "16:00", 40), ("Cena", "20:00", "23:30", 40)]:
        llamar("POST", "/turnos", {"nombre": nombre, "dias": DIAS_TODOS, "horaInicio": ini, "horaFin": fin, "aforoComensales": aforo})
    paso("2 turnos (Comida y Cena)")

    # 6) Reservas para hoy.
    print("Reservas")
    hoy = date.today().isoformat()
    reservas = [
        ("Ana García", "600111222", "21:00", 4, mesas.get("Terraza 1"), "Cumpleaños"),
        ("Carlos Ruiz", "600333444", "21:30", 2, None, None),
        ("Familia López", "600555666", "14:30", 6, None, "Menú del día"),
    ]
    for nombre, tel, hora, pax, mesa_id, notas in reservas:
        cuerpo = {"nombreCliente": nombre, "telefono": tel, "fechaHora": f"{hoy}T{hora}:00",
                  "duracionMinutos": 90, "comensales": pax, "notas": notas}
        if mesa_id:
            cuerpo["mesaId"] = mesa_id
        llamar("POST", "/reservas", cuerpo)
    paso(f"{len(reservas)} reservas para hoy")

    # 7) Una comanda abierta (para que la barra se vea con actividad).
    print("Comanda")
    comanda = llamar("POST", "/comandas", {"mesaId": mesas["Mesa 1"], "notas": "sin gluten"})
    for nombre, cant in [("Caña", 2), ("Tortilla", 1), ("Croquetas", 1)]:
        llamar("POST", f"/comandas/{comanda['id']}/lineas", {"productoId": productos[nombre], "cantidad": cant})
    paso("1 comanda abierta en Mesa 1")

    credenciales()


if __name__ == "__main__":
    main()
