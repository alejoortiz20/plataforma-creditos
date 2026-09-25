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
}