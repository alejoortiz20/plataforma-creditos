using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using PlataformaCreditos.Models;

namespace PlataformaCreditos.Data;

public class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : IdentityDbContext(options)
{
    public DbSet<Cliente> Clientes => Set<Cliente>();

    public DbSet<SolicitudCredito> SolicitudesCredito => Set<SolicitudCredito>();

    public DbSet<Notificacion> Notificaciones => Set<Notificacion>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<Cliente>(entidad =>
        {
            entidad.ToTable(t => t.HasCheckConstraint("CK_Cliente_IngresosPositivos", "[IngresosMensuales] > 0"));

            entidad.HasOne(c => c.Usuario)
                .WithMany()
                .HasForeignKey(c => c.UsuarioId)
                .OnDelete(DeleteBehavior.Restrict);

            entidad.HasMany(c => c.Solicitudes)
                .WithOne(s => s.Cliente)
                .HasForeignKey(s => s.ClienteId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<SolicitudCredito>(entidad =>
        {
            entidad.ToTable(t => t.HasCheckConstraint("CK_SolicitudCredito_MontoPositivo", "[MontoSolicitado] > 0"));

            entidad.Property(s => s.Estado)
                .HasConversion<int>();

            entidad.HasIndex(s => new { s.ClienteId })
                .IsUnique()
                .HasFilter("[Estado] = 0");
        });

        builder.Entity<Notificacion>(entidad =>
        {
            entidad.HasIndex(n => n.MessageId).IsUnique();
        });
    }
}