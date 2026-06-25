using BuildCv.Application.Common;
using BuildCv.Application.Features.Adapt;
using BuildCv.Application.Features.Auth;
using BuildCv.Application.Features.Credits;
using BuildCv.Application.Features.Export;
using BuildCv.Application.Features.Import;
using BuildCv.Application.Features.Invoicing;
using BuildCv.Application.Features.Payments;
using BuildCv.Domain.Adapt;
using BuildCv.Domain.Export;
using BuildCv.Domain.Lexicon;
using BuildCv.Infrastructure.Ai;
using BuildCv.Infrastructure.Auth;
using BuildCv.Infrastructure.Credits;
using BuildCv.Infrastructure.FeatureFlags;
using BuildCv.Infrastructure.Invoicing;
using BuildCv.Infrastructure.Lexicon;
using BuildCv.Infrastructure.Parsing;
using BuildCv.Infrastructure.Payments;
using BuildCv.Infrastructure.Pdf;
using BuildCv.Infrastructure.Persistence;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

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

        services.AddSingleton<JwtTokenAdapter>(sp => new JwtTokenAdapter(
            configuration["Jwt:SigningKey"] ?? "default-dev-signing-key-that-is-long-enough-for-hmac-sha256!",
            configuration["Jwt:Issuer"] ?? "buildcv",
            configuration["Jwt:Audience"] ?? "buildcv"));
        services.AddSingleton<NextAuthJwtValidator>(_ => new NextAuthJwtValidator(
            configuration["NextAuth:SigningKey"] ?? configuration["Jwt:SigningKey"] ?? "default-dev-signing-key-that-is-long-enough-for-hmac-sha256!",
            configuration["NextAuth:Issuer"] ?? configuration["Jwt:Issuer"] ?? "buildcv",
            configuration["NextAuth:Audience"] ?? configuration["Jwt:Audience"] ?? "buildcv"));
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

        services.Configure<PostgresSettings>(configuration.GetSection(PostgresSettings.SectionName));

        var provider = configuration["Persistence:Provider"] ?? "InMemory";

        if (provider.Equals("Postgres", StringComparison.OrdinalIgnoreCase))
        {
            services.AddDbContext<BuildCvDbContext>(options =>
            {
                var settings = configuration.GetSection(PostgresSettings.SectionName).Get<PostgresSettings>()
                    ?? new PostgresSettings();
                options.UseNpgsql(settings.ConnectionString);
            });
            services.AddScoped<IConsentStore, EfConsentStore>();
            services.AddScoped<IUserDataStore, EfUserDataStore>();
            services.AddScoped<IRefreshTokenStore, EfRefreshTokenStore>();
            services.AddScoped<IPaymentStore, EfPaymentStore>();
            services.AddSingleton<IUserDataService>(sp => new InMemoryUserDataService(sp.GetRequiredService<IUserDataStore>()));
            services.AddScoped<ICreditLedger, EfCreditLedger>();
            services.AddScoped<ICreditConsumptionService, EfCreditConsumptionService>();
            services.AddScoped<IFeatureFlagStore, EfFeatureFlagStore>();
            services.AddScoped<IFeatureFlag, CachingFeatureFlagDecorator>();
            services.AddScoped<IFeatureFlagAdminService, FeatureFlagAdminService>();
        }
        else
        {
            services.AddSingleton<IConsentStore, InMemoryConsentStore>();
            services.AddSingleton<IUserDataStore, InMemoryUserDataStore>();
            services.AddSingleton<IRefreshTokenStore, InMemoryRefreshTokenStore>();
            services.AddSingleton<IPaymentStore, InMemoryPaymentStore>();
            services.AddSingleton<IUserDataService>(sp => new InMemoryUserDataService(sp.GetRequiredService<IUserDataStore>()));
            services.AddSingleton<ICreditLedger, InMemoryCreditLedger>();
            services.AddSingleton<ICreditConsumptionService, InMemoryCreditConsumptionService>();
            services.AddSingleton<IFeatureFlagStore, InMemoryFeatureFlagStore>();
            services.AddSingleton<IFeatureFlag, CachingFeatureFlagDecorator>();
        }

        // Invoicing services
        services.Configure<FactusSettings>(configuration.GetSection("Factus"));
        services.AddSingleton<INumberingRangeStore, InMemoryNumberingRangeStore>();
        services.AddSingleton<IInvoiceStore, InMemoryInvoiceStore>();

        var factusEnabled = configuration.GetValue<bool>("Factus:Enabled");
        if (factusEnabled)
        {
            services.AddHttpClient<IInvoiceProvider, FactusAdapter>();
        }
        else
        {
            services.AddSingleton<IInvoiceProvider, LocalInvoiceProvider>();
        }

        // Payment services (012-wompi PR2)
        services.Configure<WompiSettings>(configuration.GetSection(WompiSettings.SectionName));

        // Credit services (013-credit-consumption PR2)
        services.Configure<CreditsOptions>(configuration.GetSection(CreditsOptions.SectionName));
        services.AddSingleton<ICreditsFeatureFlag, FeatureFlagCreditsAdapter>();

        services.AddSingleton<AccreditPurchaseHandler>();
        services.AddSingleton<AccreditWelcomeHandler>();
        services.AddSingleton<ConsumeForAdaptHandler>();
        services.AddSingleton<RefundConsumptionHandler>();
        services.AddSingleton<GetCreditBalanceHandler>();
        services.AddSingleton<GetCreditHistoryHandler>();
        services.AddSingleton<GrantManualCreditHandler>();

        var wompiEnabled = configuration.GetValue<bool>(WompiSettings.SectionName + ":Enabled");
        if (wompiEnabled)
        {
            services.AddHttpClient<IPaymentProvider, WompiAdapter>();
            services.AddSingleton<IPaymentReconciliationService, PaymentReconciliationService>();
            services.AddHostedService<PaymentReconciliationWorker>();
        }
        else
        {
            services.AddSingleton<IPaymentProvider, DisabledPaymentProvider>();
        }

        services.Configure<FeatureFlagsOptions>(configuration.GetSection("FeatureFlags"));
        services.AddHostedService<FeatureFlagMigrationService>();

        return services;
    }
}
