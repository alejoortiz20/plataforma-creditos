# Plataforma de Créditos

Plataforma de evaluación de solicitudes de crédito construida con **ASP.NET Core MVC (.NET 10)**, **ASP.NET Core Identity**, **EF Core (SQLite)**, **Redis** (sesión y caché), **SignalR/WebSockets** (notificaciones en tiempo real), **RabbitMQ/CloudAMQP** (cola durable de notificaciones) y despliegue en **Render.com**.

## Requisitos

- .NET SDK 10.0.400 o superior
- `dotnet-ef` 10.x (`dotnet tool install -g dotnet-ef`)

## Ejecutar localmente

```bash
git clone https://github.com/alejoortiz20/plataforma-creditos.git
cd plataforma-creditos
dotnet restore
dotnet ef database update   # aplica migraciones (también se aplican solas al arrancar)
dotnet run --project src/PlataformaCreditos
```

La app queda en `http://localhost:5054` (ver `launchSettings.json`).

**Cuentas seed** (rol `Analista` incluido): `analista@plataforma.test`, `cliente1@plataforma.test`, `cliente2@plataforma.test` — contraseña `PlataformaDemo2026*`.

**Secretos locales** (no versionados, ver `.gitignore`): `src/PlataformaCreditos/appsettings.Development.json` con `Redis:ConnectionString`, `PieSocket:*` y `RabbitMq:ConnectionString`.

## Migraciones

```bash
dotnet ef migrations add <Nombre> --project src/PlataformaCreditos
dotnet ef database update --project src/PlataformaCreditos
```

Al arrancar la app, `DbInitializer` ejecuta `Migrate()` (idempotente), crea el rol `Analista`, los usuarios seed y los datos iniciales. Por eso la base se reconstruye sola en cada despliegue.

## Variables de entorno

Ningún secreto vive en el repo.

| Variable | Descripción | Ejemplo |
|---|---|---|
| `ASPNETCORE_ENVIRONMENT` | Entorno | `Production` |
| `ConnectionStrings__DefaultConnection` | Cadena de conexión SQLite | `DataSource=app.db;Cache=Shared` |
| `Redis__ConnectionString` | Redis sesión/caché (**formato StackExchange**, sin esquema `redis://`) | `host:puerto,password=…,abortConnect=False` |
| `RabbitMq__ConnectionString` | AMQPS CloudAMQP | `amqps://usuario:clave@host/vhost` |
| `RabbitMq__QueueName` | Cola durable | `solicitudes.notificaciones` |
| `RabbitMq__ConsumerEnabled` | Activa el consumidor `BackgroundService` | `true` |
| `PieSocket__ApiKey` / `PieSocket__Secret` / `PieSocket__Cluster` | Canal privado PieSocket (P6, respaldo de SignalR) | — |

**`ASPNETCORE_URLS` no se define como variable de entorno**: Render no expande `$PORT` en env vars. Se expande en el **start command**:

```sh
sh -c "ASPNETCORE_URLS=http://0.0.0.0:$PORT dotnet PlataformaCreditos.dll"
```

## Despliegue en Render

- **URL:** **https://plataforma-creditos-1qwp.onrender.com**
- **Rama:** `main` · auto-deploy activado · **1 sola instancia** (el consumidor de RabbitMQ corre dentro del mismo proceso, no necesita servicio aparte)
- **Runtime:** Docker (`Dockerfile` multi-stage: `sdk:10.0` → `aspnet:10.0`)
- **Región:** Oregon · plan free

### Persistencia de SQLite entre despliegues

Render usa un **filesystem efímero**: `app.db` no está en git (`.gitignore`) y cada despliegue/cold start arranca el contenedor nuevo. Como `DbInitializer` ejecuta `Migrate()` + seed al iniciar, **la base se reconstruye automáticamente** con el esquema y los datos seed en cada arranque.

Para conservar los datos reales entre despliegues hay que montar un **Render Disk** (plan de pago) apuntando a la ruta de `app.db`, o migrar la conexión a una base externa (PostgreSQL, etc.). En plan free la opción documentada es la reconstrucción automática con seed.

## Funcionalidades por pregunta (ramas/PRs)

| Pregunta | Rama | PR |
|---|---|---|
| P1 Dominio + restricciones + seed | `feature/bootstrap-dominio` | #1 |
| P2 Catálogo "Mis solicitudes" + filtros | `feature/catalogo-solicitudes` | #2 |
| P3 Formulario de solicitud | `feature/solicitudes` | #3 |
| P4 Sesión + caché Redis 60s | `feature/sesion-redis` | #4 |
| P5 Panel del analista | `feature/panel-analista` | #5 |
| P6 SignalR + PieSocket (tiempo real) | `feature/websocket-notificaciones` | #6 |
| P7 Cola CloudAMQP (publicador/consumidor) | `feature/cloudmq-notificaciones` | #7 |
| P8 Deploy en Render | `deploy/render` | #8 |

## Evidencias

Capturas y registros de pruebas en [`docs/evidencias/`](docs/evidencias/).
