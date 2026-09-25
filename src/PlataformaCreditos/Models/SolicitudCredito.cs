namespace PlataformaCreditos.Models;

public enum EstadoSolicitud
{
    Pendiente = 0,
    Aprobado = 1,
    Rechazado = 2
}

public class SolicitudCredito
{
    public int Id { get; set; }

    public int ClienteId { get; set; }

    public decimal MontoSolicitado { get; set; }

    public DateTime FechaSolicitud { get; set; }

    public EstadoSolicitud Estado { get; set; } = EstadoSolicitud.Pendiente;

    public string? MotivoRechazo { get; set; }

    public Cliente? Cliente { get; set; }
}