using BuildCv.Domain.Common;

namespace BuildCv.Application.Features.Auth;

public sealed class WebSignupHandler(IUserDataService userDataService)
{
    private static readonly HashSet<string> AllowedProviders = new(StringComparer.OrdinalIgnoreCase)
    {
        "google",
        "linkedin"
    };

    public async Task<Result<WebSignupResult>> HandleAsync(WebSignupCommand command, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(command.Provider) || !AllowedProviders.Contains(command.Provider))
        {
            return Result.Failure<WebSignupResult>(new Error("AUTH/UNKNOWN_PROVIDER", $"Unsupported provider: {command.Provider}"));
        }

        if (string.IsNullOrWhiteSpace(command.ProviderAccountId))
        {
            return Result.Failure<WebSignupResult>(new Error("AUTH/MISSING_PROVIDER_ACCOUNT_ID", "providerAccountId is required"));
        }

        if (string.IsNullOrWhiteSpace(command.Email) || !IsValidEmail(command.Email))
        {
            return Result.Failure<WebSignupResult>(new Error("AUTH/INVALID_EMAIL", "Invalid email format"));
        }

        if (string.IsNullOrWhiteSpace(command.Name))
        {
            return Result.Failure<WebSignupResult>(new Error("AUTH/MISSING_NAME", "name is required"));
        }

        var userResult = await userDataService.GetOrCreateAsync(
            command.Provider.ToLowerInvariant(),
            command.ProviderAccountId,
            command.Email,
            command.Name,
            ct);

        if (userResult.IsFailure)
        {
            return Result.Failure<WebSignupResult>(userResult.Error);
        }

        return Result.Success(new WebSignupResult(userResult.Value.Id));
    }

    private static bool IsValidEmail(string email)
    {
        if (email.Length is > 254 or < 3 || email.Any(char.IsWhiteSpace))
        {
            return false;
        }

        var atIndex = email.IndexOf('@');
        if (atIndex <= 0 || atIndex != email.LastIndexOf('@') || atIndex > 64 || atIndex == email.Length - 1)
        {
            return false;
        }

        var domain = email[(atIndex + 1)..];
        var labels = domain.Split('.');
        return labels.Length >= 2
            && labels[^1].Length >= 2
            && labels.All(IsValidDomainLabel);
    }

    private static bool IsValidDomainLabel(string label)
    {
        return label.Length > 0
            && label[0] != '-'
            && label[^1] != '-'
            && label.All(static c => char.IsAsciiLetterOrDigit(c) || c == '-');
    }
}

public sealed record WebSignupResult(Guid UserId);
