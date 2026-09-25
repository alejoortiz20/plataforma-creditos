# P7 — Notificaciones con CloudAMQP (RabbitMQ)

Rama: `feature/cloudmq-notificaciones` · PR #7 · Cola durable `solicitudes.notificaciones` (AMQPS, CloudAMQP)

## Arquitectura implementada

- **Publisher** (`Services/NotificacionPublisherService.cs`): tras guardar la solicitud Pendiente se publica JSON
  `{MessageId UUID, SolicitudId, UsuarioId, FechaEventoUtc}` con **publisher confirms**
  (`CreateChannelOptions(publisherConfirmationsEnabled: true, publisherConfirmationTrackingEnabled: true)`;
  el `await BasicPublishAsync` resuelve con el confirm del broker).
  **No se publica si falla validación o persistencia** (el publish va después de `SaveChangesAsync`).
  Si la publicación falla: la solicitud **se conserva**, se loguea el error y se muestra advertencia
  `TempData["Advertencia"]` al usuario; el README documenta el reenvío manual con el **mismo MessageId** (sin outbox).
- **Consumer** (`Services/NotificacionConsumerService.cs`, `BackgroundService`): consume con `autoAck:false`,
  prefetch 10. Guarda `Notificacion {Id, MessageId, SolicitudId, UsuarioId, Texto, FechaProcesamientoUtc}` en SQLite
  con texto *"Recibimos tu solicitud de crédito y está pendiente de evaluación"*.
  **ACK manual SOLO después de guardar**; unicidad de MessageId (índice único en BD + chequeo previo →
  redelivery confirma sin duplicar); JSON inválido → `BasicNack(requeue:false)` logueado
  "rechazado sin reencolar"; fallo de procesamiento → log y **sin confirmar** (sin reintentos infinitos,
  requeue manual documentado desde CloudAMQP).
- **Vista** "Mis notificaciones" (`Controllers/NotificacionesController.cs` + `Views/Notificaciones/Index.cshtml`):
  filtra por usuario autenticado; enlace en el layout.
- Env vars: `RabbitMq__ConnectionString` (amqps), `RabbitMq__QueueName=solicitudes.notificaciones`,
  `RabbitMq__ConsumerEnabled=true` (appsettings.json con placeholders; credenciales solo en
  `appsettings.Development.json` gitignored / env var en Render).

## Pruebas ejecutadas (2026-09-25, local localhost:5054)

| # | Prueba | Resultado |
|---|--------|-----------|
| A | App con `RabbitMq__ConsumerEnabled=false`, cliente2 crea solicitud | ✅ `cola: 1 consumers: 0` — mensaje **pendiente** en CloudAMQP |
| B | Reactivar consumidor (restart ON) | ✅ `cola: 0 consumers: 1` y **1 sola notificación** en BD (MessageId `159158fc-…`, Solicitud 12) |
| C | Reenviar **mismo MessageId** vía publicador | ✅ Notificaciones `2 → 2` (sin duplicar) — log: *"ya procesado; se confirma sin duplicar"* |
| D | Mensaje inválido (`{json roto !!!`) | ✅ Rechazado **sin reencolar** — log: *"Mensaje invalido, rechazado sin reencolar. DeliveryTag=3"* |

### Capturas de logs (app_out.log)
```
[RabbitMQ] Consumidor escuchando cola solicitudes.notificaciones.
[RabbitMQ] Notificacion guardada MessageId=159158fc-4888-4460-8816-957e916893d1 Solicitud=12.
[RabbitMQ] MessageId=159158fc-4888-4460-8816-957e916893d1 ya procesado; se confirma sin duplicar.
[RabbitMQ] Mensaje invalido, rechazado sin reencolar. DeliveryTag=3 Cuerpo={json roto !!!
```

### Estados verificados en BD
- Solicitudes: #11 (cliente1, 2000, Pendiente), #12 (cliente2, 1500, Pendiente).
- Notificaciones: 1 → #1 solicitud 11 (MID `56d47dbc-…`), 2 → #2 solicitud 12 (MID `159158fc-…`).

## Evidencia visual
- `P7_mis_notificaciones.png` — vista "Mis notificaciones" de cliente2 (MID de su solicitud 12).

## Reenvío manual documentado (README)
1. **Fallo de publicación**: reintentar publish con el **mismo MessageId** (la solicitud ya existe en BD).
2. **Fallo de procesamiento**: mensaje queda sin ACK → requeue manual desde CloudAMQP (no hay reintentos automátios infinitos).
3. **Mensaje inválido**: se descarta sin reencolar (evidencia en logs); corregir y publicar uno nuevo.
