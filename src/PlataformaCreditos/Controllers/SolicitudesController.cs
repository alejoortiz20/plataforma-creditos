using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PlataformaCreditos.Data;
using PlataformaCreditos.Models;
using PlataformaCreditos.Models.ViewModels;
using PlataformaCreditos.Services;

namespace PlataformaCreditos.Controllers;

[Authorize]
public class SolicitudesController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly SolicitudCacheServicio _cacheSolicitudes;
    private readonly NotificacionPublisherService _publicador;
    private readonly ILogger<SolicitudesController> _logger;

    public SolicitudesController(
        ApplicationDbContext db,
        SolicitudCacheServicio cacheSolicitudes,
        NotificacionPublisherService publicador,
        ILogger<SolicitudesController> logger)
    {
        _db = db;
        _cacheSolicitudes = cacheSolicitudes;
        _publicador = publicador;
        _logger = logger;
    }

    [HttpGet]
    public async Task<IActionResult> Index(SolicitudesViewModel modelo)
    {
        var usuarioId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;

        var baseList = await _cacheSolicitudes.ObtenerAsync(usuarioId);
        if (baseList == null)
        {
            baseList = await _db.SolicitudesCredito
                .Where(s => s.Cliente!.UsuarioId == usuarioId)
                .OrderByDescending(s => s.FechaSolicitud)
                .ToListAsync();
            await _cacheSolicitudes.EstablecerAsync(usuarioId, baseList);
        }

        if (!ModelState.IsValid)
        {
            modelo.Solicitudes = baseList;
            return View(modelo);
        }

        var filtradas = baseList.AsEnumerable();

        if (modelo.Estado.HasValue)
        {
            filtradas = filtradas.Where(s => s.Estado == modelo.Estado.Value);
        }

        if (modelo.MontoMinimo.HasValue)
        {
            filtradas = filtradas.Where(s => s.MontoSolicitado >= modelo.MontoMinimo.Value);
        }

        if (modelo.MontoMaximo.HasValue)
        {
            filtradas = filtradas.Where(s => s.MontoSolicitado <= modelo.MontoMaximo.Value);
        }

        if (modelo.FechaDesde.HasValue)
        {
            filtradas = filtradas.Where(s => s.FechaSolicitud >= modelo.FechaDesde.Value.Date);
        }

        if (modelo.FechaHasta.HasValue)
        {
            filtradas = filtradas.Where(s => s.FechaSolicitud <= modelo.FechaHasta.Value.Date.AddDays(1));
        }

        modelo.Solicitudes = filtradas
            .OrderByDescending(s => s.FechaSolicitud)
            .ToList();

        return View(modelo);
    }

    [HttpGet]
    public async Task<IActionResult> Detalle(int id)
    {
        var usuarioId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var solicitud = await _db.SolicitudesCredito
            .Include(s => s.Cliente)
            .FirstOrDefaultAsync(s => s.Id == id && s.Cliente!.UsuarioId == usuarioId);

        if (solicitud == null)
        {
            return NotFound();
        }

        HttpContext.Session.SetInt32("UltimaSolicitudId", solicitud.Id);
        HttpContext.Session.SetString("UltimaSolicitudMonto", solicitud.MontoSolicitado.ToString("N2"));

        return View(solicitud);
    }

    [HttpGet]
    public async Task<IActionResult> Crear()
    {
        var (cliente, errores) = await CargarClienteParaFormularioAsync();

        var modelo = new NuevaSolicitudViewModel();
        if (cliente != null)
        {
            modelo.IngresosMensuales = cliente.IngresosMensuales;
            modelo.MontoMaximoSolicitable = cliente.IngresosMensuales * ReglasCredito.MultiplicadorMaximoSolicitud;
            modelo.MaximoDefinido = true;
        }

        foreach (var error in errores)
        {
            ModelState.AddModelError(string.Empty, error);
        }

        return View(modelo);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Crear(NuevaSolicitudViewModel modelo)
    {
        var (cliente, errores) = await CargarClienteParaFormularioAsync();
        if (cliente != null)
        {
            modelo.IngresosMensuales = cliente.IngresosMensuales;
            modelo.MontoMaximoSolicitable = cliente.IngresosMensuales * ReglasCredito.MultiplicadorMaximoSolicitud;
            modelo.MaximoDefinido = true;
        }

        foreach (var error in errores)
        {
            ModelState.AddModelError(string.Empty, error);
        }

        if (!ModelState.IsValid)
        {
            return View(modelo);
        }

        if (cliente == null)
        {
            return View(modelo);
        }

        var yaTienePendiente = await _db.SolicitudesCredito
            .AnyAsync(s => s.ClienteId == cliente.Id && s.Estado == EstadoSolicitud.Pendiente);

        if (yaTienePendiente)
        {
            ModelState.AddModelError(string.Empty,
                "Ya tienes una solicitud de crédito pendiente de evaluación. Espera a que sea procesada.");
            return View(modelo);
        }

        if (modelo.MontoSolicitado > modelo.MontoMaximoSolicitable)
        {
            ModelState.AddModelError(string.Empty,
                $"No puedes solicitar un monto mayor a {modelo.MontoMaximoSolicitable:C} (10 veces tus ingresos mensuales).");
            return View(modelo);
        }

        var nueva = new SolicitudCredito
        {
            ClienteId = cliente.Id,
            MontoSolicitado = modelo.MontoSolicitado,
            FechaSolicitud = DateTime.UtcNow,
            Estado = EstadoSolicitud.Pendiente,
        };
        _db.SolicitudesCredito.Add(nueva);

        await _db.SaveChangesAsync();

        await _cacheSolicitudes.InvalidarAsync(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

        TempData["MensajeExito"] =
            "Tu solicitud de crédito fue registrada y quedó pendiente de evaluación: S/ " +
            modelo.MontoSolicitado.ToString("N2") + ".";

        // Publicacion SOLO despues de validar y persistir correctamente.
        try
        {
            var usuarioId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
            var messageId = await _publicador.PublicarSolicitudRegistradaAsync(nueva.Id, usuarioId);
            if (messageId != null)
            {
                TempData["MensajeExito"] += " Notificación en cola (MessageId: " + messageId + ").";
            }
        }
        catch (Exception ex)
        {
            // Fallo de publicacion: la solicitud YA existe; se conserva, se registra el error
            // y se advierte. Reenvio manual con el mismo MessageId (ver README P7).
            _logger.LogError(ex, "[RabbitMQ] Fallo al publicar SolicitudRegistrada para solicitud {Id}. Reenviar con mismo MessageId.", nueva.Id);
            TempData["Advertencia"] =
                "Tu solicitud se registró correctamente, pero no se pudo publicar la notificación en la cola. " +
                "Reintenta más tarde (reenvío manual con el mismo MessageId).";
        }

        return RedirectToAction(nameof(Crear));
    }

    private async Task<(Cliente? cliente, List<string> errores)> CargarClienteParaFormularioAsync()
    {
        var usuarioId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var errores = new List<string>();

        var cliente = await _db.Clientes.FirstOrDefaultAsync(c => c.UsuarioId == usuarioId);
        if (cliente == null)
        {
            errores.Add("No tienes un cliente asociado en la plataforma. Contacta con soporte.");
        }
        else if (!cliente.Activo)
        {
            errores.Add("Tu cliente está inactivo y no puede solicitar créditos.");
        }

        return (cliente, errores);
    }
}