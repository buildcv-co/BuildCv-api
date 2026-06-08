namespace BuildCv.Application.Features.Adapt;

public sealed record AdaptCvCommand(
    string CvText,
    string JobText);
