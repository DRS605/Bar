# ALXOR Core — Documento de diseño técnico y funcional (MVP)

> **Estado: propuesta para revisión.** Entregable previo al código. No se ha escrito todavía el
> proyecto. Cuando lo apruebes, el desarrollo será **módulo a módulo**, cada uno completamente
> terminado (dominio · API · persistencia · tests unitarios · tests de integración · documentación)
> antes de empezar el siguiente.

---

## 0. Visión y objetivo

ALXOR Core **no** es un ERP tradicional. Es **el ERP más sencillo del mercado para pequeñas
empresas españolas**. El éxito no se mide en número de funcionalidades, sino en que **cualquier
persona pueda empezar a trabajar sin leer un manual** y **emitir una factura en menos de 5 minutos**
sin haber usado nunca el programa.

Regla de oro para toda decisión (técnica, funcional o de UX): **la simplicidad gana siempre a la
cantidad de funcionalidades.**

---

## 1. Filosofía de producto (principios inquebrantables)

- La simplicidad siempre gana a la cantidad de funcionalidades.
- Ninguna pantalla debe intimidar al usuario.
- Ninguna acción frecuente debe requerir más de **tres clics**.
- **Todo** debe poder hacerse con teclado.
- Nunca añadiremos una funcionalidad solo porque otro ERP la tenga.
- Solo añadiremos funcionalidades **solicitadas o validadas por clientes reales**.
- Debe sentirse como una **aplicación moderna**, no como un ERP clásico.

Estos principios son criterio de aceptación: una funcionalidad que los incumpla no entra, aunque
"sea útil".

---

## 2. Modelo de negocio

ALXOR **no vende un ERP**: vende **soluciones independientes** que funcionan solas o integradas.

Productos:

- **ALXOR Core Start** — el MVP de este documento (gestión diaria de una microempresa).
- **ALXOR Core Pro** — evolución con funciones avanzadas (ver §14).
- **Questioner** (calidad), **Tuday** (control horario), **CostControl** (costes) — **aplicaciones
  totalmente independientes**, no forman parte del código de ALXOR Core.

Reglas del modelo:

- Un cliente puede usar cualquier aplicación **sin necesidad de tener ALXOR Core**.
- ALXOR Core es únicamente **el núcleo donde converge la información**.
- Si más adelante el cliente quiere centralizar, incorpora ALXOR Core **sin migraciones ni pérdida
  de datos**. → Implicación técnica: las integraciones con otros productos se harán vía **API y
  eventos** detrás de puertos; **nada de acoplamiento** en el código inicial.

---

## 3. Alcance del MVP (ALXOR Core Start)

**Incluye únicamente:**

- Registro e inicio de sesión
- Empresa
- Usuarios
- Clientes
- Productos y servicios
- Facturas emitidas
- Gastos
- Cobros
- Pagos
- Dashboard
- Informes básicos
- **Libros de IVA** (repercutido/soportado)
- **Exportación para la gestoría**
- PDF de documentos
- Envío por email

**Fuera del MVP** (pertenece a ALXOR Core Pro u otros productos): presupuestos, pedidos, albaranes,
producción, stock, compras avanzadas, contabilidad completa, CRM, RR. HH., nóminas, BI,
automatizaciones e IA.

**Contabilidad**: en el MVP **no** hay programa de contabilidad. Solo facturación, gastos, cobros,
pagos, **libros de IVA** y **exportación para la gestoría**. La contabilidad completa llega después.

---

## 4. Experiencia de usuario (PRIORIDAD ABSOLUTA)

Cada pantalla debe cumplir:

- Menú lateral con **menos de 10 opciones**.
- Formularios con **el menor número posible de campos**.
- **Opciones avanzadas ocultas** (plegadas por defecto).
- **Valores por defecto inteligentes** (IVA 21 %, serie del año, fecha de hoy, cliente reciente…).
- Diseño limpio, **muy pocos colores**, **mucho espacio en blanco**.
- Iconografía sencilla, **lenguaje natural**, sin terminología técnica innecesaria.
- Todo operable con **teclado**; acciones frecuentes en **≤ 3 clics**.

Criterio de aceptación de UX: **emitir una factura en < 5 minutos sin formación previa**.

> Nota: la UI se construye después del backend (API-first ya acordado). Estos principios se fijan
> **ahora** porque condicionan la API: endpoints pensados para flujos de ≤ 3 pasos, valores por
> defecto resueltos en servidor, y respuestas listas para pintar sin lógica compleja en cliente.

