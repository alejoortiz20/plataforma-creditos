# P6 - Notificaciones en tiempo real (SignalR + PieSocket)

Rama: `feature/websocket-notificaciones` · PR asociado

## Implementación

- **Hub SignalR** `/hubs/solicitudes` con `[Authorize]` (Identity). Identidad SIEMPRE server-side
  (`Context.UserIdentifier`), jamás UsuarioId enviado desde el navegador.
- Al aprobar/rechazar en el panel del analista (después de **guardar en BD** e **invalidar caché Redis**):
  - `await _hub.Clients.User(UsuarioId).SendAsync("SolicitudEstadoActualizado", evento)` →
    evento `SolicitudEstadoActualizado` {SolicitudId, Estado, MotivoRechazo} **SOLO al usuario propietario**.
  - Publicación secundaria al canal privado PieSocket `solicitudes-{usuarioId}` (JWT HS256 firmado con API Secret).
- Vistas "Mis solicitudes" y Detalle conectan al hub (`_NotificacionesWss.cshtml`):
  - Badge de estado en vivo en cada fila (`data-solicitud-id`), indicador de conexión (`#wss-estado`),
    reconexión automática (`withAutomaticReconnect`) y **al reconectar consulta el estado vigente**
    (`ConsultarEstadoVigente(solicitudId)` devuelve null si la solicitud no pertenece al usuario).
- Serialización de enums como string en SignalR (`JsonStringEnumConverter`).

## Pruebas y evidencias (BD: solicitudes #1..#10)

1. **Sesión cliente1** (página aislada) ve su lista con `Conexión en tiempo real: Conectado`
   → screenshot `P6_cliente1_antes.png`.
2. **Evento sin recargar**: el analista aprueba la solicitud #9 (vía POST /Analista/Aprobar/9);
   la página del cliente1, SIN recargar, cambia el badge de `Pendiente` a `Aprobado` (`text-bg-success`)
   → screenshot `P6_cliente1_despues.png`. Se repitió con #10 (mismo resultado verificado por DOM).
3. **2º cliente NO recibe**: con cliente2 abierto en otra pestaña (contexto de cookies aislado),
   al aprobar la #10 de cliente1, la página de cliente2 NO cambió: solo lista sus propias
   solicitudes #5/#3/#2 (sin #10, todos intactos) → screenshot `P6_cliente2_no_recibe.png`.
4. **Conexión wss verificable**: `POST /hubs/solicitudes/negotiate?negotiateVersion=1` con cookie de
   Identity → HTTP 200, body `{"negotiateVersion":1,"connectionId":...,"availableTransports":[
   {"transport":"WebSockets",...},...]}` (headers incluyen `x-signalr-user-agent`).
5. **Conexión anónima rechazada**: mismo negotiate SIN cookie → **HTTP 401**.

## Orden de operaciones (requisito del enunciado)

Guardar estado en BD → invalidar caché Redis → emitir evento (SignalR + PieSocket). Mantener todas
las validaciones del panel (5× ingresos, motivo obligatorio, ya procesadas) sin cambios.

## Notas

- Publisher PieSocket: `POST https://free.blr2.piesocket.com/api/publish`
  {key, secret, channelId, message} (credenciales solo en appsettings.Development.json / env vars).
- El botón del modal de aprobación en Chrome MCP no dispara submit; las aprobaciones de la prueba
  se hicieron con POST equivalente (mismo endpoint, misma validación antiforgery, 302 a /Analista).