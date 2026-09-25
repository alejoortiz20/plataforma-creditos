using System.Text;
using System.Text.Json;

namespace PlataformaCreditos.Services;

public class NotificadorSolicitudesServicio
{
    private readonly IConfiguration _configuracion;
    private readonly IHttpClientFactory _httpClientFactory;

    public NotificadorSolicitudesServicio(IConfiguration configuracion, IHttpClientFactory httpClientFactory)
    {
        _configuracion = configuracion;
        _httpClientFactory = httpClientFactory;
    }

    public async Task PublicarPieSocketAsync(string usuarioId, int solicitudId, string estado, string? motivoRechazo)
    {
        var apiKey = _configuracion["PieSocket:ApiKey"];
        var secret = _configuracion["PieSocket:Secret"];
        var cluster = _configuracion["PieSocket:Cluster"];

        if (string.IsNullOrWhiteSpace(apiKey) || string.IsNullOrWhiteSpace(secret) || string.IsNullOrWhiteSpace(cluster))
        {
            return;
        }

        var canal = $"solicitudes-{usuarioId}";
        var mensaje = JsonSerializer.Serialize(new
        {
            SolicitudId = solicitudId,
            Estado = estado,
            MotivoRechazo = motivoRechazo,
        });

        var cliente = _httpClientFactory.CreateClient();
        var contenido = new StringContent(
            JsonSerializer.Serialize(new
            {
                key = apiKey,
                secret,
                channelId = canal,
                message = mensaje,
            }),
            Encoding.UTF8,
            "application/json");

        await cliente.PostAsync($"https://{cluster}.piesocket.com/api/publish", contenido);
    }

    public string CrearJwtPieSocket(string usuarioId, string canal)
    {
        var apiKey = _configuracion["PieSocket:ApiKey"];
        var secret = _configuracion["PieSocket:Secret"];

        var header = Base64UrlEncode(JsonSerializer.SerializeToUtf8Bytes(new { alg = "HS256", typ = "JWT" }));
        var payload = Base64UrlEncode(JsonSerializer.SerializeToUtf8Bytes(new
        {
            apiKey,
            channelId = canal,
            uid = usuarioId,
            exp = DateTimeOffset.UtcNow.AddHours(2).ToUnixTimeSeconds(),
        }));

        var firmaEncabezado = header + "." + payload;
        var firma = Base64UrlEncode(HmacSha256(secret ?? "", firmaEncabezado));

        return firmaEncabezado + "." + firma;
    }

    private static string Base64UrlEncode(byte[] bytes)
    {
        return Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    }

    private static byte[] HmacSha256(string clave, string valor)
    {
        using var hmac = new System.Security.Cryptography.HMACSHA256(Encoding.UTF8.GetBytes(clave));
        return hmac.ComputeHash(Encoding.UTF8.GetBytes(valor));
    }
}