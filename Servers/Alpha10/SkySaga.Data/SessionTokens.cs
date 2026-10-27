using System.Security.Cryptography;
using System.Text;

namespace SkySaga.Data;

public static class SessionTokens
{
    public static string New() => Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(32));

    public static byte[] Hash(string token) => SHA256.HashData(Encoding.UTF8.GetBytes(token));
}
