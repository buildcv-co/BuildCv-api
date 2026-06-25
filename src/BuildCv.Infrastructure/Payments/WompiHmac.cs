using System.Security.Cryptography;
using System.Text;

namespace BuildCv.Infrastructure.Payments;

internal static class WompiHmac
{
    public static bool Verify(string? secret, string payload, string signatureHeader)
    {
        if (string.IsNullOrEmpty(signatureHeader) || string.IsNullOrEmpty(secret))
        {
            return false;
        }

        var keyBytes = Encoding.UTF8.GetBytes(secret);
        var payloadBytes = Encoding.UTF8.GetBytes(payload);

        using var hmac = new HMACSHA256(keyBytes);
        var computed = hmac.ComputeHash(payloadBytes);
        var expected = WompiAdapter.HexToBytes(signatureHeader);

        if (expected.Length != computed.Length)
        {
            return false;
        }

        return CryptographicOperations.FixedTimeEquals(computed, expected);
    }
}