Menú lateral objetivo del MVP (**8 opciones**): Inicio (Dashboard) · Facturas · Gastos · Clientes ·
Productos · Cobros y pagos · Informes · Ajustes.

---

## 5. Perfiles de usuario y tareas

| Perfil | Quién es | Tareas principales |
|---|---|---|
| **Propietario** | Dueño de la microempresa | Crear empresa, emitir facturas, registrar gastos/cobros/pagos, ver dashboard e informes, gestionar usuarios |
| **Usuario** | Persona de apoyo/gestión | Facturas, gastos, cobros/pagos, clientes, productos, envío por email |
| **Solo lectura / Gestoría** | Asesor externo | Consultar y **exportar** facturas, gastos, libros de IVA e informes; sin editar |

Roles del MVP: **Propietario, Usuario, Solo lectura**. Permisos granulares por debajo (§10).

---

## 6. Flujos esenciales del MVP

1. **Alta y arranque**: registro → (verificación email) → crear empresa (NIF, dirección, IVA por
   defecto, serie) → listo para facturar.
2. **Emitir factura (flujo estrella, < 5 min, ≤ 3 clics)**: nueva factura → elegir/crear cliente →
   añadir línea (producto o texto libre) → **emitir**. PDF y envío por email opcionales al final.
3. **Registrar un gasto**: nuevo gasto → proveedor (texto) + importe + IVA → guardar.
4. **Cobrar / pagar**: sobre una factura o gasto → registrar cobro/pago (total o parcial) → el saldo
   se actualiza solo.
5. **Cierre del día**: dashboard con pendientes de cobro/pago y totales del mes.
6. **Gestoría**: exportar libros de IVA y documentos del periodo (CSV/estándar).

---

## 7. Módulos y responsabilidades (monolito modular)

Cada módulo es un *bounded context* con fronteras claras. Se comunican por **interfaces de
aplicación** y **eventos de dominio in-process** (un módulo no toca la BD de otro).

| # | Módulo | Responsabilidad | Agregados |
|---|---|---|---|
| — | **Nucleo (SharedKernel)** | Tipos comunes: `Dinero`, `TipoImpositivo`, `ContextoEmpresa`, `Resultado`, IDs, fechas | Value objects |
| 1 | **Identidad** | Registro, login, JWT, usuarios, roles, permisos, membresías | Usuario, Rol, Membresia |
| 2 | **Organizacion** | Empresas (tenants), ajustes fiscales, series y numeración | Empresa, SerieNumeracion |
| 3 | **Terceros** | Clientes | Cliente |
| 4 | **Catalogo** | Productos/servicios e impuestos | Producto, Impuesto |
| 5 | **Facturacion** | Facturas emitidas (sin presupuestos/pedidos/albaranes) | Factura |
| 6 | **Gastos** | Gastos (proveedor como texto libre) | Gasto |
| 7 | **Tesoreria** | Cobros y pagos (total/parcial), saldo de documentos | Movimiento (cobro/pago) |
| 8 | **Documentos** | PDF y envío por email (adaptadores tras puertos) | — |
| 9 | **Informes** | Dashboard, informes básicos, **libros de IVA**, **exportación gestoría** | — (lecturas) |
| — | **Auditoria** | Registro inmutable de operaciones críticas (transversal) | RegistroAuditoria |

> Cambios respecto a la versión anterior: se eliminan **Presupuestos, Pedidos y Albaranes**; el
> antiguo módulo "Ventas" se reduce a **Facturacion**; "Compras" se reduce a **Gastos**; se añade a
> **Informes** los **libros de IVA** y la **exportación para gestoría**. Se elimina el módulo de
> proveedores como entidad (proveedor = texto en el gasto).

---

## 8. Modelo de dominio (visión de agregados)

Notación: **Agregado** { campos clave }. Todo agregado de negocio lleva `empresa_id`.

