using System.Text.Json;
using Microsoft.Extensions.Caching.Distributed;
using PlataformaCreditos.Models;

namespace PlataformaCreditos.Services;

public class SolicitudCacheServicio
{
    private static readonly DistributedCacheEntryOptions Expiracion = new()
    {
        AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(60),
    };

    private readonly IDistributedCache _cache;

    public SolicitudCacheServicio(IDistributedCache cache)
    {
        _cache = cache;
    }

    private static string Clave(string usuarioId) => $"solicitudes:usuario:{usuarioId}";

    public async Task<List<SolicitudCredito>?> ObtenerAsync(string usuarioId)
    {
        var json = await _cache.GetStringAsync(Clave(usuarioId));
        if (string.IsNullOrEmpty(json))
        {
            return null;
        }

        return JsonSerializer.Deserialize<List<SolicitudCredito>>(json);
    }

    public Task EstablecerAsync(string usuarioId, List<SolicitudCredito> solicitudes)
    {
        return _cache.SetStringAsync(Clave(usuarioId), JsonSerializer.Serialize(solicitudes), Expiracion);
    }

    public Task InvalidarAsync(string usuarioId)
    {
        return _cache.RemoveAsync(Clave(usuarioId));
    }
}