using BuildCv.Application.Features.Adapt;
using BuildCv.Application.Features.Auth;
using BuildCv.Application.Features.Consent;
using BuildCv.Application.Features.Export;
using BuildCv.Application.Features.Import;
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

        services.AddSingleton<IValidator<AdaptCvCommand>, AdaptCvValidator>();
        services.AddSingleton<AdaptCvHandler>();

        services.AddSingleton<IValidator<ExportPdfCommand>, ExportPdfValidator>();

        services.AddSingleton<IValidator<ImportCvCommand>, ImportCvValidator>();
        services.AddSingleton<ImportCvHandler>();

        services.AddSingleton<InMemoryConsentStore>();
        services.AddSingleton<InMemoryUserDataStore>();
        services.AddSingleton<IUserDataService>(sp => new InMemoryUserDataService(sp.GetRequiredService<InMemoryUserDataStore>()));
        services.AddSingleton<GoogleOAuthCallbackHandler>();
        services.AddSingleton<LinkedInOAuthCallbackHandler>();
        services.AddSingleton<RefreshTokenHandler>();
        services.AddSingleton<LogoutHandler>();
        services.AddSingleton<GrantConsentHandler>();
        services.AddSingleton<RevokeConsentHandler>();
        services.AddSingleton<HasActiveConsentHandler>();
        services.AddSingleton<GetConsentHistoryHandler>();
        services.AddSingleton<GetUserDataHandler>();
        services.AddSingleton<RectifyUserDataHandler>();
        services.AddSingleton<DeleteUserDataHandler>();
        services.AddSingleton<PrivacyPolicyQueryHandler>();

        return services;
    }
}
