using System.Text;
using System.Text.Json;
using RabbitMQ.Client;

namespace PlataformaCreditos.Services;

public class NotificacionPublisherService
{
    private readonly IConfiguration _config;
    private readonly ILogger<NotificacionPublisherService> _log;

    public NotificacionPublisherService(IConfiguration config, ILogger<NotificacionPublisherService> log)
    {
        _config = config;
        _log = log;
    }

    public string Cola => _config["RabbitMq:QueueName"] ?? "solicitudes.notificaciones";

    public async Task<string?> PublicarSolicitudRegistradaAsync(int solicitudId, string usuarioId, Guid? messageId = null)
    {
        var cs = _config["RabbitMq:ConnectionString"];
        if (string.IsNullOrWhiteSpace(cs))
        {
            _log.LogWarning("[RabbitMQ] Sin ConnectionString: se omite publicacion de SolicitudRegistrada para solicitud {Id}.", solicitudId);
            return null;
        }

        var id = messageId ?? Guid.NewGuid();
        var mensaje = new
        {
            MessageId = id,
            SolicitudId = solicitudId,
            UsuarioId = usuarioId,
            FechaEventoUtc = DateTime.UtcNow,
        };

        var cuerpo = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(mensaje));

        var factory = new ConnectionFactory { Uri = new Uri(cs) };
        using var connection = await factory.CreateConnectionAsync();
        await using var canal = await connection.CreateChannelAsync(
            new CreateChannelOptions(publisherConfirmationsEnabled: true, publisherConfirmationTrackingEnabled: true));

        // Con publisher confirms activos, la confirmacion del broker resuelve el await del publish.
        await canal.BasicPublishAsync(exchange: string.Empty, routingKey: Cola, body: cuerpo);

        _log.LogInformation("[RabbitMQ] Publicado SolicitudRegistrada MessageId={MessageId} Solicitud={Solicitud}", id, solicitudId);
        return id.ToString();
    }
}