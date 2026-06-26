namespace BuildCv.Domain.Scoring;

/// <summary>
/// Severidad cualitativa de una <see cref="RedFlag"/>. La etiqueta viaja
/// al cliente tal cual; el motor no la usa para deducir puntaje (Art. I:
/// cero invención, una señal nunca resta).
/// </summary>
public enum RedFlagSeverity
{
    Low,
    Medium,
    High,
}

/// <summary>
/// Señal observable en el CV que el motor reporta al usuario sin alterar
/// el número (Art. I: una red flag es información, nunca deducción). El
/// <see cref="Code"/> es estable y consumible por la UI para i18n; el
/// <see cref="Message"/> es legible y orientado al candidato.
/// </summary>
/// <remarks>
/// Se usa un record no posicional con constructor explícito para mantener
/// la validación de argumentos en el constructor primario. El compilador
/// Roslyn del SDK .NET 10.0.108 (Linux) tiene una regresión que rechaza
/// sentencias ejecutables (<c>if</c>/<c>throw</c>) en el cuerpo de un
/// record posicional; el workaround conserva el contrato de construcción
/// posicional desde el call site (misma firma <c>(string,
/// RedFlagSeverity, string)</c>) sin sacrificar la validación.
/// </remarks>
public sealed record RedFlag
{
    public string Code { get; }

    public RedFlagSeverity Severity { get; }

    public string Message { get; }

    public RedFlag(string code, RedFlagSeverity severity, string message)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            throw new ArgumentException("Code is required.", nameof(code));
        }

        if (string.IsNullOrWhiteSpace(message))
        {
            throw new ArgumentException("Message is required.", nameof(message));
        }

        Code = code;
        Severity = severity;
        Message = message;
    }
}
