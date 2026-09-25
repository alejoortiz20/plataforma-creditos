# P4 - Sesión y caché con Redis

Rama: `feature/sesion-redis` · Merge en main: PR #4

## Implementación

- **Session Redis**: `AddSession` con `AddStackExchangeRedisCache`, cookie `.PlataformaCreditos.Session`, IdleTimeout 30 min.
- `HttpContext.Session.SetInt32("UltimaSolicitudId", id)` y `SetString("UltimaSolicitudMonto", monto)` en `Detalle`.
- Layout: si existe `UltimaSolicitudId`, muestra link **"Ver última solicitud {monto}"**.
- **Caché Redis 60s** del listado del usuario: `SolicitudCacheServicio` (clave `solicitudes:usuario:{id}`, TTL 60s, JSON).
- `Index` consulta caché primero; si no existe, consulta BD y la pobla.
- Invalidación del caché al crear solicitud nueva (POST Crear → `InvalidarAsync`).

## Problema encontrado y solución

StackExchange.Redis no conectaba con URI `redis://default:...@host:19687`
(solamente python redis conectaba). Se cambió la cadena al formato:
`host:19687,password=...,abortConnect=False,connectTimeout=10000`.
Con eso la sesión emite `Set-Cookie: .PlataformaCreditos.Session`.

## Evidencias

1. **Sesión Redis**: GET /Solicitudes/Detalle/1 emite `Set-Cookie: .PlataformaCreditos.Session=...`.
   En la siguiente petición aparece en el nav: `Ver última solicitud 15.000,00`.
2. **Caché 60s**: visita a /Solicitudes crea llave
   `plataforma-creditos:solicitudes:usuario:53b224fe...` con TTL **57s** (≈60 - 1s transcurrido).
3. Screenshot: `P4_ultima_solicitud.png` (nav con "Ver última solicitud 15.000,00").
4. Invalidación: al crear nueva solicitud se elimina la llave del caché.

## Estado

App ejecutada en local con Redis remoto (son-group...db.redis.io:19687). Build 0 errores.