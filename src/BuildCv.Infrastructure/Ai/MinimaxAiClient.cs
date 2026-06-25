using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using BuildCv.Application.Features.Adapt;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace BuildCv.Infrastructure.Ai;

/// <summary>
/// Cliente Minimax con structured output vía JSON mode (compatible con la convención
/// OpenAI <c>response_format: { type: "json_object", schema: ... }</c>).
/// Proveedor alternativo cuando no se tiene acceso a Anthropic.
/// Constitution Art. VI: implementación del puerto en Infrastructure.
/// </summary>
public sealed class MinimaxAiClient : IAiClient
{
    public const string HttpClientName = "Minimax";

    private readonly HttpClient _http;
    private readonly MinimaxSettings _settings;
    private readonly ILogger<MinimaxAiClient> _logger;

    public MinimaxAiClient(
        HttpClient http,
        IOptions<MinimaxSettings> settings,
        ILogger<MinimaxAiClient> logger)
    {
        _http = http;
        _settings = settings.Value;
        _logger = logger;
    }

    public async Task<T> CompleteStructuredAsync<T>(string prompt, CancellationToken ct = default)
        where T : class
    {
        var schemaJson = JsonSchemaExporterHelper.Export<T>();
        var schemaElement = JsonDocument.Parse(schemaJson).RootElement;

        var request = new
        {
            model = _settings.Model ?? "MiniMax-Text-01",
            messages = new[] { new { role = "user", content = prompt } },
            response_format = new { type = "json_object", schema = schemaElement },
            max_tokens = _settings.MaxTokens ?? 4096
        };

        using var response = await _http.PostAsJsonAsync("/v1/chat/completions", request, ct).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        var doc = await response.Content.ReadFromJsonAsync<MinimaxChatResponse>(cancellationToken: ct).ConfigureAwait(false)
            ?? throw new InvalidOperationException("Minimax devolvió respuesta vacía.");

        var json = doc.Choices[0].Message.Content
            ?? throw new InvalidOperationException("Minimax devolvió contenido vacío.");

        var result = JsonSerializer.Deserialize<T>(json, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        }) ?? throw new InvalidOperationException("Falló deserialización de respuesta Minimax.");

        var validationErrors = ValidateDataAnnotations(result);
        if (validationErrors.Count > 0)
        {
            throw new InvalidOperationException(
                $"Respuesta Minimax falló validación DataAnnotations: {string.Join("; ", validationErrors)}");
        }

        _logger.LogInformation("Minimax structured response OK (type={Type})", typeof(T).Name);
        return result;
    }

    public async Task<string> CompleteAsync(string prompt, CancellationToken ct = default)
    {
        var request = new
        {
            model = _settings.Model ?? "MiniMax-Text-01",
            messages = new[] { new { role = "user", content = prompt } },
            max_tokens = _settings.MaxTokens ?? 4096
        };

        using var response = await _http.PostAsJsonAsync("/v1/chat/completions", request, ct).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        var doc = await response.Content.ReadFromJsonAsync<MinimaxChatResponse>(cancellationToken: ct).ConfigureAwait(false)
            ?? throw new InvalidOperationException("Minimax devolvió respuesta vacía.");

        return doc.Choices[0].Message.Content ?? string.Empty;
    }

    private static List<string> ValidateDataAnnotations<T>(T instance) where T : class
    {
        var ctx = new System.ComponentModel.DataAnnotations.ValidationContext(instance);
        var results = new List<System.ComponentModel.DataAnnotations.ValidationResult>();
        System.ComponentModel.DataAnnotations.Validator.TryValidateObject(instance, ctx, results, validateAllProperties: true);
        return results.Select(r => r.ErrorMessage ?? "(error sin mensaje)").ToList();
    }
}

/// <summary>
/// Settings para Minimax. <see cref="ApiKey"/> vía <c>Ai:ApiKey</c>; <see cref="BaseUrl"/>
/// configurable para apuntar a gateways on-prem o réplicas regionales.
/// </summary>
public sealed class MinimaxSettings
{
    public string ApiKey { get; init; } = "";
    public string BaseUrl { get; init; } = "https://api.MiniMax.chat";
    public string? Model { get; init; }
    public int? MaxTokens { get; init; }
}

internal sealed record MinimaxChatResponse(MinimaxChoice[] Choices);
internal sealed record MinimaxChoice(MinimaxMessage Message);
internal sealed record MinimaxMessage(string Role, string? Content);
