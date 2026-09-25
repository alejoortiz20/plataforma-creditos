# P3 — Formulario de nueva solicitud de crédito (feature/solicitudes)

## Implementación
- `Models/ViewModels/NuevaSolicitudViewModel.cs`: `[Required]` + `[Range(0.01, double.MaxValue, ErrorMessage = "El monto solicitado debe ser mayor que cero.")]` sobre `MontoSolicitado`; propiedades `IngresosMensuales`, `MontoMaximoSolicitable`, `MaximoDefinido`.
- `Controllers/SolicitudesController.cs` (controlador ya existía con `[Authorize]` en P2):
  - `GET Crear`: muestra el formulario con ingresos y tope (10 × ingresos) del cliente autenticado.
  - `POST Crear` con `[ValidateAntiForgeryToken]`, validaciones server-side en este orden:
    1. Usuario autenticado → `[Authorize]` en el controlador.
    2. Cliente existe y está activo → error "No tienes un cliente asociado..." / "Tu cliente está inactivo...".
    3. ModelState (`monto > 0`) → "[Range]" server-side.
    4. No más de 1 solicitud Pendiente por cliente → `AnyAsync(... Estado == Pendiente)` → "Ya tienes una solicitud de crédito pendiente de evaluación..." (además del índice único filtrado en BD de P1).
    5. Monto ≤ 10 × ingresos → `ReglasCredito.MultiplicadorMaximoSolicitud = 10m` → "No puedes solicitar un monto mayor a ... (10 veces tus ingresos mensuales)".
  - Feedback: errores en la misma vista (`asp-validation-summary="All"`), éxito con `TempData["MensajeExito"]` + redirect (PRG) → alerta verde en la misma vista del formulario.
- `Views/Solicitudes/Crear.cshtml`: formulario con monto, tope informativo, botón deshabilitado lógicamente sin cliente activo.
- `Views/_ViewImports.cshtml`: añadido `@using PlataformaCreditos.Models.ViewModels`.
- `Views/Shared/_Layout.cshtml`: enlace "Nueva solicitud" en la barra de navegación (solo autenticados).

## Pruebas (Chrome DevTools, http://localhost:5054)
1. **Formulario OK** (cliente2, ingresos 8.000 €): muestra tope 80.000 € = 10× ingresos.
2. **Monto > 10× ingresos (90.000)** → server-side: "No puedes solicitar un monto mayor a 80.000,00 € (10 veces tus ingresos mensuales)." → `P3_errores_formulario.png`.
3. **Éxito (20.000)** → alerta verde "Tu solicitud de crédito fue registrada y quedó pendiente de evaluación: S/ 20.000,00." (PRG) → solicitud #3 creada (verificado en SQLite: `(3, 2, 20000.0, 0)`) y visible en Mis solicitudes → `P3_solicitud_creada.png`, `P3_detalle_creada.png`.
4. **Ya tiene Pendiente (nuevo intento)** → server-side: "Ya tienes una solicitud de crédito pendiente de evaluación. Espera a que sea procesada."
5. **Sin cliente asociado** (usuario registrado nuevo sin fila en Clientes) → "No tienes un cliente asociado en la plataforma. Contacta con soporte." + "No puedes registrar solicitudes porque no tienes un cliente activo asociado." → `P3_sin_cliente.png`.
6. **Monto negativo (-100)** → "[Range]" client + server: "El monto solicitado debe ser mayor que cero."
7. `dotnet build`: 0 errores, 0 warnings.

## Evidencias
- `docs/evidencias/P3_errores_formulario.png`
- `docs/evidencias/P3_solicitud_creada.png`
- `docs/evidencias/P3_detalle_creada.png`
- `docs/evidencias/P3_sin_cliente.png`


> **Nota:** las capturas de este documento fueron re-tomadas con el diseño renovado (UI "Fintech vibrante", rama `feature/diseno-fintech`).
