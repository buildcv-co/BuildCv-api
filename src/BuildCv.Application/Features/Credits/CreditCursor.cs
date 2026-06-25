using System.Text;

namespace BuildCv.Application.Features.Credits;

public static class CreditCursor
{
    public static string Encode(DateTime createdAt, Guid id)
    {
        var raw = $"{createdAt.Ticks}:{id}";
        return Convert.ToBase64String(Encoding.UTF8.GetBytes(raw));
    }

    public static CreditCursorPosition? Decode(string? cursor)
    {
        if (string.IsNullOrWhiteSpace(cursor))
        {
            return null;
        }

        try
        {
            var bytes = Convert.FromBase64String(cursor);
            var raw = Encoding.UTF8.GetString(bytes);
            var parts = raw.Split(':', 2);
            if (parts.Length != 2)
            {
                return null;
            }

            if (!long.TryParse(parts[0], out var ticks))
            {
                return null;
            }

            if (!Guid.TryParse(parts[1], out var id))
            {
                return null;
            }

            return new CreditCursorPosition(new DateTime(ticks, DateTimeKind.Utc), id);
        }
        catch (FormatException)
        {
            return null;
        }
    }
}

public readonly record struct CreditCursorPosition(DateTime CreatedAt, Guid Id);
