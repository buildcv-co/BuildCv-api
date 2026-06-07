using Asp.Versioning;
using BuildCv.Api.Endpoints;
using BuildCv.Api.Errors;
using BuildCv.Api.Health;
using BuildCv.Api.Security;
using BuildCv.Application;
using BuildCv.Infrastructure;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Scalar.AspNetCore;
using Serilog;
using Serilog.Events;

var builder = WebApplication.CreateBuilder(args);

// Logging estructurado (Serilog). Solo metadatos: nunca se registra el contenido
// del CV ni de la vacante (privacidad por diseño, NFR-002).
builder.Services.AddSerilog((_, lc) => lc
    .MinimumLevel.Information()
    .MinimumLevel.Override("Microsoft.AspNetCore", LogEventLevel.Warning)
    .Enrich.FromLogContext()
    .WriteTo.Console());

// Errores uniformes: ProblemDetails (RFC 9457) + manejador global de excepciones.
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();

// Documentación viva de la API (OpenAPI + Scalar).
builder.Services.AddOpenApi();

// Versionado de la API (/api/v1). Los endpoints versionados llegan en M1.
builder.Services.AddApiVersioning(options =>
{
    options.DefaultApiVersion = new ApiVersion(1, 0);
    options.AssumeDefaultVersionWhenUnspecified = true;
    options.ReportApiVersions = true;
});

// Health checks: 'live' (proceso vivo) y 'ready' (listo para servir, incluye config de IA).
builder.Services.AddHealthChecks()
    .AddCheck("self", () => HealthCheckResult.Healthy("Proceso vivo."), tags: ["live", "ready"])
    .AddCheck<AiConfigHealthCheck>("ai-config", tags: ["ready"]);

// CORS para el frontend. El BFF de Next.js llama al backend en same-origin (server-to-server),
// por lo que CORS solo habilita pruebas directas/Scalar; los orígenes vienen de configuración.
var corsOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
builder.Services.AddCors(options => options.AddPolicy("frontend", policy => policy
    .WithOrigins(corsOrigins)
    .AllowAnyHeader()
    .AllowAnyMethod()));

// Composición por capas. El dominio es PURO: sus servicios se registran desde Application.
builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

// Anti-abuso por IP (rate limiting nativo).
builder.Services.AddAppRateLimiting();

// Detrás de un proxy inverso (Render/Vercel): recuperar la IP real del cliente,
// indispensable para el rate limiting por IP que se añade en M1.
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    options.KnownIPNetworks.Clear();
    options.KnownProxies.Clear();
});

var app = builder.Build();

app.UseForwardedHeaders();
app.UseSerilogRequestLogging();
app.UseExceptionHandler();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();
}

app.UseCors("frontend");
app.UseRateLimiter();

app.MapHealthEndpoints();
app.MapScoringEndpoints();

app.Run();

// Expuesto para WebApplicationFactory<Program> en las pruebas de integración.
public partial class Program { }
