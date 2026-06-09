using BuildCv.Application.Features.Auth;
using BuildCv.Domain.Common;
using BuildCv.Infrastructure.Auth;
using FluentAssertions;

namespace BuildCv.Infrastructure.Tests.Auth;

public sealed class GoogleOAuthAdapterTests
{
    private sealed class FakeHttpMessageHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _responder;
        public List<HttpRequestMessage> Requests { get; } = [];

        public FakeHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> responder)
            => _responder = responder;

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add(request);
            return Task.FromResult(_responder(request));
        }
    }

    [Fact]
    public async Task ExchangeCodeAsync_exchanges_code_for_user_info()
    {
        var handler = new FakeHttpMessageHandler(request =>
        {
            if (request.RequestUri!.ToString().Contains("oauth2.googleapis.com/token"))
            {
                return new HttpResponseMessage(System.Net.HttpStatusCode.OK)
                {
                    Content = new StringContent("""{"access_token":"ya29.mock","token_type":"Bearer","expires_in":3600}""")
                };
            }

            return new HttpResponseMessage(System.Net.HttpStatusCode.OK)
            {
                Content = new StringContent("""{"id":"g-123","email":"alice@gmail.com","name":"Alice Google"}""")
            };
        });

        var httpClient = new HttpClient(handler);
        var adapter = new GoogleOAuthAdapter(httpClient, "client-id", "client-secret");

        var result = await adapter.ExchangeCodeAsync("google", "auth-code", "http://localhost/callback");

        result.IsSuccess.Should().BeTrue();
        result.Value.Provider.Should().Be("google");
        result.Value.ProviderId.Should().Be("g-123");
        result.Value.Email.Should().Be("alice@gmail.com");
        result.Value.Name.Should().Be("Alice Google");
    }

    [Fact]
    public async Task ExchangeCodeAsync_returns_failure_when_token_exchange_fails()
    {
        var handler = new FakeHttpMessageHandler(_ =>
            new HttpResponseMessage(System.Net.HttpStatusCode.BadRequest)
            {
                Content = new StringContent("""{"error":"invalid_grant"}""")
            });

        var httpClient = new HttpClient(handler);
        var adapter = new GoogleOAuthAdapter(httpClient, "client-id", "client-secret");

        var result = await adapter.ExchangeCodeAsync("google", "bad-code", "http://localhost/callback");

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("AUTH/OAUTH_FAILED");
    }

    [Fact]
    public async Task ExchangeCodeAsync_returns_failure_when_userinfo_fails()
    {
        var handler = new FakeHttpMessageHandler(request =>
        {
            if (request.RequestUri!.ToString().Contains("oauth2.googleapis.com/token"))
            {
                return new HttpResponseMessage(System.Net.HttpStatusCode.OK)
                {
                    Content = new StringContent("""{"access_token":"ya29.mock","token_type":"Bearer","expires_in":3600}""")
                };
            }

            return new HttpResponseMessage(System.Net.HttpStatusCode.Unauthorized)
            {
                Content = new StringContent("""{"error":"invalid_token"}""")
            };
        });

        var httpClient = new HttpClient(handler);
        var adapter = new GoogleOAuthAdapter(httpClient, "client-id", "client-secret");

        var result = await adapter.ExchangeCodeAsync("google", "auth-code", "http://localhost/callback");

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("AUTH/OAUTH_FAILED");
    }
}
