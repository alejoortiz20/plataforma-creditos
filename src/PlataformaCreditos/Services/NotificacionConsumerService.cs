using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using PlataformaCreditos.Data;
using PlataformaCreditos.Models;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace PlataformaCreditos.Services;

public class NotificacionConsumerService : BackgroundService
{
    private readonly IConfiguration _config;
    private readonly IServiceScopeFactory _scopes;
    private readonly ILogger<NotificacionConsumerService> _log;

    public NotificacionConsumerService(
        IConfiguration config,
        IServiceScopeFactory scopes,
        ILogger<NotificacionConsumerService> log)
    {
        _config = config;
        _scopes = scopes;
        _log = log;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var cs = _config["RabbitMq:ConnectionString"];
        var habilitado = _config.GetValue("RabbitMq:ConsumerEnabled", true);

        if (string.IsNullOrWhiteSpace(cs))
        {
            _log.LogWarning("[RabbitMQ] Consumer deshabilitado: sin ConnectionString.");
            return;
        }

        if (!habilitado)
        {
            _log.LogWarning("[RabbitMQ] RabbitMq__ConsumerEnabled=false: el consumidor NO corre (mensajes quedan pendientes en la cola).");
            return;
        }

        var cola = _config["RabbitMq:QueueName"] ?? "solicitudes.notificaciones";

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var factory = new ConnectionFactory { Uri = new Uri(cs) };
                using var connection = await factory.CreateConnectionAsync(stoppingToken);
                await using var canal = await connection.CreateChannelAsync(cancellationToken: stoppingToken);

                await canal.QueueDeclareAsync(cola, durable: true, exclusive: false, autoDelete: false, cancellationToken: stoppingToken);
                await canal.BasicQosAsync(prefetchSize: 0, prefetchCount: 10, global: false, cancellationToken: stoppingToken);

                var consumidor = new AsyncEventingBasicConsumer(canal);
                consumidor.ReceivedAsync += OnMensajeRecibido;

                await canal.BasicConsumeAsync(cola, autoAck: false, consumer: consumidor, cancellationToken: stoppingToken);
                _log.LogInformation("[RabbitMQ] Consumidor escuchando cola {Cola}.", cola);

                await Task.Delay(Timeout.Infinite, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _log.LogError(ex, "[RabbitMQ] Error de conexion/consumo; reintento en 5s.");
                try { await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken); } catch (OperationCanceledException) { break; }
            }
        }
    }

    private async Task OnMensajeRecibido(object sender, BasicDeliverEventArgs ea)
    {
        var canal = ((AsyncEventingBasicConsumer)sender).Channel;
        string? messageId = null;

        try
        {
            var json = Encoding.UTF8.GetString(ea.Body.ToArray());

            MensajeSolicitudRegistrada? mensaje;
            try
            {
                mensaje = JsonSerializer.Deserialize<MensajeSolicitudRegistrada>(json);
            }
            catch (JsonException)
            {
                mensaje = null;
            }

            if (mensaje is null
                || string.IsNullOrWhiteSpace(mensaje.MessageId)
                || mensaje.SolicitudId <= 0
                || string.IsNullOrWhiteSpace(mensaje.UsuarioId))
            {
                // Mensaje invalido: se rechaza SIN reencolar, con evidencia en log.
                _log.LogError("[RabbitMQ] Mensaje invalido, rechazado sin reencolar. DeliveryTag={Tag} Cuerpo={Cuerpo}", ea.DeliveryTag, json);
                await canal.BasicNackAsync(ea.DeliveryTag, multiple: false, requeue: false);
                return;
            }

            messageId = mensaje.MessageId;

            using var scope = _scopes.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            // Unicidad de MessageId: si ya existe, se confirma sin duplicar (redelivery).
            var existe = await db.Notificaciones.AnyAsync(n => n.MessageId == messageId);
            if (existe)
            {
                _log.LogInformation("[RabbitMQ] MessageId={MessageId} ya procesado; se confirma sin duplicar.", messageId);
                await canal.BasicAckAsync(ea.DeliveryTag, multiple: false);
                return;
            }

            db.Notificaciones.Add(new Notificacion
            {
                MessageId = messageId,
                SolicitudId = mensaje.SolicitudId,
                UsuarioId = mensaje.UsuarioId,
                Texto = "Recibimos tu solicitud de crédito y está pendiente de evaluación",
                FechaProcesamientoUtc = DateTime.UtcNow,
            });
            await db.SaveChangesAsync();

            // ACK manual SOLO despues de guardar.
            await canal.BasicAckAsync(ea.DeliveryTag, multiple: false);
            _log.LogInformation("[RabbitMQ] Notificacion guardada MessageId={MessageId} Solicitud={Solicitud}.", messageId, mensaje.SolicitudId);
        }
        catch (Exception ex)
        {
            // Fallo de procesamiento: se registra, NO se confirma, sin reintentos infinitos
            // (el mensaje queda sin ack; se documenta reenvio/requeue manual desde CloudAMQP).
            _log.LogError(ex, "[RabbitMQ] Fallo al procesar MessageId={MessageId}; NO se confirma el mensaje. Requeue manual desde CloudAMQP si corresponde.", messageId);
        }
    }

    private sealed class MensajeSolicitudRegistrada
    {
        public string? MessageId { get; set; }
        public int SolicitudId { get; set; }
        public string? UsuarioId { get; set; }
        public DateTime FechaEventoUtc { get; set; }
    }
}