- **Empresa** { NIF, razón social, dirección fiscal, régimen IVA, moneda=EUR, país=ES }
- **Usuario** { email, hash contraseña, nombre, estado, email verificado } *(global, no por tenant)*
- **Membresia** { usuario_id, empresa_id, rol, estado }
- **Cliente** { NIF/NIE/VAT, nombre, dirección, email, IRPF por defecto }
- **Producto** { referencia, nombre, tipo (bien/servicio), precio, impuesto por defecto, unidad }
- **Impuesto** { código, tipo (IVA/IRPF), porcentaje, vigencia }
- **SerieNumeracion** { tipo de documento, ejercicio, prefijo, **siguiente número** }
- **Factura** { serie+número, cliente (datos **congelados**), líneas, base, cuota IVA, retención
  IRPF, total, estado, **fecha emisión**, **fecha operación**, tipo (ordinaria/rectificativa),
  factura rectificada (ref), tipo de operación, *campos VeriFactu reservados* }
- **Gasto** { proveedor (texto), concepto, base, IVA soportado, retención, fecha, estado }
- **Movimiento (cobro/pago)** { documento asociado (factura/gasto), sentido, importe, fecha, método }
- **RegistroAuditoria** { actor, empresa, entidad, id, acción, cambios, timestamp }

**Value objects**: `Dinero` (importe + moneda, redondeo a 2 decimales), `TipoImpositivo`, `NIF`,
`Direccion`, `NumeroDocumento`.

**Datos congelados**: al emitir, la factura copia NIF/nombre/dirección del cliente y
precios/impuestos de las líneas. No depende de cambios posteriores en cliente o producto.

### Campos VeriFactu/SII reservados (no calculados en el MVP)

En `Factura`, nullable, para no rehacer el núcleo al añadir SII/VeriFactu en el futuro: `huella`,
`huella_anterior`, `id_registro`, `tipo_operacion`, `clave_regimen`, `fecha_expedicion`,
`estado_envio_aeat`.

---

## 9. Invariantes y reglas críticas

**Facturación:**
- **F1 — Numeración correlativa sin huecos** por (serie + ejercicio); se asigna **solo al emitir**,
  bajo transacción/bloqueo.
- **F2 — Factura emitida inmutable**: no se edita ni se borra; corrección solo vía rectificativa.
- **F3 — Cuadre**: `total = Σ base_línea + Σ cuota_IVA − retención_IRPF`; impuestos por línea;
  redondeo a 2 decimales consistente.
- **F4 — Datos fiscales congelados** al emitir.
- **F5 — Fechas coherentes**: `fecha_operación ≤ fecha_emisión`; ejercicio derivado de la emisión.
- **F6 — Rectificativa**: exige motivo, referencia a original y signo coherente.

**Tesorería:**
- **P1** — La suma de cobros/pagos de un documento **no puede superar su total**.
- **P2** — Estado del documento (pendiente/parcial/pagado) **derivado** del saldo, no editable a mano.

**Multiempresa / seguridad:**
- **T1 — Aislamiento total** por `empresa_id`; RLS como red de seguridad.
- **T2** — Un usuario solo opera sobre empresas donde tiene `Membresia` activa.

**Impuestos (MVP):** IVA 21/10/4/0 %; retención IRPF 7/15 % (opción avanzada, oculta por defecto).

---

## 10. Multiempresa, permisos y auditoría

**Multiempresa (multitenancy):** BD compartida, `empresa_id` obligatorio en cada agregado. Tenant
resuelto del JWT → `ContextoEmpresa` (scoped) + `SET app.empresa_actual` en PostgreSQL. Doble
barrera: **global query filter de EF Core** (imposible olvidar el WHERE) + **RLS en PostgreSQL**
(la BD no devuelve filas de otro tenant aunque una consulta se escape).

**Permisos (granular):** códigos finos (`factura.emitir`, `factura.leer`, `cobro.registrar`,
`gasto.gestionar`, `cliente.gestionar`, `producto.gestionar`, `informe.leer`, `datos.exportar`,
`empresa.ajustes`, `usuario.gestionar`). Roles = conjuntos de permisos (Propietario = todos;
Usuario = operativa sin gestión de usuarios/ajustes sensibles; Solo lectura = `*.leer` +
`datos.exportar`). Comprobación centralizada en la capa de aplicación (`IComprobadorPermisos`).

**Auditoría:** los agregados emiten **eventos de dominio**; un manejador escribe en
`registro_auditoria` (append-only) **en la misma transacción**. Se audita: emitir/rectificar
factura, registrar cobro/pago, cambios de estado, alta/baja de usuarios y permisos, cambios de
ajustes fiscales y series. Base para el registro VeriFactu futuro.

---

## 11. Esquema inicial de base de datos (PostgreSQL)

