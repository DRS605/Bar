# Módulo Terceros

Gestiona **clientes y proveedores** de cada empresa (ambos multiempresa, con RLS).

Gestión de **clientes** de cada empresa. Es el primer módulo puramente multiempresa (todo su dato
lleva `empresa_id`, con filtro global y RLS).

## Modelo

`Cliente` { Nombre (obligatorio), NifFiscal (opcional, texto — admite clientes extranjeros), Email,
Direccion, PorcentajeIrpfDefecto (0–60 %, se prerrellena al facturar), Activo }.

## API

| Método | Ruta | Auth | Descripción |
|---|---|---|---|
| `GET` | `/clientes` | JWT + empresa | Lista los clientes activos. |
| `GET` | `/clientes/{id}` | JWT + empresa | Obtiene un cliente. |
| `POST` | `/clientes` | permiso `cliente.gestionar` | Crea un cliente. **201** |
| `PUT` | `/clientes/{id}` | permiso `cliente.gestionar` | Actualiza un cliente. |

## Persistencia

- Esquema **`terceros`**, tabla `cliente` (con RLS por empresa).
- El repositorio implementa tanto la escritura (`IRepositorioClientes`) como las consultas de
  lectura (`IConsultaClientes`), que también consumirá **Facturación** para tomar los datos del
  cliente al emitir una factura.

## Tests

- **Unitarios**: validaciones de `Cliente` (nombre, IRPF), creación/actualización/desactivación.
- **Integración**: CRUD completo y **aislamiento por empresa** (una empresa no ve los clientes de
  otra).


## Proveedores

`Proveedor` { Nombre, NifFiscal opcional, Email, Direccion, PorcentajeIrpfDefecto, Activo }. CRUD en `/proveedores` (escritura con permiso `gasto.gestionar`). Los usa el módulo Gastos para enlazar cada gasto a un proveedor y copiar su nombre.
