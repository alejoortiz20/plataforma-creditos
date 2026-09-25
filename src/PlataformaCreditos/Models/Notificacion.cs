namespace PlataformaCreditos.Models;

public class Notificacion
{
    public int Id { get; set; }

    public required string MessageId { get; set; }

    public int SolicitudId { get; set; }

    public required string UsuarioId { get; set; }

    public required string Texto { get; set; }

    public DateTime FechaProcesamientoUtc { get; set; }
}