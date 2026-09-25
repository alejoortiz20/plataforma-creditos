using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PlataformaCreditos.Data;
using PlataformaCreditos.Models;
using PlataformaCreditos.Models.ViewModels;

namespace PlataformaCreditos.Controllers;

[Authorize]
public class SolicitudesController : Controller
{
    private readonly ApplicationDbContext _db;

    public SolicitudesController(ApplicationDbContext db)
    {
        _db = db;
    }

    [HttpGet]
    public async Task<IActionResult> Index(SolicitudesViewModel modelo)
    {
        var usuarioId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var consulta = _db.SolicitudesCredito
            .Include(s => s.Cliente)
            .Where(s => s.Cliente!.UsuarioId == usuarioId);

        if (!ModelState.IsValid)
        {
            modelo.Solicitudes = await consulta
                .OrderByDescending(s => s.FechaSolicitud)
                .ToListAsync();
            return View(modelo);
        }

        if (modelo.Estado.HasValue)
        {
            consulta = consulta.Where(s => s.Estado == modelo.Estado.Value);
        }

        if (modelo.MontoMinimo.HasValue)
        {
            consulta = consulta.Where(s => s.MontoSolicitado >= modelo.MontoMinimo.Value);
        }

        if (modelo.MontoMaximo.HasValue)
        {
            consulta = consulta.Where(s => s.MontoSolicitado <= modelo.MontoMaximo.Value);
        }

        if (modelo.FechaDesde.HasValue)
        {
            consulta = consulta.Where(s => s.FechaSolicitud >= modelo.FechaDesde.Value.Date);
        }

        if (modelo.FechaHasta.HasValue)
        {
            consulta = consulta.Where(s => s.FechaSolicitud <= modelo.FechaHasta.Value.Date.AddDays(1));
        }

        modelo.Solicitudes = await consulta
            .OrderByDescending(s => s.FechaSolicitud)
            .ToListAsync();

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

        _db.SolicitudesCredito.Add(new SolicitudCredito
        {
            ClienteId = cliente.Id,
            MontoSolicitado = modelo.MontoSolicitado,
            FechaSolicitud = DateTime.UtcNow,
            Estado = EstadoSolicitud.Pendiente,
        });

        await _db.SaveChangesAsync();

        TempData["MensajeExito"] =
            "Tu solicitud de crédito fue registrada y quedó pendiente de evaluación: S/ " +
            modelo.MontoSolicitado.ToString("N2") + ".";

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