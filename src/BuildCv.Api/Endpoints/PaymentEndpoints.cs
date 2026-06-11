using System.Security.Claims;
using BuildCv.Application.Features.Payments;

namespace BuildCv.Api.Endpoints;

public static class PaymentEndpoints
{
    public static IEndpointRouteBuilder MapPaymentEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/v1/payments/checkout", async (
            CheckoutRequest request,
            ClaimsPrincipal user,
            CreateCheckoutHandler handler,
            CancellationToken ct) =>
        {
            var userId = GetUserId(user);
            if (userId is null)
            {
                return Results.Unauthorized();
            }

            var command = new CreateCheckoutCommand
            {
                UserId = userId.Value.ToString(),
                PackageId = request.PackageId,
            };
            var result = await handler.HandleAsync(command, ct);

            if (result.IsFailure)
            {
                return result.Error.Code == "PAYMENT/INVALID_PACKAGE"
                    ? Results.BadRequest(new { error = result.Error.Code, message = result.Error.Message })
                    : Results.Json(
                        new { type = "https://buildcv.com/errors/payment", title = result.Error.Code, status = 502, detail = result.Error.Message },
                        statusCode: 502);
            }

            return Results.Ok(new
            {
                sessionId = result.Value.SessionId,
                publicKey = result.Value.PublicKey,
                amountInCents = result.Value.AmountInCents,
                currency = result.Value.Currency,
                reference = result.Value.Reference,
            });
        })
        .RequireAuthorization()
        .WithName("CreateCheckout")
        .WithSummary("Create a Wompi checkout session for purchasing credits.");

        app.MapPost("/api/v1/payments/webhook", async (
            HttpRequest request,
            HandleWebhookHandler handler,
            ILoggerFactory loggerFactory,
            CancellationToken ct) =>
        {
            var logger = loggerFactory.CreateLogger("PaymentEndpoints");
            using var reader = new StreamReader(request.Body);
            var payload = await reader.ReadToEndAsync(ct);
            var signature = ExtractSignature(request);

            if (string.IsNullOrEmpty(signature))
            {
                logger.LogWarning("Wompi webhook rejected: missing X-Event-Checksum header");
                return Results.Unauthorized();
            }

            var command = new HandleWebhookCommand
            {
                Payload = payload,
                SignatureHeader = signature,
            };
            var result = await handler.HandleAsync(command, ct);

            if (result.IsFailure)
            {
                if (result.Error.Code == "PAYMENT/INVALID_SIGNATURE")
                {
                    logger.LogWarning("Wompi webhook rejected: invalid HMAC signature");
                    return Results.Unauthorized();
                }

                if (result.Error.Code == "PAYMENT/NOT_FOUND")
                {
                    logger.LogWarning("Wompi webhook rejected: {Detail}", result.Error.Message);
                    return Results.NotFound(new { error = result.Error.Code, message = result.Error.Message });
                }

                return Results.Json(
                    new { type = "https://buildcv.com/errors/payment", title = result.Error.Code, status = 400, detail = result.Error.Message },
                    statusCode: 400);
            }

            return Results.Ok(new { status = "received" });
        })
        .WithName("HandlePaymentWebhook")
        .WithSummary("Wompi server-to-server webhook (HMAC verified).");

        app.MapGet("/api/v1/payments/{id:guid}", async (
            Guid id,
            ClaimsPrincipal user,
            GetPaymentHandler handler,
            CancellationToken ct) =>
        {
            var userId = GetUserId(user);
            if (userId is null)
            {
                return Results.Unauthorized();
            }

            var query = new GetPaymentQuery
            {
                PaymentId = id,
                UserId = userId.Value.ToString(),
            };
            var result = await handler.HandleAsync(query, ct);

            if (result.IsFailure)
            {
                return Results.NotFound(new { error = result.Error.Code, message = result.Error.Message });
            }

            return Results.Ok(MapToPaymentResponse(result.Value));
        })
        .RequireAuthorization()
        .WithName("GetPayment")
        .WithSummary("Get a payment by id (owner only).");

        app.MapGet("/api/v1/payments", async (
            ClaimsPrincipal user,
            ListPaymentsHandler handler,
            int? page,
            int? perPage,
            CancellationToken ct) =>
        {
            var userId = GetUserId(user);
            if (userId is null)
            {
                return Results.Unauthorized();
            }

            var query = new ListPaymentsQuery
            {
                UserId = userId.Value.ToString(),
                Page = page is null or <= 0 ? 1 : page.Value,
                PerPage = perPage is null or <= 0 ? 20 : Math.Min(perPage.Value, 100),
            };
            var result = await handler.HandleAsync(query, ct);

            return Results.Ok(result.Value.Select(MapToPaymentResponse));
        })
        .RequireAuthorization()
        .WithName("ListPayments")
        .WithSummary("List the authenticated user's payments, paginated.");

        return app;
    }

    private static Guid? GetUserId(ClaimsPrincipal user)
    {
        var userId = user.FindFirstValue(ClaimTypes.NameIdentifier) ?? user.FindFirstValue("sub");
        return userId is not null && Guid.TryParse(userId, out var id) ? id : null;
    }

    private static string? ExtractSignature(HttpRequest request)
    {
        if (request.Headers.TryGetValue("X-Event-Checksum", out var checksum) && !string.IsNullOrEmpty(checksum))
        {
            return checksum.ToString();
        }

        if (request.Headers.TryGetValue("X-Signature", out var signature) && !string.IsNullOrEmpty(signature))
        {
            return signature.ToString();
        }

        return null;
    }

    private static PaymentResponse MapToPaymentResponse(BuildCv.Domain.Payments.Payment payment) => new(
        payment.Id,
        payment.UserId,
        payment.PackageId,
        payment.Credits,
        payment.AmountInCents,
        payment.Currency,
        payment.Status.ToString(),
        payment.WompiTransactionId,
        payment.WompiPaymentLink,
        payment.ProviderSessionId,
        payment.IdempotencyKey,
        payment.CreatedAt,
        payment.UpdatedAt,
        payment.PaidAt);
}

public sealed record CheckoutRequest
{
    public string PackageId { get; init; } = "";
}

public sealed record PaymentResponse(
    Guid Id,
    Guid UserId,
    string PackageId,
    int Credits,
    long AmountInCents,
    string Currency,
    string Status,
    string? WompiTransactionId,
    string? WompiPaymentLink,
    string? ProviderSessionId,
    string IdempotencyKey,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    DateTime? PaidAt);
