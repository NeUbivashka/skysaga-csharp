using System.Security.Cryptography;
using System.Text;
using Konscious.Security.Cryptography;

namespace SkySaga.Auth.Services;

public static class PasswordHasher
{
    private const int MemoryKib = 65536;
    private const int Iterations = 3;
    private const int Parallelism = 4;
    private const int SaltBytes = 16;
    private const int HashBytes = 32;

    public static string Hash(string password)
    {
        var salt = RandomNumberGenerator.GetBytes(SaltBytes);
        var hash = Compute(password, salt, MemoryKib, Iterations, Parallelism, HashBytes);

        return $"$argon2id$v=19$m={MemoryKib},t={Iterations},p={Parallelism}${Convert.ToBase64String(salt)}${Convert.ToBase64String(hash)}";
    }

    public static bool Verify(string password, string? encoded)
    {
        if (encoded?.Split('$') is not ["", "argon2id", "v=19", var parameters, var saltText, var hashText])
            return false;

        var values = parameters.Split(',').Select(part => part.Split('=')).ToDictionary(pair => pair[0], pair => int.Parse(pair[1]));
        var expected = Convert.FromBase64String(hashText);
        var actual = Compute(string.IsNullOrEmpty(password) ? "\0" : password, Convert.FromBase64String(saltText), values["m"], values["t"], values["p"], expected.Length);

        return CryptographicOperations.FixedTimeEquals(actual, expected) && !string.IsNullOrEmpty(password);
    }

    private static byte[] Compute(string password, byte[] salt, int memoryKib, int iterations, int parallelism, int length)
    {
        using var argon = new Argon2id(Encoding.UTF8.GetBytes(password))
        {
            Salt = salt,
            MemorySize = memoryKib,
            Iterations = iterations,
            DegreeOfParallelism = parallelism
        };

        return argon.GetBytes(length);
    }
}
