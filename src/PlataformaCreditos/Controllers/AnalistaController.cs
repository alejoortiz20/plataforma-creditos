using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using PlataformaCreditos.Data;
using PlataformaCreditos.Hubs;
using PlataformaCreditos.Models;
using PlataformaCreditos.Services;

namespace PlataformaCreditos.Controllers;

[Authorize(Roles = "Analista")]
public class AnalistaController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly SolicitudCacheServicio _cacheSolicitudes;
    private readonly IHubContext<SolicitudesHub> _hub;
    private readonly NotificadorSolicitudesServicio _notificador;

    public AnalistaController(
        ApplicationDbContext db,
        SolicitudCacheServicio cacheSolicitudes,
        IHubContext<SolicitudesHub> hub,
        NotificadorSolicitudesServicio notificador)
    {
        _db = db;
        _cacheSolicitudes = cacheSolicitudes;
        _hub = hub;
        _notificador = notificador;
    }

    [HttpGet]
    public async Task<IActionResult> Index()
    {
        var pendientes = await _db.SolicitudesCredito
            .Include(s => s.Cliente)!
                .ThenInclude(c => c!.Usuario)
            .Where(s => s.Estado == EstadoSolicitud.Pendiente)
            .OrderBy(s => s.FechaSolicitud)
            .ToListAsync();

        return View(pendientes);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Aprobar(int id)
    {
        var solicitud = await _db.SolicitudesCredito
            .Include(s => s.Cliente)
            .FirstOrDefaultAsync(s => s.Id == id);

        if (solicitud == null)
        {
            return NotFound();
        }

        if (solicitud.Estado != EstadoSolicitud.Pendiente)
        {
            TempData["ErrorAnalista"] = $"La solicitud #{id} ya fue procesada (estado actual: {solicitud.Estado}).";
            return RedirectToAction(nameof(Index));
        }

        if (!ReglasCredito.EsAprobable(solicitud.MontoSolicitado, solicitud.Cliente!.IngresosMensuales))
        {
            TempData["ErrorAnalista"] = $"La solicitud #{id} no puede aprobarse: el monto solicitado ({solicitud.MontoSolicitado:C}) supera 5 veces los ingresos mensuales del cliente ({solicitud.Cliente.IngresosMensuales:C}).";
            return RedirectToAction(nameof(Index));
        }

        solicitud.Estado = EstadoSolicitud.Aprobado;
        await _db.SaveChangesAsync();

        await _cacheSolicitudes.InvalidarAsync(solicitud.Cliente!.UsuarioId);

        var evento = new SolicitudEstadoActual(solicitud.Id, solicitud.Estado, solicitud.MotivoRechazo);
        await _hub.Clients.User(solicitud.Cliente!.UsuarioId).SendAsync("SolicitudEstadoActualizado", evento);
        await _notificador.PublicarPieSocketAsync(solicitud.Cliente!.UsuarioId, solicitud.Id, solicitud.Estado.ToString(), solicitud.MotivoRechazo);

        TempData["ExitoAnalista"] = $"Solicitud #{id} aprobada.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Rechazar(int id, string? motivo)
    {
        var solicitud = await _db.SolicitudesCredito
            .Include(s => s.Cliente)
            .FirstOrDefaultAsync(s => s.Id == id);

        if (solicitud == null)
        {
            return NotFound();
        }

        if (solicitud.Estado != EstadoSolicitud.Pendiente)
        {
            TempData["ErrorAnalista"] = $"La solicitud #{id} ya fue procesada (estado actual: {solicitud.Estado}).";
            return RedirectToAction(nameof(Index));
        }

        if (string.IsNullOrWhiteSpace(motivo))
        {
            TempData["ErrorAnalista"] = $"Debe indicar un motivo de rechazo para la solicitud #{id}.";
            return RedirectToAction(nameof(Index));
        }

        solicitud.Estado = EstadoSolicitud.Rechazado;
        solicitud.MotivoRechazo = motivo.Trim();
        await _db.SaveChangesAsync();

        await _cacheSolicitudes.InvalidarAsync(solicitud.Cliente!.UsuarioId);

        var evento = new SolicitudEstadoActual(solicitud.Id, solicitud.Estado, solicitud.MotivoRechazo);
        await _hub.Clients.User(solicitud.Cliente!.UsuarioId).SendAsync("SolicitudEstadoActualizado", evento);
        await _notificador.PublicarPieSocketAsync(solicitud.Cliente!.UsuarioId, solicitud.Id, solicitud.Estado.ToString(), solicitud.MotivoRechazo);

        TempData["ExitoAnalista"] = $"Solicitud #{id} rechazada.";
        return RedirectToAction(nameof(Index));
    }
}