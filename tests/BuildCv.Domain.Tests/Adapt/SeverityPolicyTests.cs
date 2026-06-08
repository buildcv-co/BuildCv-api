using BuildCv.Domain.Adapt;
using FluentAssertions;
using Xunit;

namespace BuildCv.Domain.Tests.Adapt;

public sealed class SeverityPolicyTests
{
    private readonly SeverityPolicy _policy = new();

    [Fact]
    public void No_inventions_returns_None()
    {
        var result = _policy.Classify(Array.Empty<EntityInvention>());

        result.Should().Be(Severity.None);
    }

    [Fact]
    public void One_soft_invention_returns_Warning()
    {
        var inventions = new[]
        {
            new EntityInvention(InventionType.Skill, "AWS", null, InventionSeverity.Soft, 0)
        };

        var result = _policy.Classify(inventions);

        result.Should().Be(Severity.Warning);
    }

    [Fact]
    public void Two_soft_inventions_returns_Warning()
    {
        var inventions = new[]
        {
            new EntityInvention(InventionType.Skill, "AWS", null, InventionSeverity.Soft, 0),
            new EntityInvention(InventionType.Skill, "Docker", null, InventionSeverity.Soft, 1)
        };

        var result = _policy.Classify(inventions);

        result.Should().Be(Severity.Warning);
    }

    [Fact]
    public void Three_soft_inventions_returns_Critical()
    {
        var inventions = new[]
        {
            new EntityInvention(InventionType.Skill, "AWS", null, InventionSeverity.Soft, 0),
            new EntityInvention(InventionType.Skill, "Docker", null, InventionSeverity.Soft, 1),
            new EntityInvention(InventionType.Skill, "K8s", null, InventionSeverity.Soft, 2)
        };

        var result = _policy.Classify(inventions);

        result.Should().Be(Severity.Critical);
    }

    [Fact]
    public void One_hard_invention_returns_Critical()
    {
        var inventions = new[]
        {
            new EntityInvention(InventionType.Company, "FakeCorp", null, InventionSeverity.Hard, 0)
        };

        var result = _policy.Classify(inventions);

        result.Should().Be(Severity.Critical);
    }

    [Fact]
    public void Title_invention_is_hard()
    {
        var inventions = new[]
        {
            new EntityInvention(InventionType.Title, "Senior", null, InventionSeverity.Hard, 0)
        };

        var result = _policy.Classify(inventions);

        result.Should().Be(Severity.Critical);
    }
}
