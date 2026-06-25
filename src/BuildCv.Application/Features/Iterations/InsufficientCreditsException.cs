namespace BuildCv.Application.Features.Iterations;

public sealed class InsufficientCreditsException(int balance, int required)
    : Exception($"Insufficient credits: required {required}, balance {balance}.")
{
    public int Balance { get; } = balance;
    public int Required { get; } = required;
}
