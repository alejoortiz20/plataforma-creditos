# P8 — Deploy en Render.com

Rama: `deploy/render` · PRs #8 (infra) y #9 (evidencias) · Servicio `srv-dar1op7lot8c73e4thog`

## URL pública

**https://plataforma-creditos-1qwp.onrender.com**

## Configuración del servicio

| Campo | Valor |
|---|---|
| Tipo | Web Service (Docker) |
| Repo / rama | `github.com/alejoortiz20/plataforma-creditos` / `main` |
| Dockerfile | `./Dockerfile` (multi-stage `sdk:10.0` → `aspnet:10.0`), contexto `.` |
| **Start command** | `sh -c "ASPNETCORE_URLS=http://0.0.0.0:$PORT dotnet PlataformaCreditos.dll"` (expande `$PORT` en ejecución, NO en env var) |
| Plan / región / instancias | free / Oregon / **1 sola instancia** (consumidor RabbitMQ dentro del mismo proceso) |
| Auto-deploy | sí (commit a `main`) |
| Health check | `/` |

## Variables de entorno (vía API, sin secretos en el repo)

`ASPNETCORE_ENVIRONMENT=Production`, `ConnectionStrings__DefaultConnection=DataSource=app.db;Cache=Shared`,
`Redis__ConnectionString` (formato StackExchange host:puerto,password=…), `RabbitMq__ConnectionString` (amqps),
`RabbitMq__QueueName=solicitudes.notificaciones`, `RabbitMq__ConsumerEnabled=true`,
`PieSocket__ApiKey/Secret/Cluster`.

Nota: el primer contenedor arrancó antes de inyectar las env vars → se ejecutó un **redeploy** (`forceLatest`) y quedó `live`.

## Pruebas en producción (2026-09-25)

| # | Prueba | Resultado |
|---|---|---|
| 1 | Home | ✅ HTTP 200 |
| 2 | Login `cliente1` (Identity) | ✅ autenticado |
| 3 | Sesión Redis: visitar Detalle/1 → enlace "Ver última solicitud" persiste en Home | ✅ |
| 4 | `/hubs/solicitudes/negotiate` anónimo (POST) | ✅ **401** |
| 5 | Validación server-side: cliente1 (pendiente seed) no puede crear → alerta de éxito sin MessageId | ✅ |
| 6 | Analista aprueba solicitud en `/Analista` (prod) | ✅ HTTP 200 |
| 7 | cliente2 crea solicitud → **MessageId publicado** `e60933a7-f50b-4f93-a899-139da775d481` + alerta "Notificación en cola" | ✅ |
| 8 | **Consumidor CloudAMQP en prod**: "Mis notificaciones" muestra el MID en **4 s** | ✅ |
| 9 | "Mis solicitudes" lista 6 filas (filtros + caché Redis) | ✅ |

## Persistencia de SQLite (documentada en README)

Filesystem efímero de Render: `app.db` no está en git; `DbInitializer` ejecuta `Migrate()` + seed en cada
arranque, así que **cada despliegue/cold start reconstruye la base con datos seed**.
Para conservar datos reales: montar un **Render Disk** (plan de pago) o usar una BD externa.

## Evidencia visual
- `P8_prod_notificaciones.png` — "Mis notificaciones" en producción con la notificación consumida desde la cola.

## Re-verificación con diseño renovado (2026-09-25, PR #10)

Tras fusionar el rediseño "Fintech vibrante" (`feature/diseno-fintech`, PR #10) Render desplegó
automáticamente desde `main`:

- `/css/site.css` en producción contiene las variables del nuevo sistema (`--pc-primary`, gradientes índigo/violeta) → **deploy con UI nueva confirmado por HTTP**.
- Flujo completo re-ejecutado en prod con la nueva interfaz: login analista → aprobar solicitud #3 →
  login cliente2 → crear solicitud de **S/ 1,800.00** → alerta con **MessageId `0aadde7b-9b36-4df8-868c-210839456cd1`**
  → "Mis notificaciones" muestra la notificación procesada en ~15 s (solo el consumidor de prod activo).
- `P8_prod_notificaciones.png` re-tomada con el diseño nuevo (timeline de notificaciones, navbar gradiente).

Nota operativa: durante las pruebas la app local y producción comparten la misma cola CloudAMQP; si
ambos consumidores están activos, el mensaje lo toma cualquiera de los dos (ACK exclusivo). Para
evidencias de prod conviene dejar la app local detenida.
