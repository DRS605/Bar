# Módulo Gastos

Registro de **gastos** (facturas recibidas simplificadas). El proveedor se guarda como **texto
libre** (en el MVP no hay entidad Proveedor).

## Modelo

`Gasto` { ProveedorTexto (opcional), Concepto, Fecha, BaseImponible, CodigoIva/PorcentajeIva,
CuotaIva (IVA soportado), PorcentajeIrpf/RetencionIrpf, Total, Estado }. Multiempresa (RLS).

Cálculo: `cuota = redondeo(base × IVA%)`, `retención = redondeo(base × IRPF%)`,
`total = base + cuota − retención`.

## API

| Método | Ruta | Auth | Descripción |
|---|---|---|---|
| `GET` | `/gastos` | permiso `gasto.leer` | Lista de gastos. |
| `GET` | `/gastos/{id}` | permiso `gasto.leer` | Obtiene un gasto. |
| `POST` | `/gastos` | permiso `gasto.gestionar` | Registra un gasto. **201** |

## Persistencia

Esquema **`gastos`**, tabla `gasto` (RLS por empresa). Repositorio con escritura
(`IRepositorioGastos`) y consultas (`IConsultaGastos`), que usarán Tesorería e Informes.

## Tests

- **Unitarios**: cálculo de IVA soportado y retención, validaciones, anulación.
- **Integración**: registrar/listar/obtener y aislamiento por empresa.
