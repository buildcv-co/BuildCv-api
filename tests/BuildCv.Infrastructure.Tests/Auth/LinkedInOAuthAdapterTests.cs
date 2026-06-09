using BuildCv.Application.Features.Auth;
using BuildCv.Domain.Common;
using BuildCv.Infrastructure.Auth;
using FluentAssertions;

namespace BuildCv.Infrastructure.Tests.Auth;

public sealed class LinkedInOAuthAdapterTests
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
            if (request.RequestUri!.ToString().Contains("linkedin.com/oauth/v2/accessToken"))
            {
                return new HttpResponseMessage(System.Net.HttpStatusCode.OK)
                {
                    Content = new StringContent("""{"access_token":"aq-linkedin-mock","expires_in":5184000}""")
                };
            }

            return new HttpResponseMessage(System.Net.HttpStatusCode.OK)
            {
                Content = new StringContent("""{"sub":"li-456","email":"bob@linkedin.com","name":"Bob LinkedIn"}""")
            };
        });

        var httpClient = new HttpClient(handler);
        var adapter = new LinkedInOAuthAdapter(httpClient, "client-id", "client-secret");

        var result = await adapter.ExchangeCodeAsync("linkedin", "auth-code", "http://localhost/callback");

        result.IsSuccess.Should().BeTrue();
        result.Value.Provider.Should().Be("linkedin");
        result.Value.ProviderId.Should().Be("li-456");
        result.Value.Email.Should().Be("bob@linkedin.com");
        result.Value.Name.Should().Be("Bob LinkedIn");
    }

    [Fact]
    public async Task ExchangeCodeAsync_returns_failure_when_token_exchange_fails()
    {
        var handler = new FakeHttpMessageHandler(_ =>
            new HttpResponseMessage(System.Net.HttpStatusCode.BadRequest)
            {
                Content = new StringContent("""{"error":"invalid_grant","error_description":"code is expired"}""")
            });

        var httpClient = new HttpClient(handler);
        var adapter = new LinkedInOAuthAdapter(httpClient, "client-id", "client-secret");

        var result = await adapter.ExchangeCodeAsync("linkedin", "bad-code", "http://localhost/callback");

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("AUTH/OAUTH_FAILED");
    }

    [Fact]
    public async Task ExchangeCodeAsync_returns_failure_when_userinfo_fails()
    {
        var handler = new FakeHttpMessageHandler(request =>
        {
            if (request.RequestUri!.ToString().Contains("linkedin.com/oauth/v2/accessToken"))
            {
                return new HttpResponseMessage(System.Net.HttpStatusCode.OK)
                {
                    Content = new StringContent("""{"access_token":"aq-mock","expires_in":5184000}""")
                };
            }

            return new HttpResponseMessage(System.Net.HttpStatusCode.Unauthorized)
            {
                Content = new StringContent("""{"error":"unauthorized"}""")
            };
        });

        var httpClient = new HttpClient(handler);
        var adapter = new LinkedInOAuthAdapter(httpClient, "client-id", "client-secret");

        var result = await adapter.ExchangeCodeAsync("linkedin", "auth-code", "http://localhost/callback");

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("AUTH/OAUTH_FAILED");
    }
}