Convenciones: **PK `uuid` (v7)**; `empresa_id uuid` en toda tabla de negocio; `creado_en/
actualizado_en timestamptz`; sin borrado físico de documentos fiscales; índices `(empresa_id, …)`;
**nombres en español, `snake_case`**.

- `usuario` (id, email UNIQUE, hash_password, nombre, email_verificado, estado, timestamps)
- `empresa` (id, nif, razon_social, direccion_*, regimen_iva, moneda, pais, timestamps)
- `membresia` (id, usuario_id, empresa_id, rol, estado, UNIQUE(usuario_id, empresa_id))
- `rol` / `permiso` / `rol_permiso`
- `serie_numeracion` (id, empresa_id, tipo_documento, ejercicio, prefijo, siguiente_numero, UNIQUE(empresa_id, tipo_documento, ejercicio, prefijo))
- `cliente` (id, empresa_id, nif, nombre, direccion(jsonb), email, irpf_defecto)
- `producto` (id, empresa_id, referencia, nombre, tipo, precio_unitario, impuesto_defecto_id, unidad)
- `impuesto` (id, empresa_id, codigo, tipo[IVA|IRPF], porcentaje, vigente_desde, vigente_hasta)
- `factura` (id, empresa_id, serie_id, numero, fecha_emision, fecha_operacion, cliente_snapshot(jsonb), base, cuota_iva, retencion_irpf, total, estado, tipo_factura, rectifica_factura_id, tipo_operacion, **campos veri_factu nullable**)
- `linea_factura` (id, factura_id, producto_id?, descripcion, cantidad, precio_unitario, descuento, impuesto_id, base_linea, cuota_linea)
- `gasto` (id, empresa_id, proveedor_texto, concepto, base, cuota_iva, retencion, fecha, estado)
- `movimiento` (id, empresa_id, tipo_documento, documento_id, sentido[cobro|pago], importe, fecha, metodo)
- `registro_auditoria` (id, empresa_id, actor_usuario_id, entidad, entidad_id, accion, cambios(jsonb), en) — *append-only*

RLS por `current_setting('app.empresa_actual')` en toda tabla con `empresa_id`.

---

## 12. Estados y transiciones

- **Factura**: `borrador → emitida → (cobro parcial) → pagada`; `emitida → rectificada`; nunca se
  borra una emitida; la numeración se asigna en `borrador → emitida`.
- **Gasto**: `borrador → registrado → (pago parcial) → pagado | anulado`.
- **Movimiento**: sin máquina de estados propia; mueve el saldo del documento (`pendiente → parcial
  → pagado`), derivado (P2).

Transiciones inválidas lanzan error de dominio y están cubiertas por tests.

---

## 13. Arquitectura técnica (decisiones aprobadas, sin cambios)

.NET 8 LTS · PostgreSQL · **monolito modular** · **API First** · **Clean Architecture ligera** ·
**DDD práctico** · EF Core · JWT · **UUID** · **BD compartida con `empresa_id` obligatorio** ·
arquitectura preparada para crecer 20 años. Sin sobrearquitectura (sin microservicios, sin
CQRS/event-sourcing pesado, sin MediatR obligatorio).

```
src/
  AlxorCore.Api            → host web, endpoints REST, auth, OpenAPI (Swagger)
  AlxorCore.Nucleo         → Dinero, Resultado, ContextoEmpresa, IDs, tipos comunes
  Modulos/
    Identidad/{Dominio, Aplicacion, Infraestructura}
    Organizacion/{...}
    Terceros/{...}
    Catalogo/{...}
    Facturacion/{...}
    Gastos/{...}
    Tesoreria/{...}
    Documentos/{Aplicacion(puertos), Infraestructura(PDF, correo)}
    Informes/{Aplicacion, Infraestructura}
    Auditoria/{...}
tests/
  <por módulo>.PruebasUnitarias / .PruebasIntegracion
```

- **Dominio** sin dependencias de framework; **Aplicación** = casos de uso simples + puertos;
  **Infraestructura** = EF Core (Npgsql), repositorios, adaptadores; **Api** = REST + OpenAPI + JWT.
- Persistencia con **global query filter** por tenant + **RLS**.
- Integraciones externas (correo, futuro AEAT/SII, otros productos ALXOR) siempre tras **puertos**.
- Testing: unit (impuestos, numeración, estados, permisos) + integración con **PostgreSQL real vía
  Testcontainers**.
