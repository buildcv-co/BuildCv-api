using BuildCv.Application.Features.Scoring;
using BuildCv.Domain.Jobs;
using BuildCv.Domain.Resumes;
using BuildCv.Domain.Scoring;
using BuildCv.Domain.Text;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;

namespace BuildCv.Application;

public static class DependencyInjection
{
    /// <summary>
    /// Registra los casos de uso de la capa de aplicación y los servicios de dominio
    /// puros (normalizador, léxicos, matcher, analizadores, motor de puntaje), como
    /// Singleton por ser inmutables y sin estado. El dominio es puro, por eso su
    /// composición vive aquí y no en una capa con dependencias externas.
    /// </summary>
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddSingleton<ITextNormalizer, SpanishTextNormalizer>();
        services.AddSingleton<ISpanishStemmer, SpanishLightStemmer>();
        services.AddSingleton<ConfusableBlocklist>();
        services.AddSingleton<SectionSplitter>();
        services.AddSingleton<SkillScanner>();
        services.AddSingleton<ISkillMatcher, SkillMatcher>();
        services.AddSingleton<IJobAnalyzer, JobAnalyzer>();
        services.AddSingleton<ICvAnalyzer, CvAnalyzer>();
        services.AddSingleton<IScoringEngine, ScoringEngine>();

        services.AddSingleton<ScoreCvHandler>();
        services.AddSingleton<IValidator<ScoreCvCommand>, ScoreCvValidator>();

        return services;
    }
}
