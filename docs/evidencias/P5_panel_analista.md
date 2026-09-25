# P5 - Panel del analista

Rama: `feature/panel-analista` · Merge en main: PR #5

## Implementación

- `AnalistaController` con `[Authorize(Roles = "Analista")]`, inyecta `ApplicationDbContext` y `SolicitudCacheServicio`.
- `GET /Analista`: lista solicitudes **Pendiente** (incluye Cliente y Usuario).
- `POST /Analista/Aprobar/{id}`:
  - 404 si no existe.
  - Rechaza si ya fue procesada ("La solicitud #N ya fue procesada (estado actual: ...)").
  - Validación de negocio: **no aprobar si MontoSolicitado > 5 × IngresosMensuales** (`ReglasCredito.EsAprobable`).
- `POST /Analista/Rechazar/{id}`:
  - **MotivoRechazo obligatorio** ("Debe indicar un motivo de rechazo...").
  - Rechaza si ya fue procesada.
- Al cambiar estado: **invalida el caché Redis** del usuario propietario (`_cacheSolicitudes.InvalidarAsync(Cliente.UsuarioId)`).
- Vista con tablas + modales Bootstrap (aprobar con warning si no es aprobable; rechazar con textarea requerido). Alerts de TempData.

## Pruebas (curl + redis)

| Caso | Resultado |
|---|---|
| cliente1 visita /Analista | AccessDenied (redirect) ✓ |
| Aprobar #4 (10000€, ingresos 1000€ → supera 5×) | Error: "supera 5 veces los ingresos mensuales" ✓ |
| Rechazar #4 sin motivo | Error: "Debe indicar un motivo de rechazo" ✓ |
| Rechazar #4 con motivo | "Solicitud #4 rechazada." ✓ |
| Rechazar de nuevo #4 | "ya fue procesada (Rechazado)" ✓ |
| Aprobar #1 (15000€, ingresos 5000€) | "Solicitud #1 aprobada." ✓ |
| Caché Redis cliente2 (key 9c97866f...) | Tras aprobar #5 → llave **eliminada** ✓ |

## Evidencias

- `P5_acceso_denegado.png` — cliente1 sin rol → AccessDenied.
- `P5_panel_analista.png` — panel con solicitudes pendientes.
- `P5_aprobacion_denegada.png` — error por superar 5× ingresos.
- `P5_panel_final.png` — panel tras aprobar/rechazar.
- Estado BD final: #1,#2,#3,#5 Aprobado; #4 Rechazado con motivo.

> **Nota:** las capturas de este documento fueron re-tomadas con el diseño renovado (UI "Fintech vibrante", rama `feature/diseno-fintech`).
