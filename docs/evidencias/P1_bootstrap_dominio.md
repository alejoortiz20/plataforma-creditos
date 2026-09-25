# Evidencia P1 — Bootstrap del dominio

Rama: `feature/bootstrap-dominio` · PR: [rojo #1](https://github.com/alejoortiz20/plataforma-creditos/pull/1) (MERGED)

## Modelos y restricciones
- `Cliente`: Id, UsuarioId (FK AspNetUsers, Restrict), IngresosMensuales (CHECK > 0), Activo.
- `SolicitudCredito`: Id, ClienteId (FK, Cascade), MontoSolicitado (CHECK > 0), FechaSolicitud, Estado (Pendiente/Aprobado/Rechazado → int), MotivoRechazo.
- Índice único filtrado: 1 sola solicitud Pendiente por cliente (`[Estado] = 0`).
- `ReglasCredito.EsAprobable`: monto ≤ ingresos × 5 (se aplica en panel Analista).

## Migración y seed
- `dotnet ef migrations add InicialDominio` → tablas Clientes, SolicitudesCredito con CHECK + índice único filtrado.
- `dotnet ef database update` aplicado OK sobre `app.db`.
- Al arrancar, `DbInitializer` (usando `MigrateAsync`) crea:
  - Rol `Analista` + usuario `analista@plataforma.test`.
  - Cliente 1 (`cliente1@plataforma.test`, ingresos 5000, activo) → solicitud 15000 **Pendiente**.
  - Cliente 2 (`cliente2@plataforma.test`, ingresos 8000, activo) → solicitud 12000 **Aprobado**.

## Verificación en BD (SQLite)
```
Usuarios:  analista@..., cliente1@..., cliente2@... (EmailConfirmed=1)
Roles:     Analista  (asignado a analista@)
Clientes:  (1, 5000, 1, cliente1)   (2, 8000, 1, cliente2)
Solicitudes: (1, 1, 15000, Pendiente=0)   (2, 2, 12000, Aprobado=1)
```

## Prueba de humo
- `dotnet build` → 0 errores.
- App levantada con `dotnet run` → `http://localhost:5054` responde HTTP 200.
- Login efectivo con los usuarios de seed (Home MVC con Identity).

## Nota de configuración
- `AddDefaultIdentity` no registra `RoleManager`; se añadió `.AddRoles<IdentityRole>()` para el rol Analista.
- `Program.cs` debe tener `using PlataformaCreditos;` para resolver `DbInitializer`.