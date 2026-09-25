using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PlataformaCreditos.Data;
using System.Security.Claims;

namespace PlataformaCreditos.Controllers;

[Authorize]
public class NotificacionesController : Controller
{
    private readonly ApplicationDbContext _db;

    public NotificacionesController(ApplicationDbContext db)
    {
        _db = db;
    }

    [HttpGet]
    public async Task<IActionResult> Index()
    {
        var usuarioId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;

        var lista = await _db.Notificaciones
            .Where(n => n.UsuarioId == usuarioId)
            .OrderByDescending(n => n.FechaProcesamientoUtc)
            .ToListAsync();

        return View(lista);
    }
}