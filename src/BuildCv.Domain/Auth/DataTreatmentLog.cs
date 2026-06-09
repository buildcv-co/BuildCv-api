namespace BuildCv.Domain.Auth;

public sealed record DataTreatmentLog
{
    public Guid Id { get; init; }
    public Guid UserId { get; init; }
    public string DataType { get; init; } = "";
    public string Action { get; init; } = "";
    public DateTime Timestamp { get; init; }
    public string Reason { get; init; } = "";
}
