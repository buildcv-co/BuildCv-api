namespace BuildCv.Domain.Text;

/// <summary>Reduce una palabra (ya normalizada, sin acentos) a una raíz aproximada.</summary>
public interface ISpanishStemmer
{
    string Stem(string word);
}
