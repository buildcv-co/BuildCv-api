using System.Text.Json;
using System.Text.Json.Nodes;
using Anthropic.SDK;
using Anthropic.SDK.Common;
using Anthropic.SDK.Constants;
using Anthropic.SDK.Extensions;
using Anthropic.SDK.Messaging;
using BuildCv.Application.Features.Adapt;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace BuildCv.Infrastructure.Ai;

/// <summary>
/// Anthropic Claude con structured output vía tool use (function calling).
/// Equivalente C# del patrón Python: Pydantic <c>BaseModel</c> →
/// <see cref="JsonSchemaExporterHelper"/> → Anthropic <c>tools</c> parameter.
/// Constitution Art. VI: implementación del puerto en Infrastructure.
/// </summary>
public sealed class AnthropicAiClient : IAiClient
{
    private readonly AnthropicClient _client;
    private readonly AnthropicSettings _settings;
    private readonly ILogger<AnthropicAiClient> _logger;

    public AnthropicAiClient(
        AnthropicClient client,
        IOptions<AnthropicSettings> settings,
        ILogger<AnthropicAiClient> logger)
    {
        _client = client;
        _settings = settings.Value;
        _logger = logger;
    }

    public async Task<T> CompleteStructuredAsync<T>(string prompt, CancellationToken ct = default)
        where T : class
    {
        var schemaJson = JsonSchemaExporterHelper.Export<T>();
        var schemaNode = JsonNode.Parse(schemaJson)!.AsObject();

        var toolName = $"return_{typeof(T).Name.ToLowerInvariant()}";
        var function = new Function(toolName, $"Returns a {typeof(T).Name} strictly matching the JSON schema.", schemaNode);
        var tool = new Anthropic.SDK.Common.Tool(function);

        var parameters = new MessageParameters
        {
            Model = _settings.Model ?? AnthropicModels.Claude4Sonnet,
            MaxTokens = _settings.MaxTokens ?? 4096,
            Tools = new List<Anthropic.SDK.Common.Tool> { tool },
            ToolChoice = new ToolChoice { Type = ToolChoiceType.Tool, Name = toolName },
            Messages = new List<Message> { new Message(RoleType.User, prompt) }
        };

        var response = await _client.Messages.GetClaudeMessageAsync(parameters, ct).ConfigureAwait(false);

        var toolUse = response.Content.OfType<ToolUseContent>().FirstOrDefault()
            ?? throw new InvalidOperationException(
                "Anthropic no devolvió un tool_use en la respuesta. La integración estructurada requiere tool use forzado.");

        var inputJson = toolUse.Input.ToJsonString();
        var result = JsonSerializer.Deserialize<T>(inputJson, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        }) ?? throw new InvalidOperationException("Falló deserialización de respuesta Anthropic.");

        var validationErrors = ValidateDataAnnotations(result);
        if (validationErrors.Count > 0)
        {
            throw new InvalidOperationException(
                $"Respuesta Anthropic falló validación DataAnnotations: {string.Join("; ", validationErrors)}");
        }

        _logger.LogInformation("Anthropic structured response OK (type={Type})", typeof(T).Name);
        return result;
    }

    public async Task<string> CompleteAsync(string prompt, CancellationToken ct = default)
    {
        var parameters = new MessageParameters
        {
            Model = _settings.Model ?? AnthropicModels.Claude4Sonnet,
            MaxTokens = _settings.MaxTokens ?? 4096,
            Messages = new List<Message> { new Message(RoleType.User, prompt) }
        };

        var response = await _client.Messages.GetClaudeMessageAsync(parameters, ct).ConfigureAwait(false);
        return string.Concat(response.Content.OfType<TextContent>().Select(t => t.Text));
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
/// Settings para Anthropic. <see cref="ApiKey"/> se resuelve de <c>Ai:ApiKey</c>
/// (dev: appsettings.Development.json; prod: env var <c>Ai__ApiKey</c> o user-secrets).
/// </summary>
public sealed class AnthropicSettings
{
    public string ApiKey { get; init; } = "";
    public string? Model { get; init; }
    public int? MaxTokens { get; init; }
}
