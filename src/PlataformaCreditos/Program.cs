using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using PlataformaCreditos;
using PlataformaCreditos.Data;
using PlataformaCreditos.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection") ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlite(connectionString));
builder.Services.AddDatabaseDeveloperPageExceptionFilter();

builder.Services.AddDefaultIdentity<IdentityUser>(options => options.SignIn.RequireConfirmedAccount = true)
    .AddRoles<IdentityRole>()
    .AddEntityFrameworkStores<ApplicationDbContext>();
builder.Services.AddControllersWithViews();
builder.Services.AddSignalR()
    .AddJsonProtocol(opciones => opciones.PayloadSerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter()));
builder.Services.AddHttpClient();
    builder.Services.AddScoped<NotificadorSolicitudesServicio>();
    builder.Services.AddScoped<NotificacionPublisherService>();
    builder.Services.AddHostedService<NotificacionConsumerService>();

var redisConnection = builder.Configuration["Redis:ConnectionString"];
if (string.IsNullOrWhiteSpace(redisConnection))
{
    builder.Services.AddDistributedMemoryCache();
    builder.Services.AddDataProtection();
    Console.WriteLine("[Redis] Sin cadena de conexion: se usa cache en memoria.");
}
else
{
    builder.Services.AddStackExchangeRedisCache(options =>
    {
        options.Configuration = redisConnection;
        options.InstanceName = "plataforma-creditos:";
    });
    Console.WriteLine("[Redis] Cache y sesion conectados a Redis.");
}

builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(30);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
    options.Cookie.Name = ".PlataformaCreditos.Session";
});

builder.Services.AddScoped<SolicitudCacheServicio>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseMigrationsEndPoint();
}
else
{
    app.UseExceptionHandler("/Home/Error");
}
app.UseRouting();

app.UseSession();

app.UseAuthorization();

app.MapStaticAssets();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();

app.MapRazorPages()
   .WithStaticAssets();

    app.MapHub<PlataformaCreditos.Hubs.SolicitudesHub>("/hubs/solicitudes");

await DbInitializer.InicializarAsync(app.Services, app.Configuration);

app.Run();
