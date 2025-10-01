using System.Security.Cryptography;
using System.Text;

namespace MarineControl.Api.Security;

public static class HmacHelper
{
    public static string ComputeHex(string body, string plainKey)
    {
        using var h = new HMACSHA256(Encoding.UTF8.GetBytes(plainKey));
        var hash = h.ComputeHash(Encoding.UTF8.GetBytes(body));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    public static bool Verify(string body, string signatureHex, string plainKey)
    {
        var expected = ComputeHex(body, plainKey);
        // zaman sabitli karşılaştırma
        return CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(expected),
            Encoding.UTF8.GetBytes(signatureHex.ToLowerInvariant()));
    }
}
