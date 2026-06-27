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
        var atIndex = email.IndexOf('@');
        if (atIndex <= 0 || atIndex == email.Length - 1)
        {
            return false;
        }

        var dotIndex = email.IndexOf('.', atIndex);
        return dotIndex > atIndex + 1 && dotIndex < email.Length - 1;
    }
}

public sealed record WebSignupResult(Guid UserId);
