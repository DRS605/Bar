# ALXOR Core — Documento de diseño técnico y funcional (MVP)

> **Estado: propuesta para revisión.** Este documento es el entregable previo al código.
> No se ha escrito todavía el proyecto. Cuando lo apruebes, el primer incremento será un
> *slice vertical mínimo* (login → empresa → cliente → producto → factura → cobro).

---

## Contexto

ALXOR Core es un **ERP SaaS para autónomos y pequeñas empresas españolas**, con precio objetivo
~15 €/mes. Prioridades: simplicidad de uso ("sin manual"), coste operativo bajo, y un núcleo
pequeño pero sólido que pueda crecer durante años sin rehacerse. Es el núcleo de la futura
plataforma ALXOR; Questioner, Tuday y CostControl quedan **fuera** del código inicial (se
conectarán en el futuro vía API/eventos).

**Decisiones ya fijadas:**

| Decisión | Elección |
|---|---|
| Backend | **.NET 8 (C#)** |
| Base de datos | **PostgreSQL** |
| Multitenancy | **BD compartida + `company_id` + Row-Level Security (RLS)** |
| Primer incremento | **Slice vertical mínimo** (login → empresa → cliente → producto → factura → cobro) |
| Huella fiscal (VeriFactu/SII) | **Solo reservar campos** en el modelo; sin cálculo de encadenamiento aún |
| Frontend | Fuera de esta fase (API-first); UI se añade después |
| Idioma del código | **Español** (Factura, Cliente, Cobro…; tablas en español). UI/PDF/correo también en español |
| Impuestos del slice inicial | **IVA + IRPF** (retención) desde el primer incremento |

Principio rector transversal: **simplicidad visible, complejidad interna controlada; nada de
sobrearquitectura**. Se evitan patrones que no aporten valor hoy (sin microservicios, sin
CQRS/event-sourcing pesado, sin MediatR obligatorio).

---

## 1. Perfiles de usuario y tareas principales

| Perfil | Quién es | Tareas principales | Notas |
|---|---|---|---|
| **Propietario / Autónomo** | El propio dueño del negocio | Crear empresa, emitir facturas, registrar cobros/pagos, ver panel e informes básicos, gestionar usuarios | Es el rol por defecto al registrarse; ve todo de su empresa |
| **Administrativo / Facturación** | Persona de gestión | Clientes/proveedores, presupuestos→factura, cobros/pagos, gastos, envío por correo, importación/exportación CSV | No gestiona usuarios ni ajustes fiscales sensibles |
| **Solo lectura / Gestoría** | Asesor externo, contable | Consultar y exportar facturas, gastos e informes; **sin** editar | Preparado para el día que la gestoría necesite acceso |
| **Superadmin de plataforma** | Operación de ALXOR (interno) | Soporte, alta/baja de tenants, métricas globales | Fuera del alcance funcional del MVP salvo lo mínimo de operación |

En el MVP se implementan **3 roles de negocio** (Propietario, Administrativo, Solo lectura) más el
soporte técnico interno. Los permisos son granulares por debajo (ver §9) para poder crecer.

---

## 2. Flujos esenciales del MVP

1. **Alta y arranque**: registro → verificación de email → crear empresa → configurar datos
   fiscales (NIF, dirección, IVA por defecto, serie de facturación).
2. **Venta feliz (camino corto)**: crear cliente → **emitir factura** directa → generar PDF →
   enviar por correo → **registrar cobro**. *Este es el flujo que debe hacerse sin formación.*
3. **Venta completa (camino largo, opcional)**: presupuesto → aceptado → pedido de venta →
   albarán (entrega) → factura → cobro. Cada paso puede saltarse; se puede facturar directo.
4. **Compra/gasto**: registrar factura recibida o gasto → registrar pago.
5. **Cobros y pagos**: registrar cobro/pago total o parcial contra una factura; ver saldo pendiente.
6. **Cierre operativo diario**: panel con pendientes de cobro/pago, borradores, y totales del mes.
7. **Datos**: importar clientes/productos por CSV; exportar facturas/gastos/informes a CSV.
8. **Auditoría**: toda operación crítica queda registrada (quién, qué, cuándo).

Regla de UX transversal: **formulario simple por defecto**, con "opciones avanzadas" plegadas
(retenciones, recargo de equivalencia, tipo de operación, fechas fiscales alternativas, etc.).

---

## 3. Módulos y responsabilidades (monolito modular)

Cada módulo es un *bounded context* con fronteras claras. Se comunican por **interfaces de
aplicación** y **eventos de dominio in-process** (no llamadas directas a la BD de otro módulo).

| Módulo | Responsabilidad | Agregados principales |
|---|---|---|
| **Identidad** | Autenticación, usuarios, roles, permisos, membresías por empresa | Usuario, Rol, Membresia |
| **Organizacion** | Empresas (tenants), ajustes fiscales, series y numeración | Empresa, SerieNumeracion, AjustesEmpresa |
| **Terceros** | Clientes y proveedores | Cliente, Proveedor |
| **Catalogo** | Productos/servicios e impuestos | Producto, Impuesto |
| **Ventas** | Presupuestos, pedidos, albaranes, facturas emitidas, rectificativas | Presupuesto, PedidoVenta, Albaran, Factura |
| **Compras** | Facturas recibidas y gastos | FacturaRecibida, Gasto |
| **Cobros** | Cobros y pagos (total/parcial), saldo de documentos | Movimiento (cobro/pago) |
| **Documentos** | Generación de PDF y envío por correo (adaptadores) | — (servicios de aplicación + puertos) |
| **Informes** | Informes básicos, importación/exportación CSV | — (consultas de lectura) |
| **Auditoria** | Registro inmutable de operaciones críticas | RegistroAuditoria |
| **Nucleo (SharedKernel)** | Tipos comunes: `Dinero`, `TipoImpositivo`, `ContextoEmpresa`, `Resultado`, IDs | Value objects transversales |

**Integraciones externas (Documentos, y futuro AEAT/SII, pasarelas de correo)** siempre detrás de
**puertos (interfaces) en Application** con adaptadores en Infrastructure → desacople total.

---

## 4. Modelo de dominio (visión de agregados)

Notación: **Agregado** { entidades / value objects clave }. Todo agregado lleva `empresa_id`.

- **Empresa** { NIF, razón social, dirección fiscal, régimen IVA, moneda=EUR, país=ES }
- **Membresia** { usuario_id, empresa_id, rol, estado } — un usuario puede pertenecer a varias empresas.
- **Usuario** { email, hash contraseña, nombre, estado, email verificado } *(global, no por tenant)*.
- **Rol/Permiso** { código de permiso } — asignación de permisos a roles.
- **Cliente / Proveedor (Tercero)** { NIF/NIE/VAT, nombre, direcciones, email, IRPF por defecto, tipo de operación por defecto }
- **Producto** { referencia, nombre, tipo (bien/servicio), precio, impuesto por defecto, unidad }
- **Impuesto** { código, nombre, tipo (IVA/IRPF/RecargoEq), porcentaje, vigencia }
- **SerieNumeracion** { código, tipo de documento, ejercicio, patrón, **último número** }
- **Presupuesto** { tercero, líneas, estado, validez } → líneas { producto?, descripción, cantidad, precio, impuestos, descuento }
- **PedidoVenta** { origen presupuesto?, líneas, estado }
- **Albaran** { pedido?, líneas entregadas, estado }
- **Factura (emitida)** { serie+número, tercero (datos "congelados"), líneas, base, cuotas, retención, total, estado, **fecha emisión**, **fecha operación/devengo**, tipo de factura (ordinaria/rectificativa), factura rectificada (ref), tipo de operación, *campos VeriFactu reservados* }
- **FacturaRecibida** { proveedor, nº externo, base, cuota soportada, retención, fecha, estado }
- **Gasto** { proveedor?, concepto, importe, impuesto, fecha, estado }
- **Movimiento (cobro/pago)** { documento asociado (factura/gasto), sentido (cobro/pago), importe, fecha, método } — soporta parciales.
- **RegistroAuditoria** { actor, empresa, entidad, id, acción, timestamp, resumen de cambios }

**Value objects**: `Dinero` (importe + moneda, redondeo a 2 decimales), `TipoImpositivo`, `NIF/VAT`,
`Direccion`, `NumeroDocumento`, `RangoFechas`.

**Datos "congelados" en la factura**: al emitir, se copian NIF/nombre/dirección del tercero y
precios/impuestos de las líneas. La factura **no** depende de cambios posteriores en cliente o
producto (invariante fiscal clave).

### Campos VeriFactu/SII **reservados** (no calculados en MVP)

En `Factura` se crean ya, nullable, para no rehacer el núcleo al añadir SII/VeriFactu:
`huella` (hash del registro), `huella_anterior` (encadenamiento), `id_registro`,
`tipo_operacion`, `clave_regimen`, `fecha_expedicion`, `estado_envio_aeat`. Se dejan documentados
y sin lógica de cálculo (decisión: "solo reservar campos").

---

## 5. Invariantes y reglas críticas

**Facturación (las más importantes):**
- **F1 — Numeración correlativa sin huecos** por (serie + ejercicio). Se asigna el número **solo
  al emitir**, no en borrador. Asignación bajo bloqueo/transacción para evitar duplicados o saltos.
- **F2 — Factura emitida es inmutable**: una vez emitida no se edita ni se borra. Corrección solo
  vía **factura rectificativa** que referencia a la original.
- **F3 — Cuadre de importes**: `total = Σ(base_línea) + Σ(cuota_IVA) + Σ(recargo) − retención_IRPF`.
  Impuestos se calculan **por línea** y se agregan por tipo; redondeo a 2 decimales consistente.
- **F4 — Datos fiscales congelados** al emitir (ver §4).
- **F5 — Fechas fiscales coherentes**: `fecha_operación ≤ fecha_emisión`; el ejercicio de la serie
  se deriva de la fecha de emisión.
- **F6 — Rectificativa** obliga a: motivo, referencia a original, y signo coherente (importes que
  corrigen). No puede rectificar una factura inexistente ni una ya anulada por rectificación total.

**Cobros/pagos:**
- **P1** — La suma de cobros/pagos de un documento **no puede superar su total** (sin sobrepago).
- **P2** — Estado del documento (pendiente/parcial/pagado) se deriva del saldo, no se fija a mano.

**Multiempresa / seguridad:**
- **T1 — Aislamiento total**: toda lectura/escritura filtra por `empresa_id`; RLS como red de
  seguridad a nivel BD (ver §8).
- **T2** — Un usuario solo opera sobre empresas donde tiene `Membresia` activa.

**Impuestos españoles (MVP):**
- IVA: 21 % / 10 % / 4 % / 0 % (exento). IRPF retención: 7 % / 15 % (configurable).
  Recargo de equivalencia: 5,2 / 1,4 / 0,5 % (opción avanzada). Cálculo por línea con su tipo.

---

## 6. Esquema inicial de base de datos (PostgreSQL)

Convenciones: **PK `uuid` (v7, ordenable)**; `empresa_id uuid` en toda tabla de negocio;
`creado_en/actualizado_en timestamptz`; borrado lógico solo donde tenga sentido (no en facturas).
Índice compuesto `(empresa_id, …)` en todo acceso frecuente. **Nombres en español, `snake_case`**.

**Núcleo identidad/organización**
- `usuario` (id, email UNIQUE, hash_password, nombre, email_verificado, estado, timestamps)
- `empresa` (id, nif, razon_social, direccion_*, regimen_iva, moneda, pais, timestamps)
- `membresia` (id, usuario_id, empresa_id, rol, estado, UNIQUE(usuario_id, empresa_id))
- `rol` / `permiso` / `rol_permiso` (semilla de permisos; ver §9)
- `serie_numeracion` (id, empresa_id, tipo_documento, ejercicio, prefijo, siguiente_numero, UNIQUE(empresa_id, tipo_documento, ejercicio, prefijo))

**Terceros y catálogo**
- `cliente` (id, empresa_id, nif, nombre, direcciones(jsonb), email, irpf_defecto, …)
- `proveedor` (id, empresa_id, nif, nombre, …)
- `producto` (id, empresa_id, referencia, nombre, tipo, precio_unitario, impuesto_defecto_id, unidad)
- `impuesto` (id, empresa_id, codigo, tipo[IVA|IRPF|REQ], porcentaje, vigente_desde, vigente_hasta)

**Ventas**
- `presupuesto` / `pedido_venta` / `albaran` (id, empresa_id, tercero_id, estado, fechas, totales…)
- `factura` (id, empresa_id, serie_id, numero, fecha_emision, fecha_operacion, cliente_snapshot(jsonb),
  base, cuota_iva, retencion_irpf, total, estado, tipo_factura, rectifica_factura_id, tipo_operacion,
  **campos veri_factu nullable**)
- `linea_*` para cada documento (id, documento_id, producto_id?, descripcion, cantidad, precio_unitario,
  descuento, impuesto_id, base_linea, cuota_linea)

**Compras y cobros**
- `factura_recibida` (id, empresa_id, proveedor_id, numero_externo, base, cuota, retencion, fecha, estado)
- `gasto` (id, empresa_id, proveedor_id?, concepto, importe, impuesto_id, fecha, estado)
- `movimiento` (id, empresa_id, tipo_documento, documento_id, sentido[cobro|pago], importe, fecha, metodo)

**Auditoría**
- `registro_auditoria` (id, empresa_id, actor_usuario_id, entidad, entidad_id, accion, cambios(jsonb), en) — *append-only*.

RLS: política por `current_setting('app.empresa_actual')` en todas las tablas con `empresa_id`.

---

## 7. Estados y transiciones

**Presupuesto**: `borrador → enviado → aceptado | rechazado | caducado`
(desde `aceptado` puede generar Pedido o Factura).

**PedidoVenta**: `borrador → confirmado → (parcialmente) servido → cerrado | cancelado`.

**Albaran**: `borrador → entregado → facturado | cancelado`.

**Factura (emitida)**: `borrador → emitida → (cobro parcial) → pagada`;
además `emitida → rectificada` (por una rectificativa) y `anulada` solo vía rectificativa total.
*Nunca* se borra una emitida. Numeración se asigna en `borrador → emitida`.

**FacturaRecibida / Gasto**: `borrador → registrada → (pago parcial) → pagada | anulada`.

**Movimiento (cobro/pago)**: no tiene máquina de estados propia; su existencia mueve el estado de
saldo del documento asociado (`pendiente → parcial → pagado`), derivado (P2).

Cada transición válida se valida en el dominio; las inválidas lanzan error de dominio (no excepción
genérica) y quedan cubiertas por tests.

---

## 8. Estrategia multiempresa (multitenancy)

- **Modelo**: BD compartida, discriminador `empresa_id` en cada agregado de negocio.
- **Resolución de tenant**: de la sesión/JWT (empresa activa) → se fija en un `ContextoEmpresa`
  (scoped por request) y en la sesión de PostgreSQL vía `SET app.empresa_actual = <uuid>`.
- **Filtro por defecto (EF Core global query filter)**: todas las entidades de negocio filtran por
  `empresa_id == ContextoEmpresa.EmpresaId` automáticamente → imposible "olvidar" el WHERE.
- **Red de seguridad (defensa en profundidad)**: **RLS en PostgreSQL** usando `app.empresa_actual`.
  Aunque una consulta se escape del filtro de EF, la BD no devuelve filas de otro tenant.
- **Cambio de empresa**: un usuario con varias `membresias` cambia de empresa activa; se re-emite
  el contexto. Sin fuga de datos entre empresas.
- **Escalado futuro**: si un cliente grande requiere aislamiento, se puede migrar ese tenant a
  esquema/BD propia sin cambiar el modelo de dominio (el `empresa_id` ya existe).

---

## 9. Permisos básicos (autorización granular)

- **Permisos** como códigos finos: `factura.crear`, `factura.emitir`, `factura.leer`,
  `cobro.registrar`, `cliente.gestionar`, `producto.gestionar`, `informe.leer`, `empresa.ajustes`,
  `usuario.gestionar`, `datos.exportar`, `datos.importar`, etc.
- **Roles = conjuntos de permisos** (semilla):
  - *Propietario*: todos.
  - *Administrativo*: gestión operativa (clientes, productos, ventas, compras, cobros/pagos,
    import/export) **sin** `usuario.gestionar` ni `empresa.ajustes` sensibles.
  - *Solo lectura*: `*.leer` + `datos.exportar`.
- **Aplicación**: comprobación en la capa de aplicación (caso de uso) mediante un
  `IComprobadorPermisos`, no en controladores dispersos. Autorización basada en política de ASP.NET
  Core mapeada a permisos.
- **Multiempresa**: el permiso se evalúa **para la empresa activa** del usuario.
- Diseño preparado para permisos por recurso/serie en el futuro sin rehacer el modelo.

---

## 10. Estrategia de auditoría

- **Qué se audita**: operaciones críticas — emitir/rectificar factura, registrar cobro/pago,
  cambios de estado de documentos, alta/baja de usuarios y cambios de permisos, cambios de ajustes
  fiscales y de series.
- **Cómo**: los agregados emiten **eventos de dominio**; un manejador escribe en `registro_auditoria`
  (append-only) dentro de la **misma transacción** que la operación (consistencia).
- **Contenido**: actor, empresa, entidad+id, acción, diff resumido (jsonb), timestamp. Inmutable:
  sin update/delete sobre `registro_auditoria`.
- **No** es un log técnico: es un registro funcional consultable/exportable (base para
  cumplimiento antifraude futuro).
- Preparado para, más adelante, alimentar el **registro de facturación VeriFactu** (los eventos ya
  existen; solo faltaría calcular la huella).

---

## 11. Arquitectura técnica (sencilla)

**Estilo**: Monolito modular + Clean Architecture **ligera** por módulo. Un solo proceso
ASP.NET Core, un solo despliegue, una sola BD.

Capas por módulo (sin ceremonia innecesaria):

```
src/
  AlxorCore.Api            → host web, endpoints, auth, OpenAPI (Swagger)
  AlxorCore.Nucleo         → Dinero, Resultado, ContextoEmpresa, IDs, tipos comunes
  Modulos/
    Identidad/{Dominio, Aplicacion, Infraestructura}
    Organizacion/{Dominio, Aplicacion, Infraestructura}
    Terceros/{...}
    Catalogo/{...}
    Ventas/{Dominio, Aplicacion, Infraestructura}
    Compras/{...}
    Cobros/{...}
    Documentos/{Aplicacion(puertos), Infraestructura(PDF, correo)}
    Informes/{Aplicacion, Infraestructura}
    Auditoria/{...}
tests/
  <por módulo>.PruebasUnitarias / .PruebasIntegracion
```

- **Dominio**: entidades, value objects, invariantes, eventos de dominio. Sin dependencias de framework.
- **Aplicación**: casos de uso como servicios/handlers simples (clases con un método), interfaces
  (puertos), validación. **Sin MediatR obligatorio** (se puede añadir después si aporta).
- **Infraestructura**: EF Core (Npgsql), repositorios, adaptadores (PDF con QuestPDF, correo vía puerto),
  migraciones.
- **Api**: endpoints REST, **API First** con contrato OpenAPI, validación de entrada, mapeo a casos
  de uso, autenticación JWT, autorización por permisos.
- **Persistencia**: EF Core + migraciones; **global query filter** por tenant; **RLS** en BD.
- **Fronteras entre módulos**: un módulo no accede al `DbContext` de otro; se comunican por
  interfaces de aplicación públicas y eventos de dominio in-process. Preparado para *outbox* cuando
  haya eventos externos (ALXOR/otros productos), sin implementarlo aún.
- **Seguridad por diseño**: hashing de contraseñas (`PasswordHasher`), JWT, validación estricta, RLS,
  autorización por permiso, secretos fuera del código, HTTPS.
- **Testing**: unit tests de dominio (impuestos, numeración, estados, permisos) + integración con
  **PostgreSQL real vía Testcontainers**. Cobertura obligatoria en facturación, impuestos, permisos
  y estados (requisito del producto).
- **Coste operativo bajo**: 1 contenedor .NET + 1 PostgreSQL gestionado pequeño; sin colas ni
  servicios extra en el MVP.

---

## 12. Roadmap incremental

**Incremento 0 — Cimientos (parte del primer PR):** solución .NET, Nucleo, `ContextoEmpresa`,
EF Core + Npgsql, primera migración, RLS, autenticación JWT, esqueleto de OpenAPI, pipeline de
tests con Testcontainers, CI básico.

**Incremento 1 — Slice vertical mínimo (PRIMER entregable de código):**
Registro/login → crear empresa → crear cliente → crear producto → **emitir factura** (con
numeración, IVA+IRPF, congelado de datos, invariantes F1–F5) → **registrar cobro** → auditoría de
esas operaciones. Con tests de dominio e integración end-to-end. **Sin PDF/correo todavía**.

**Incremento 2 — Documentos y comunicación:** PDF de factura (QuestPDF), envío por correo,
panel principal básico.

**Incremento 3 — Ventas completas:** presupuestos → pedidos → albaranes → factura; rectificativas;
recargo de equivalencia.

**Incremento 4 — Compras y gastos:** facturas recibidas, gastos, pagos.

**Incremento 5 — Datos e informes:** import/export CSV, informes básicos, roles/solo lectura pulido.

**Incremento 6 — Preparación fiscal:** cálculo real de huella/encadenamiento (activar campos
reservados) — puerta de entrada a VeriFactu/SII **post-MVP**.

Cada incremento es desplegable y con tests verdes.

---

## 13. Riesgos (seguridad, fiscales, integridad de datos)

**Seguridad**
- *Fuga entre tenants*: mitigado con doble barrera (query filter EF + RLS). Riesgo si algún acceso
  usa SQL crudo → norma: todo acceso pasa por el `DbContext` con tenant fijado.
- *Auth*: robo de credenciales/JWT → hashing fuerte, expiración/rotación de tokens, rate limiting en
  login, verificación de email.
- *Autorización*: fallos de permisos → comprobación centralizada en Aplicación, tests de permisos.

**Fiscal / cumplimiento**
- *Numeración con huecos o duplicada* → invariante F1 con asignación transaccional; test de concurrencia.
- *Edición/borrado de factura emitida* → prohibido por diseño (F2); solo rectificativas.
- *Redondeo de impuestos incoherente* → política de redondeo única por línea (F3), tests de impuestos.
- *No estar listo para VeriFactu/SII* → campos reservados + auditoría por eventos ya presentes;
  activar cálculo de huella es aditivo, no rehace el núcleo.
- *Datos fiscales que cambian tras emitir* → congelado de snapshot (F4).

**Integridad de datos**
- *Saldos incoherentes* → estado derivado del saldo (P1/P2), no editable a mano.
- *Migraciones destructivas* → migraciones versionadas, revisadas, con backups; sin borrado físico
  de documentos fiscales.
- *Importación CSV sucia* → validación estricta y modo "previsualizar antes de confirmar".

---

## Primer incremento (detalle del código a escribir cuando apruebes)

1. **Solución y proyectos**: `AlxorCore.Api`, `AlxorCore.Nucleo`, módulos
   `Identidad`, `Organizacion`, `Terceros`, `Catalogo`, `Ventas`, `Cobros`, `Auditoria`
   (solo lo necesario para el slice), + proyectos de test.
2. **Infra base**: EF Core + Npgsql, `DbContext` con global query filter por `empresa_id`,
   primera migración, script RLS, `ContextoEmpresa` scoped.
3. **Auth**: registro, login, JWT, verificación de email (stub), `PasswordHasher`.
4. **Casos de uso del slice**: CrearEmpresa, CrearCliente, CrearProducto, EmitirFactura (numeración
   + **IVA e IRPF** + snapshot + invariantes F1–F5), RegistrarCobro; con auditoría vía eventos de
   dominio. Nombres de dominio en **español**.
5. **API REST + OpenAPI** para esos casos de uso.
6. **Tests**: unit (impuestos, numeración, estados, permisos) + integración con Testcontainers
   (flujo login→empresa→cliente→producto→factura→cobro).

---

## Verificación (cómo se probará el primer incremento)

- `dotnet build` y `dotnet test` verdes (unit + integración).
- Integración levanta **PostgreSQL vía Testcontainers**, aplica migraciones y ejecuta el flujo
  completo por la API.
- Prueba manual opcional: `docker compose up` (api + postgres) y recorrer el flujo con Swagger.
- Criterios: numeración correlativa correcta, importes/impuestos cuadran, factura emitida inmutable,
  cobro parcial deja estado "parcial", ningún dato visible de otra empresa (test de aislamiento).

---

## Preguntas abiertas (no bloquean el diseño)

1. **Verificación de email**: ¿proveedor real (SMTP/SendGrid/etc.) desde el inicio o *stub* en el
   slice y proveedor real en el Incremento 2? (recomiendo stub ahora).
2. **PDF**: ¿confirmas **QuestPDF** como librería (licencia gratuita para facturación pequeña)?
3. **Recargo de equivalencia**: IVA e IRPF entran ya en el slice (decidido). El **recargo de
   equivalencia** se deja como opción avanzada para el Incremento 3. ¿Conforme?
