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
dotnet ef database update
dotnet run
```

## Migraciones

```bash
dotnet ef migrations add <Nombre>
dotnet ef database update
```

## Variables de entorno

Ningún secreto vive en el repo. Ver `docs/configuracion-env.md` (se crea en la fase de despliegue) para el listado completo.

| Variable | Descripción |
|---|---|
| `ConnectionStrings__DefaultConnection` | Cadena de conexión SQLite |
| `Redis__ConnectionString` | Conexión Redis (sesión/caché) |
| `RabbitMq__ConnectionString` | Conexión AMQPS a CloudAMQP |
| `RabbitMq__QueueName` | Nombre de la cola (`solicitudes.notificaciones`) |
| `RabbitMq__ConsumerEnabled` | Activa/desactiva el consumidor |

## Despliegue en Render

_URL por definir al completar la fase de despliegue._

## Evidencias

Capturas y registros de pruebas en [`docs/evidencias/`](docs/evidencias/).
