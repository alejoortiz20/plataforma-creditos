# Evidencia P2 — Catálogo de solicitudes ("Mis solicitudes")

Rama: `feature/catalogo-solicitudes`

## Implementación
- `Controllers/SolicitudesController.cs` con `[Authorize]`:
  - `GET /Solicitudes` — lista **solo** las solicitudes del usuario autenticado (filtra por `Cliente.UsuarioId == NameIdentifier`). Acepta filtros por query string: `Estado`, `MontoMinimo`, `MontoMaximo`, `FechaDesde`, `FechaHasta`.
  - `GET /Solicitudes/Detalle/{id}` — detalle, **validando que la solicitud pertenezca al usuario** (si no, `NotFound`).
- `Models/ViewModels/SolicitudesViewModel.cs` con validación server-side (`IValidatableObject` + `[Range]`):
  - Montos negativos → error "El monto mínimo/máximo no puede ser negativo."
  - `FechaDesde > FechaHasta` → error "La fecha inicial no puede ser posterior a la fecha final."
  - `MontoMinimo > MontoMaximo` → error.
- Vistas: `Views/Solicitudes/Index.cshtml` (filtros + tabla + badge de estado + Total) y `Views/Solicitudes/Detalle.cshtml` (muestra MotivoRechazo si Rechazado).
- Enlace "Mis solicitudes" en `_Layout.cshtml` solo para usuarios autenticados.
- Cuando `ModelState` es inválido se muestran los errores y se listan **todas** las solicitudes del usuario (no se filtran).

## Verificación funcional (login cliente1 @plataforma.test)
- Lista muestra **solo** 1 solicitud (ID 1, 15000, Pendiente) — sus propios datos.
- `Solicitudes?MontoMinimo=-5` → alerta "El monto mínimo no puede ser negativo." y lista completa.
- `Solicitudes?FechaDesde=2026-09-30&FechaHasta=2026-09-20` → alerta "La fecha inicial no puede ser posterior a la fecha final."
- `Solicitudes?Estado=Aprobado` → "No tienes solicitudes registradas." (cliente1 no tiene aprobadas).
- Detalle `Solicitudes/Detalle/1` muestra monto, fecha, estado.

## Verificación de aislamiento por usuario (login cliente2 @plataforma.test)
- `Solicitudes` muestra **solo** la solicitud ID 2 (12000, Aprobado). Cliente 1 no ve la de cliente 2 y viceversa.
- Los filtros y el detalle solo operan sobre solicitudes propias.

## Capturas
- `P2_lista.png` — filtros + tabla (cliente1, 1 solicitud Pendiente).
- `P2_detalle.png` — vista detalle.