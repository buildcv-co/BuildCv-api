namespace BuildCv.Application.Features.Import;

/// <summary>
/// Excepción de motor: el parser encontró algo que sabe clasificar (PDF cifrado,
/// DOCX protegido, etc.) y lo traduce a un código estable mapeable a HTTP.
/// Vive en Application porque el handler (Application) la lanza tras mapear
/// el código que el adaptador (Infrastructure) reporta vía ParserEngineException.
/// </summary>
public sealed class ParserEngineException : Exception
{
    public string Code { get; }

    public ParserEngineException(string code, string message)
        : base(message)
    {
        Code = code;
    }
}
