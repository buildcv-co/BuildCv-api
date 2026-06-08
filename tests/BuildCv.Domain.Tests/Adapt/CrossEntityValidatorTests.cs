using BuildCv.Domain.Adapt;
using FluentAssertions;
using Xunit;

namespace BuildCv.Domain.Tests.Adapt;

public sealed class CrossEntityValidatorTests
{
    private readonly CrossEntityValidator _validator = new();

    [Fact]
    public void Should_detect_skill_not_in_original()
    {
        var original = new[] { "C#", ".NET" };
        var adapted = new[] { "C#", ".NET", "AWS" };

        var report = _validator.Validate(
            original,
            adapted,
            new Dictionary<string, InventionType> { ["C#"] = InventionType.Skill, [".NET"] = InventionType.Skill, ["AWS"] = InventionType.Skill });

        report.IsValid.Should().BeFalse();
        report.Inventions.Should().ContainSingle(i => i.Claimed == "AWS" && i.Type == InventionType.Skill);
    }

    [Fact]
    public void Should_detect_company_not_in_original()
    {
        var original = new[] { "Acme" };
        var adapted = new[] { "Globex" };

        var report = _validator.Validate(
            original,
            adapted,
            new Dictionary<string, InventionType> { ["Acme"] = InventionType.Company, ["Globex"] = InventionType.Company });

        report.Inventions.Should().ContainSingle(i => i.Claimed == "Globex" && i.Type == InventionType.Company);
    }

    [Fact]
    public void Should_detect_date_not_in_original()
    {
        var original = new[] { "01/2020" };
        var adapted = new[] { "01/2020", "12/2023" };

        var report = _validator.Validate(
            original,
            adapted,
            new Dictionary<string, InventionType> { ["01/2020"] = InventionType.Date, ["12/2023"] = InventionType.Date });

        report.Inventions.Should().ContainSingle(i => i.Claimed == "12/2023" && i.Type == InventionType.Date);
    }

    [Fact]
    public void Should_detect_certification_not_in_original()
    {
        var original = Array.Empty<string>();
        var adapted = new[] { "AWS Certified" };

        var report = _validator.Validate(
            original,
            adapted,
            new Dictionary<string, InventionType> { ["AWS Certified"] = InventionType.Certification });

        report.Inventions.Should().ContainSingle(i => i.Claimed == "AWS Certified" && i.Type == InventionType.Certification);
    }

    [Fact]
    public void Should_not_flag_legitimate_skill_match()
    {
        var original = new[] { "C#" };
        var adapted = new[] { "C#" };

        var report = _validator.Validate(
            original,
            adapted,
            new Dictionary<string, InventionType> { ["C#"] = InventionType.Skill });

        report.IsValid.Should().BeTrue();
        report.Inventions.Should().BeEmpty();
    }

    [Fact]
    public void Should_handle_empty_original_entities()
    {
        var original = Array.Empty<string>();
        var adapted = new[] { "C#", ".NET" };

        var report = _validator.Validate(
            original,
            adapted,
            new Dictionary<string, InventionType>());

        report.Inventions.Should().HaveCount(2);
    }

    [Fact]
    public void Should_mark_hard_inventions_correctly()
    {
        var original = Array.Empty<string>();
        var adapted = new[] { "Acme Corp" };

        var report = _validator.Validate(
            original,
            adapted,
            new Dictionary<string, InventionType> { ["Acme Corp"] = InventionType.Company });

        report.Inventions.Should().ContainSingle()
            .Which.InventionSeverity.Should().Be(InventionSeverity.Hard);
    }

    [Fact]
    public void Should_mark_soft_inventions_correctly()
    {
        var original = new[] { "C#" };
        var adapted = new[] { "C#", "AWS" };

        var report = _validator.Validate(
            original,
            adapted,
            new Dictionary<string, InventionType> { ["C#"] = InventionType.Skill, ["AWS"] = InventionType.Skill });

        report.Inventions.Should().ContainSingle()
            .Which.InventionSeverity.Should().Be(InventionSeverity.Soft);
    }
}
