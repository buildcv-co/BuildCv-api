using BuildCv.Application.Features.Adapt;
using BuildCv.Domain.Adapt;
using BuildCv.Domain.Lexicon;
using BuildCv.Infrastructure.Ai;
using BuildCv.Infrastructure.Lexicon;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

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
        // El diccionario de habilidades se carga una vez desde el YAML embebido y se
        // comparte como dato inmutable (Singleton).
        services.AddSingleton<ISkillGazetteer>(_ => GazetteerLoader.LoadEmbedded());

        // Adapt (M1) — v0 usa stub determinista; M1 reemplazará con AnthropicAiClient.
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
            sp.GetRequiredService<PromptBuilder>()));

        return services;
    }
}
