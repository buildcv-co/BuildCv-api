using BuildCv.Domain.Adapt;
using BuildCv.Domain.Export;
using FluentAssertions;
using Xunit;

namespace BuildCv.Domain.Tests.Export;

public sealed class ValidationGateTests
{
    private readonly ValidationGate _gate = new();

    [Fact]
    public void No_inventions_returns_true()
    {
        var report = new ValidationReport(true, Severity.None, Array.Empty<EntityInvention>(), Array.Empty<string>());

        _gate.CanExport(report).Should().BeTrue();
    }

    [Fact]
    public void Warning_with_only_soft_inventions_returns_true()
    {
        var inventions = new[]
        {
            new EntityInvention(InventionType.Skill, "AWS", null, InventionSeverity.Soft, 0)
        };
        var report = new ValidationReport(true, Severity.Warning, inventions, new[] { "1 soft" });

        _gate.CanExport(report).Should().BeTrue();
    }

    [Fact]
    public void Critical_with_hard_invention_returns_false()
    {
        var inventions = new[]
        {
            new EntityInvention(InventionType.Company, "FakeCorp", null, InventionSeverity.Hard, 0)
        };
        var report = new ValidationReport(false, Severity.Critical, inventions, new[] { "1 hard" });

        _gate.CanExport(report).Should().BeFalse();
    }

    [Fact]
    public void Critical_with_only_soft_inventions_returns_true()
    {
        var inventions = new[]
        {
            new EntityInvention(InventionType.Skill, "AWS", null, InventionSeverity.Soft, 0),
            new EntityInvention(InventionType.Skill, "Docker", null, InventionSeverity.Soft, 1),
            new EntityInvention(InventionType.Skill, "K8s", null, InventionSeverity.Soft, 2)
        };
        var report = new ValidationReport(false, Severity.Critical, inventions, new[] { "3 soft" });

        _gate.CanExport(report).Should().BeTrue();
    }

    [Fact]
    public void Explain_why_blocked_lists_inventions()
    {
        var inventions = new[]
        {
            new EntityInvention(InventionType.Company, "FakeCorp", null, InventionSeverity.Hard, 0),
            new EntityInvention(InventionType.Certification, "AWS Certified", null, InventionSeverity.Hard, 1)
        };
        var report = new ValidationReport(false, Severity.Critical, inventions, Array.Empty<string>());

        var explanation = _gate.ExplainWhyBlocked(report);

        explanation.Should().Contain("FakeCorp");
        explanation.Should().Contain("AWS Certified");
        explanation.Should().Contain("2 invención");
    }

    [Fact]
    public void Explain_why_blocked_returns_empty_when_can_export()
    {
        var report = new ValidationReport(true, Severity.None, Array.Empty<EntityInvention>(), Array.Empty<string>());

        _gate.ExplainWhyBlocked(report).Should().BeEmpty();
    }
}
