using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using PlataformaCreditos.Data;
using PlataformaCreditos.Models;

namespace PlataformaCreditos.Hubs;

[Authorize]
public class SolicitudesHub : Hub
{
    private readonly ApplicationDbContext _db;

    public SolicitudesHub(ApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<SolicitudEstadoActual?> ConsultarEstadoVigente(int solicitudId)
    {
        var usuarioId = Context.UserIdentifier;

        var solicitud = await _db.SolicitudesCredito
            .AsNoTracking()
            .Include(s => s.Cliente)
            .FirstOrDefaultAsync(s => s.Id == solicitudId && s.Cliente!.UsuarioId == usuarioId);

        if (solicitud == null)
        {
            return null;
        }

        return new SolicitudEstadoActual(
            solicitud.Id,
            solicitud.Estado,
            solicitud.MotivoRechazo);
    }
}

public record SolicitudEstadoActual(int SolicitudId, EstadoSolicitud Estado, string? MotivoRechazo);