using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using PlataformaCreditos.Data;
using PlataformaCreditos.Models;

namespace PlataformaCreditos;

public static class DbInitializer
{
    public static async Task InicializarAsync(IServiceProvider servicios, IConfiguration configuracion)
    {
        using var scope = servicios.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        await db.Database.MigrateAsync();

        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>();
        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();

        const string rolAnalista = "Analista";
        if (!await roleManager.RoleExistsAsync(rolAnalista))
        {
            await roleManager.CreateAsync(new IdentityRole(rolAnalista));
        }

        var passwordSeed = configuracion["Seed:Password"] ?? "PlataformaDemo2026*";

        var usuarioAnalista = await CrearUsuarioAsync(userManager, "analista@plataforma.test", passwordSeed);
        if (usuarioAnalista != null && !await userManager.IsInRoleAsync(usuarioAnalista, rolAnalista))
        {
            await userManager.AddToRoleAsync(usuarioAnalista, rolAnalista);
        }

        await CrearClienteAsync(
            db, userManager, "cliente1@plataforma.test", passwordSeed,
            ingresosMensuales: 5000m, activo: true,
            montoSolicitud: 15000m, estado: EstadoSolicitud.Pendiente);

        await CrearClienteAsync(
            db, userManager, "cliente2@plataforma.test", passwordSeed,
            ingresosMensuales: 8000m, activo: true,
            montoSolicitud: 12000m, estado: EstadoSolicitud.Aprobado);
    }

    private static async Task<IdentityUser?> CrearUsuarioAsync(
        UserManager<IdentityUser> userManager, string email, string password)
    {
        var usuario = await userManager.FindByEmailAsync(email);
        if (usuario != null)
        {
            return usuario;
        }

        usuario = new IdentityUser
        {
            UserName = email,
            Email = email,
            EmailConfirmed = true,
        };

        var resultado = await userManager.CreateAsync(usuario, password);
        return resultado.Succeeded ? usuario : null;
    }

    private static async Task CrearClienteAsync(
        ApplicationDbContext db,
        UserManager<IdentityUser> userManager,
        string email,
        string password,
        decimal ingresosMensuales,
        bool activo,
        decimal montoSolicitud,
        EstadoSolicitud estado)
    {
        var usuario = await CrearUsuarioAsync(userManager, email, password);
        if (usuario == null)
        {
            return;
        }

        if (await db.Clientes.AnyAsync(c => c.UsuarioId == usuario.Id))
        {
            return;
        }

        var cliente = new Cliente
        {
            UsuarioId = usuario.Id,
            IngresosMensuales = ingresosMensuales,
            Activo = activo,
        };

        db.Clientes.Add(cliente);
        await db.SaveChangesAsync();

        db.SolicitudesCredito.Add(new SolicitudCredito
        {
            ClienteId = cliente.Id,
            MontoSolicitado = montoSolicitud,
            FechaSolicitud = DateTime.UtcNow.AddDays(-5),
            Estado = estado,
        });

        await db.SaveChangesAsync();
    }
}