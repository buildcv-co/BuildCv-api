using BuildCv.Application.Features.Adapt;
using BuildCv.Application.Features.Auth;
using BuildCv.Application.Features.Export;
using BuildCv.Application.Features.Import;
using BuildCv.Domain.Adapt;
using BuildCv.Domain.Export;
using BuildCv.Domain.Lexicon;
using BuildCv.Infrastructure.Ai;
using BuildCv.Infrastructure.Auth;
using BuildCv.Infrastructure.Lexicon;
using BuildCv.Infrastructure.Parsing;
using BuildCv.Infrastructure.Pdf;
using FluentValidation;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace BuildCv.Infrastructure;

public static class DependencyInjection
{
    /// <summary>
    /// Registra los adaptadores de infraestructura que implementan los puertos de
    /// la capa de aplicación (cliente de IA, export PDF, persistencia v1, pagos v1) y
    /// carga recursos embebidos como el diccionario de habilidades.
    /// </summary>
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddSingleton<ISkillGazetteer>(_ => GazetteerLoader.LoadEmbedded());

        services.AddSingleton<EntityExtractor>();
        services.AddSingleton<CrossEntityValidator>();
        services.AddSingleton<SeverityPolicy>();
        services.AddSingleton<PromptBuilder>();
        services.AddSingleton<IAiClient, StubAiClient>();
        services.AddSingleton<AdaptCvHandler>(sp => new AdaptCvHandler(
            sp.GetRequiredService<IAiClient>(),
            sp.GetRequiredService<EntityExtractor>(),
            sp.GetRequiredService<CrossEntityValidator>(),
            sp.GetRequiredService<SeverityPolicy>(),
            sp.GetRequiredService<PromptBuilder>(),
            sp.GetRequiredService<ILogger<AdaptCvHandler>>()));

        services.AddSingleton<ValidationGate>();
        services.AddSingleton<IPdfGenerator, QuestPdfGenerator>();
        services.AddSingleton<ExportPdfHandler>(sp => new ExportPdfHandler(
            sp.GetRequiredService<IPdfGenerator>(),
            sp.GetRequiredService<ValidationGate>(),
            sp.GetRequiredService<ILogger<ExportPdfHandler>>()));

        services.AddSingleton<PdfPigCvParser>();
        services.AddSingleton<OpenXmlCvParser>();
        services.AddSingleton<ICvParser, ParserRouter>();
        services.AddSingleton<ImportCvHandler>(sp => new ImportCvHandler(
            sp.GetRequiredService<ICvParser>(),
            sp.GetRequiredService<IValidator<ImportCvCommand>>()));

        services.AddSingleton<IRefreshTokenStore, InMemoryRefreshTokenStore>();
        services.AddSingleton<JwtTokenAdapter>(sp => new JwtTokenAdapter(
            configuration["Jwt:SigningKey"] ?? "default-dev-signing-key-that-is-long-enough-for-hmac-sha256!",
            configuration["Jwt:Issuer"] ?? "buildcv",
            configuration["Jwt:Audience"] ?? "buildcv"));
        services.AddSingleton<GoogleOAuthAdapter>(sp =>
            new GoogleOAuthAdapter(new HttpClient(), configuration["Google:ClientId"] ?? "", configuration["Google:ClientSecret"] ?? ""));
        services.AddSingleton<LinkedInOAuthAdapter>(sp =>
            new LinkedInOAuthAdapter(new HttpClient(), configuration["LinkedIn:ClientId"] ?? "", configuration["LinkedIn:ClientSecret"] ?? ""));
        services.AddSingleton<IAuthenticationService>(sp =>
        {
            var googleAdapter = sp.GetRequiredService<GoogleOAuthAdapter>();
            var linkedinAdapter = sp.GetRequiredService<LinkedInOAuthAdapter>();
            return new CompositeOAuthAdapter(googleAdapter, linkedinAdapter);
        });

        return services;
    }
}