- Coste operativo bajo: 1 contenedor .NET + 1 PostgreSQL pequeño.

---

## 14. Método de desarrollo: **un módulo a la vez, completamente terminado**

Regla estricta (tu requisito): **nunca se desarrolla más de un módulo simultáneamente**. Un módulo
no se da por hecho ni se avanza al siguiente hasta que están **todos** verdes:

- [ ] Dominio (entidades, value objects, invariantes, eventos)
- [ ] API (endpoints REST + contrato OpenAPI)
- [ ] Persistencia (EF Core, migración, RLS donde aplique)
- [ ] Tests unitarios
- [ ] Tests de integración (Testcontainers)
- [ ] Documentación del módulo

**Orden propuesto** (respeta dependencias; el **Nucleo** y la infra base de persistencia/tenant son
prerrequisito y se entregan junto con el módulo 1):

1. **Identidad** (+ Nucleo + infra base: EF Core, `ContextoEmpresa`, RLS, JWT, CI/tests).
2. **Organizacion** (Empresa, series).
3. **Terceros** (Clientes).
4. **Catalogo** (Productos, Impuestos).
5. **Facturacion** (Facturas emitidas: numeración, IVA+IRPF, invariantes F1–F6, auditoría).
6. **Gastos**.
7. **Tesoreria** (Cobros y pagos).
8. **Documentos** (PDF de factura + envío por email).
9. **Informes** (Dashboard, informes básicos, **libros de IVA**, **exportación gestoría**).

**Auditoria** es transversal: su infraestructura se levanta con el módulo 1 y cada módulo posterior
añade sus eventos auditables.

> Este método **sustituye** al "slice vertical" que habíamos acordado antes: pasamos a completar
> módulo por módulo.

---

## 15. ALXOR Core Pro y otros productos (fuera del MVP, solo para no cerrar puertas)

Pro añadirá (cuando lo pidan clientes reales): presupuestos, pedidos, albaranes, stock, compras
avanzadas, contabilidad completa, CRM, etc., y el envío real a **AEAT (VeriFactu/SII)** activando
los campos ya reservados. Questioner, Tuday y CostControl se integrarán vía **API + eventos**. Nada
de esto condiciona ni entra en el código del MVP.

---

## 16. Riesgos (seguridad, fiscales, integridad)

- **Fuga entre tenants** → doble barrera (query filter EF + RLS); norma: todo acceso por el
  `DbContext` con tenant fijado.
- **Auth** → hashing fuerte, expiración/rotación de JWT, rate limiting en login, verificación email.
- **Numeración con huecos/duplicada** → F1 transaccional + test de concurrencia.
- **Edición/borrado de factura emitida** → prohibido por diseño (F2).
- **Redondeo de impuestos** → política única por línea (F3) + tests.
- **No estar listo para VeriFactu/SII** → campos reservados + auditoría por eventos.
- **Saldos incoherentes** → estado derivado (P1/P2).
- **Migraciones destructivas** → versionadas, revisadas, sin borrado físico de documentos fiscales.
- **Libros de IVA / exportación gestoría incorrectos** → tests sobre casos reales (IVA por tipo,
  exentos, rectificativas) antes de dar por bueno el módulo Informes.

---

## 17. Verificación (por módulo)

Cada módulo se considera terminado cuando: `dotnet build` y `dotnet test` verdes (unit +
integración con Testcontainers), contrato OpenAPI publicado, documentación del módulo escrita, y —en
Facturacion/Tesoreria/Informes— se validan los criterios fiscales (numeración correlativa, cuadre de
importes, factura inmutable, saldos, libros de IVA).

---

## 18. Preguntas abiertas (no bloquean el diseño)

1. **Verificación de email**: ¿*stub* en el módulo Identidad y proveedor real (SMTP/SendGrid) en el
   módulo Documentos? (recomendado).
2. **PDF**: ¿confirmas **QuestPDF** (licencia gratuita para facturación pequeña) en Documentos?
3. **Exportación gestoría**: ¿formato objetivo? Opciones: **CSV** simple del libro de IVA (rápido,
   universal) ahora, y formatos de software contable (A3, Contasol, Sage…) más adelante. ¿Te vale
   CSV en el MVP?
4. **IRPF en facturas**: se incluye como **opción avanzada oculta** (autónomos que retienen).
   ¿Correcto que por defecto una factura no muestre IRPF salvo que el cliente/producto lo requiera?
