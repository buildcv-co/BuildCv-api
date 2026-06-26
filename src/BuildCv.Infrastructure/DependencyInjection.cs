using Anthropic.SDK;
using BuildCv.Application.Common;
using BuildCv.Application.Features.Adapt;
using BuildCv.Application.Features.Auth;
using BuildCv.Application.Features.Credits;
using BuildCv.Application.Features.Export;
using BuildCv.Application.Features.Import;
using BuildCv.Application.Features.Invoicing;
using BuildCv.Application.Features.Iterations;
using BuildCv.Application.Features.Payments;
using BuildCv.Application.Features.Scoring;
using BuildCv.Application.Features.Subscriptions;
using BuildCv.Domain.Adapt;
using BuildCv.Domain.Export;
using BuildCv.Domain.Lexicon;
using BuildCv.Infrastructure.Ai;
using BuildCv.Infrastructure.Auth;
using BuildCv.Infrastructure.Credits;
using BuildCv.Infrastructure.FeatureFlags;
using BuildCv.Infrastructure.Invoicing;
using BuildCv.Infrastructure.Iterations;
using BuildCv.Infrastructure.Lexicon;
using BuildCv.Infrastructure.Parsing;
using BuildCv.Infrastructure.Payments;
using BuildCv.Infrastructure.Pdf;
using BuildCv.Infrastructure.Persistence;
using BuildCv.Infrastructure.Subscriptions;
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
        RegisterAiClient(services, configuration);
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
        services.AddSingleton<ICvParser>(sp => sp.GetRequiredService<PdfPigCvParser>());
        services.AddSingleton<ICvParser>(sp => sp.GetRequiredService<OpenXmlCvParser>());
        services.AddSingleton<IStructuredParser>(sp => sp.GetRequiredService<PdfPigCvParser>());
        services.AddSingleton<IStructuredParser>(sp => sp.GetRequiredService<OpenXmlCvParser>());
        services.AddSingleton<ParserRouter>();
        services.AddSingleton<IParserRouter>(sp => sp.GetRequiredService<ParserRouter>());
        services.AddSingleton<ImportCvHandler>(sp => new ImportCvHandler(
            sp.GetRequiredService<IParserRouter>(),
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
            services.AddScoped<ISubscriptionStore, EfSubscriptionStore>();
            services.AddScoped<IIterationStore, EfIterationStore>();
            services.AddSingleton<IIterationCleanupCapable>(sp => (EfIterationStore)sp.GetRequiredService<IIterationStore>());
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
            services.AddSingleton<ISubscriptionStore, InMemorySubscriptionStore>();
            services.AddSingleton<IIterationStore, InMemoryIterationStore>();
            services.AddSingleton<IIterationCleanupCapable>(sp => (InMemoryIterationStore)sp.GetRequiredService<IIterationStore>());
        }

        // Invoicing services
        services.Configure<FactusSettings>(configuration.GetSection("Factus"));
        services.AddSingleton<INumberingRangeStore, InMemoryNumberingRangeStore>();
        services.AddSingleton<IInvoiceStore, InMemoryInvoiceStore>();

        services.AddHttpClient<FactusAdapter>();
        services.AddSingleton<LocalInvoiceProvider>();
        services.AddSingleton<IInvoiceProvider, FeatureFlagInvoiceAdapter>();

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

        services.AddHttpClient<WompiAdapter>();
        services.AddSingleton<DisabledPaymentProvider>();
        services.AddSingleton<IPaymentProvider, FeatureFlagPaymentAdapter>();

        if (configuration.GetValue<bool>(WompiSettings.SectionName + ":Enabled"))
        {
            services.AddSingleton<IPaymentReconciliationService, PaymentReconciliationService>();
            services.AddHostedService<PaymentReconciliationWorker>();
        }

        services.Configure<FeatureFlagsOptions>(configuration.GetSection("FeatureFlags"));
        services.AddHostedService<FeatureFlagMigrationService>();

        // Subscription services (016-subscription-recurring PR2)
        services.AddSingleton<ISubscriptionFeatureFlag, SubscriptionFeatureFlag>();
        services.AddSingleton<SubscribeHandler>();
        services.AddSingleton<CancelSubscriptionHandler>();
        services.AddSingleton<GetSubscriptionHandler>();
        services.AddSingleton<HandleRecurringChargeHandler>();
        services.AddSingleton<ProcessRetriesHandler>();
        services.AddHttpClient<ISubscriptionProvider, WompiRecurringAdapter>();
        services.AddSingleton<DisabledSubscriptionProvider>();
        Func<IServiceProvider, CancellationToken, Task> retryTick = (sp, ct) =>
            sp.GetRequiredService<ProcessRetriesHandler>().HandleAsync(ct);
        services.AddSingleton(retryTick);
        services.AddHostedService<SubscriptionReconciliationWorker>();

        // Iteration services (018-cv-iteration-loop PR2)
        services.AddSingleton<IterateAdaptationHandler>(sp => new IterateAdaptationHandler(
            sp.GetRequiredService<AdaptCvHandler>(),
            sp.GetRequiredService<ScoreCvHandler>(),
            sp.GetRequiredService<CrossEntityValidator>(),
            sp.GetRequiredService<EntityExtractor>(),
            sp.GetRequiredService<IIterationStore>(),
            sp.GetRequiredService<ICreditLedger>(),
            sp.GetRequiredService<ILogger<IterateAdaptationHandler>>()));
        services.AddSingleton<GetIterationResultHandler>();
        services.AddSingleton<IIterationService, IterationService>();
        services.AddHostedService<IterationCleanupWorker>();

        return services;
    }

    /// <summary>
    /// Selecciona el <see cref="IAiClient"/> según <c>Ai:Provider</c>:
    /// <list type="bullet">
    /// <item><c>Stub</c> — determinista, sin clave, default para tests/v0 (Constitution Art. IX).</item>
    /// <item><c>Anthropic</c> — Claude con structured output vía tool use.</item>
    /// <item><c>Minimax</c> — JSON mode OpenAI-compatible.</item>
    /// </list>
    /// La API key nunca se loguea (Constitution Art. III) y se resuelve de
    /// <c>Ai:ApiKey</c> vía el binder estándar (env var <c>Ai__ApiKey</c>,
    /// <c>dotnet user-secrets</c>, o <c>appsettings.Development.json</c>).
    /// </summary>
    private static void RegisterAiClient(IServiceCollection services, IConfiguration configuration)
    {
        var provider = configuration["Ai:Provider"];
        if (string.IsNullOrWhiteSpace(provider) || provider.Equals("Stub", StringComparison.OrdinalIgnoreCase))
        {
            services.AddSingleton<IAiClient, StubAiClient>();
            return;
        }

        if (provider.Equals("Anthropic", StringComparison.OrdinalIgnoreCase))
        {
            services.AddSingleton(_ => new AnthropicClient(
                configuration["Ai:ApiKey"] ?? string.Empty));
            services.AddSingleton<IAiClient, AnthropicAiClient>();
            return;
        }

        if (provider.Equals("Minimax", StringComparison.OrdinalIgnoreCase))
        {
            var baseUrl = configuration["Ai:BaseUrl"];
            if (string.IsNullOrWhiteSpace(baseUrl))
            {
                baseUrl = "https://api.MiniMax.chat";
            }
            services.AddHttpClient<MinimaxAiClient>(MinimaxAiClient.HttpClientName, client =>
            {
                client.BaseAddress = new Uri(baseUrl);
                client.DefaultRequestHeaders.Add("Authorization", $"Bearer {configuration["Ai:ApiKey"] ?? string.Empty}");
            });
            services.AddSingleton<IAiClient>(sp => sp.GetRequiredService<MinimaxAiClient>());
            return;
        }

        throw new InvalidOperationException(
            $"Ai:Provider desconocido: '{provider}'. Valores válidos: Stub, Anthropic, Minimax.");
    }
}
