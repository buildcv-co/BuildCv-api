namespace BuildCv.Domain.Text;

/// <summary>
/// Normaliza texto en español de forma determinista para el matching de skills.
/// Preserva la Ñ y los tokens técnicos cuya puntuación es significativa (c#, .net…).
/// </summary>
public interface ITextNormalizer
{
    /// <summary>Devuelve el texto normalizado (minúsculas, sin acentos salvo Ñ, sin puntuación irrelevante).</summary>
    string Normalize(string input);

    /// <summary>Normaliza y divide en tokens (palabras).</summary>
    IReadOnlyList<string> Tokenize(string input);
}
