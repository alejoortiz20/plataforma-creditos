namespace PlataformaCreditos.Models;

public class Cliente
{
    public int Id { get; set; }

    public required string UsuarioId { get; set; }

    public decimal IngresosMensuales { get; set; }

    public bool Activo { get; set; } = true;

    public Microsoft.AspNetCore.Identity.IdentityUser? Usuario { get; set; }

    public ICollection<SolicitudCredito> Solicitudes { get; set; } = new List<SolicitudCredito>();
}