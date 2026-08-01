using System.IdentityModel.Tokens.Jwt;
using AlxorCore.Api.Endpoints;
using AlxorCore.Identidad.Infraestructura;
using AlxorCore.Identidad.Infraestructura.Persistencia;
using AlxorCore.Identidad.Infraestructura.Seguridad;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);

// --- Módulos de ALXOR Core ---
builder.Services.AgregarModuloIdentidad(builder.Configuration);

// --- Autenticación JWT ---
// Conservamos los nombres originales de los claims (sub, email) sin remapearlos.
JwtSecurityTokenHandler.DefaultMapInboundClaims = false;

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer();

// La validación se configura desde las MISMAS opciones (IOptions<OpcionesJwt>) que usa la
// emisión de tokens, garantizando una única fuente de verdad para la clave, el emisor y la
// audiencia (evita desajustes de clave entre firma y validación).
builder.Services
    .AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
    .Configure<IOptions<OpcionesJwt>>((jwt, opcionesJwt) =>
    {
        jwt.MapInboundClaims = false;
        jwt.TokenValidationParameters = ConfiguracionJwt.ConstruirParametrosValidacion(opcionesJwt.Value);
    });

builder.Services.AddAuthorization();

// --- OpenAPI (API First) ---
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(opciones =>
{
    opciones.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "ALXOR Core API",
        Version = "v1",
        Description = "API del núcleo ALXOR Core. Módulo Identidad.",
    });

    var esquemaJwt = new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Introduce el token JWT (sin el prefijo 'Bearer').",
        Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" },
    };

    opciones.AddSecurityDefinition("Bearer", esquemaJwt);
    opciones.AddSecurityRequirement(new OpenApiSecurityRequirement { [esquemaJwt] = Array.Empty<string>() });
});

builder.Services.AddProblemDetails();

var app = builder.Build();

// En desarrollo aplicamos las migraciones automáticamente para facilitar el arranque.
if (app.Environment.IsDevelopment())
{
    using var ambito = app.Services.CreateScope();
    var contexto = ambito.ServiceProvider.GetRequiredService<IdentidadDbContext>();
    await contexto.Database.MigrateAsync().ConfigureAwait(false);

    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/salud", () => Results.Ok(new { estado = "ok" }))
    .WithTags("Salud")
    .WithName("Salud")
    .AllowAnonymous();

app.MapearIdentidad();

await app.RunAsync().ConfigureAwait(false);

/// <summary>Punto de entrada expuesto para las pruebas de integración (WebApplicationFactory).</summary>
public partial class Program;
