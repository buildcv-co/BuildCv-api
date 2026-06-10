using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;

namespace BuildCv.Api.IntegrationTests.Invoicing;

public sealed class InvoicingEndpointsTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;

    public InvoicingEndpointsTests(WebApplicationFactory<Program> factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task CreateInvoice_returns_created()
    {
        var command = new
        {
            UserId = Guid.NewGuid(),
            AmountInCents = 150000,
            CustomerName = "Juan Pérez",
            CustomerIdentification = "1234567890",
            CustomerEmail = "juan@example.com"
        };

        var response = await _client.PostAsJsonAsync("/api/v1/invoices", command);

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var invoice = await response.Content.ReadFromJsonAsync<InvoiceResponse>();
        invoice.Should().NotBeNull();
        invoice!.ReferenceCode.Should().StartWith("BUILDCV-");
    }

    [Fact]
    public async Task GetInvoice_returns_invoice()
    {
        var command = new
        {
            UserId = Guid.NewGuid(),
            AmountInCents = 200000,
            CustomerName = "María García",
            CustomerIdentification = "9876543210",
            CustomerEmail = "maria@example.com"
        };

        var createResponse = await _client.PostAsJsonAsync("/api/v1/invoices", command);
        var invoice = await createResponse.Content.ReadFromJsonAsync<InvoiceResponse>();

        var response = await _client.GetAsync($"/api/v1/invoices/{invoice!.Id}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<InvoiceResponse>();
        result.Should().NotBeNull();
        result!.ReferenceCode.Should().Be(invoice.ReferenceCode);
    }

    [Fact]
    public async Task ListInvoices_returns_invoices()
    {
        var response = await _client.GetAsync("/api/v1/invoices?userId=" + Guid.NewGuid());

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var invoices = await response.Content.ReadFromJsonAsync<List<InvoiceResponse>>();
        invoices.Should().NotBeNull();
    }

    [Fact]
    public async Task CreateCreditNote_returns_created()
    {
        var command = new
        {
            UserId = Guid.NewGuid(),
            AmountInCents = 100000,
            CustomerName = "Carlos López",
            CustomerIdentification = "5555555555",
            CustomerEmail = "carlos@example.com"
        };

        var response = await _client.PostAsJsonAsync("/api/v1/credit-notes", command);

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var note = await response.Content.ReadFromJsonAsync<InvoiceResponse>();
        note.Should().NotBeNull();
    }

    [Fact]
    public async Task CreateSupportDocument_returns_created()
    {
        var command = new
        {
            UserId = Guid.NewGuid(),
            AmountInCents = 50000,
            CustomerName = "Ana Martínez",
            CustomerIdentification = "6666666666",
            CustomerEmail = "ana@example.com"
        };

        var response = await _client.PostAsJsonAsync("/api/v1/support-documents", command);

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var document = await response.Content.ReadFromJsonAsync<InvoiceResponse>();
        document.Should().NotBeNull();
    }

    [Fact]
    public async Task GetNumberingRanges_returns_ok()
    {
        var response = await _client.GetAsync("/api/v1/numbering-ranges");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task GetCompany_returns_ok()
    {
        var response = await _client.GetAsync("/api/v1/company");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    private sealed record InvoiceResponse
    {
        public Guid Id { get; init; }
        public string ReferenceCode { get; init; } = "";
        public string? Number { get; init; }
        public long AmountInCents { get; init; }
        public string Currency { get; init; } = "";
        public string Status { get; init; } = "";
        public string CustomerName { get; init; } = "";
    }
}
