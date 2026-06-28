using BuildCv.Application.Features.LlmFeedback;
using FluentAssertions;

namespace BuildCv.Application.Tests.Features.LlmFeedback;

public sealed class PiiRedactorTests
{
    [Fact]
    public void Redact_MasksEmailAddresses()
    {
        var redacted = PiiRedactor.Redact("Contact me at ada.lovelace@example.com for .NET roles.");

        redacted.Should().Be("Contact me at [EMAIL_REDACTED] for .NET roles.");
    }

    [Theory]
    [InlineData("Call +57 300 123 4567", "Call [PHONE_REDACTED]")]
    [InlineData("Phone (555) 123-4567", "Phone [PHONE_REDACTED]")]
    [InlineData("Teléfono +34 612 345 678", "Teléfono [PHONE_REDACTED]")]
    public void Redact_MasksColombianUsAndSpanishPhones(string input, string expected)
    {
        PiiRedactor.Redact(input).Should().Be(expected);
    }

    [Fact]
    public void Redact_MasksPersonalUrlsButKeepsProfessionalDomains()
    {
        var input = "Portfolio https://personal.dev and LinkedIn https://linkedin.com/in/ada plus GitHub https://github.com/ada";

        var redacted = PiiRedactor.Redact(input);

        redacted.Should().Contain("Portfolio [URL_REDACTED]");
        redacted.Should().Contain("https://linkedin.com/in/ada");
        redacted.Should().Contain("https://github.com/ada");
    }

    [Theory]
    [InlineData("Vivo en calle 80 # 12-34 Bogotá")]
    [InlineData("Dirección: carrera 15 93-20")]
    [InlineData("avenida siempre viva 742")]
    public void Redact_MasksLikelyPhysicalAddresses(string input)
    {
        PiiRedactor.Redact(input).Should().Be("[ADDRESS_REDACTED]");
    }

    [Fact]
    public void Redact_DoesNotMaskNamesOrWorkContext()
    {
        var input = "Ada Lovelace led backend work with C#, ASP.NET Core and PostgreSQL for BuildCv.";

        PiiRedactor.Redact(input).Should().Be(input);
    }

    [Fact]
    public void Redact_ThrowsLlmFeedbackRedactionExceptionWhenInputCannotBeProcessed()
    {
        var act = () => PiiRedactor.Redact(new string('a', PiiRedactor.MaxInputCharacters + 1));

        act.Should().Throw<LlmFeedbackRedactionException>()
            .WithMessage("LLM feedback redaction failed before provider boundary.");
    }
}
