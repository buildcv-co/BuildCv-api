using System.Text;
using System.Text.Json.Serialization;
using Asp.Versioning;
using BuildCv.Api.Auth;
using BuildCv.Api.Endpoints;
using BuildCv.Api.Errors;
using BuildCv.Api.Health;
using BuildCv.Api.Security;
using BuildCv.Application;
using BuildCv.Application.Common;
using BuildCv.Infrastructure;
using BuildCv.Infrastructure.FeatureFlags;
using BuildCv.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.IdentityModel.Tokens;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;
using Prometheus;
using Scalar.AspNetCore;
using Serilog;
using Serilog.Events;

var builder = WebApplication.CreateBuilder(args);

builder.WebHost.ConfigureKestrel(options =>
{
    options.Limits.MaxRequestBodySize = BuildCv.Api.Endpoints.ImportEndpoints.MaxRequestBodyBytes;
});

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
    .AddCheck<AiConfigHealthCheck>("ai-config", tags: ["ready"])
    .AddCheck<ParserHealthCheck>("parser", tags: ["ready"])
    .AddCheck<AiClientHealthCheck>("ai-client", tags: ["ready"])
    .AddCheck<PdfGeneratorHealthCheck>("pdf-generator", tags: ["ready"]);

var persistenceProvider = builder.Configuration["Persistence:Provider"] ?? "InMemory";
if (persistenceProvider.Equals("Postgres", StringComparison.OrdinalIgnoreCase))
{
    builder.Services.AddHealthChecks()
        .AddCheck<PostgresHealthCheck>("postgres", tags: ["ready"]);
}

// Prometheus metrics + OpenTelemetry tracing.
builder.Services.AddMetrics();
builder.Services.AddOpenTelemetry()
    .WithTracing(tracing => tracing
        .AddAspNetCoreInstrumentation()
        .AddOtlpExporter());

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

builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
});

var jwtSigningKey = builder.Configuration["Jwt:SigningKey"] ?? "default-dev-signing-key-that-is-long-enough-for-hmac-sha256!";
var jwtIssuer = builder.Configuration["Jwt:Issuer"] ?? "buildcv";
var jwtAudience = builder.Configuration["Jwt:Audience"] ?? "buildcv";

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = jwtIssuer,
            ValidateAudience = true,
            ValidAudience = jwtAudience,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSigningKey)),
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromSeconds(30),
        };
    });
builder.Services.AddAuthorization();
builder.Services.AddAuthPolicies();

// Anti-abuso por IP (rate limiting nativo).
builder.Services.AddAppRateLimiting();

builder.Services.AddSingleton<IFeatureFlagCache>(sp => new FeatureFlagCacheInvalidator(
    (CachingFeatureFlagDecorator)sp.GetRequiredService<IFeatureFlag>()));

if (!persistenceProvider.Equals("Postgres", StringComparison.OrdinalIgnoreCase))
{
    builder.Services.AddSingleton<IFeatureFlagAdminService, InMemoryFeatureFlagAdminService>();
}

// Detrás de un proxy inverso (Render/Vercel): recuperar la IP real del cliente,
// indispensable para el rate limiting por IP que se añade en M1.
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    options.KnownIPNetworks.Clear();
    options.KnownProxies.Clear();
});

var app = builder.Build();

if (persistenceProvider.Equals("Postgres", StringComparison.OrdinalIgnoreCase))
{
    var postgresSettings = builder.Configuration.GetSection(PostgresSettings.SectionName).Get<PostgresSettings>()
        ?? new PostgresSettings();
    if (postgresSettings.EnableAutoMigrate)
    {
        using var scope = app.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<BuildCvDbContext>();
        await dbContext.Database.MigrateAsync();
    }
}

app.UseForwardedHeaders();
app.UseSerilogRequestLogging();
app.UseHttpMetrics();
app.UseExceptionHandler();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();
}

app.UseCors("frontend");
app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();

app.MapHealthEndpoints();
app.MapMetrics();
app.MapScoringEndpoints();
app.MapAdaptEndpoints();
app.MapExportEndpoints();
app.MapImportEndpoints();
app.MapSessionEndpoint();
app.MapAuthEndpoints();
app.MapUserDataEndpoints();
app.MapPrivacyEndpoints();
app.MapInvoicingEndpoints();
app.MapCreditEndpoints();
app.MapFeatureFlagAdminEndpoints();
app.MapSubscriptionEndpoints();
app.MapIterationEndpoints();

if (builder.Configuration.GetValue<bool>("Wompi:Enabled"))
{
    app.MapPaymentEndpoints();
}

app.Run();

// Expuesto para WebApplicationFactory<Program> en las pruebas de integración.
public partial class Program { }
