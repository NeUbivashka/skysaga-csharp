using System.Net.Mail;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.DataProtection;

namespace SkySaga.Auth.Services;

public sealed class EmailProtector
{
    public const int MaxLength = 254;

    private readonly IDataProtector _protector;
    private readonly byte[] _indexKey;

    public EmailProtector(IDataProtectionProvider provider, string keyDirectory)
    {
        _protector = provider.CreateProtector("SkySaga.Auth.Email");

        var keyProtector = provider.CreateProtector("SkySaga.Auth.EmailIndexKey");
        var path = Path.Combine(keyDirectory, "email-index.key");

        if (File.Exists(path))
        {
            _indexKey = keyProtector.Unprotect(File.ReadAllBytes(path));
            return;
        }

        _indexKey = RandomNumberGenerator.GetBytes(32);
        Directory.CreateDirectory(keyDirectory);
        File.WriteAllBytes(path, keyProtector.Protect(_indexKey));
    }

    public static string Normalize(string email) => email.Trim().ToLowerInvariant();

    public static string? Problem(string? email)
    {
        if (string.IsNullOrWhiteSpace(email) || email.Trim().Length > MaxLength)
            return "Enter a valid e-mail address.";

        var normalized = Normalize(email);

        return MailAddress.TryCreate(normalized, out var address) && address.Address == normalized && address.Host.Contains('.')
            ? null
            : "Enter a valid e-mail address.";
    }

    public static bool LooksLikeEmail(string text) => text.Contains('@');

    public byte[] Index(string email) => HMACSHA256.HashData(_indexKey, Encoding.UTF8.GetBytes(Normalize(email)));

    public string Protect(string email) => _protector.Protect(Normalize(email));

    public string Unprotect(string value) => _protector.Unprotect(value);

    public static string Mask(string email)
    {
        var normalized = Normalize(email);
        var at = normalized.LastIndexOf('@');

        return at <= 0 ? "***" : $"{normalized[0]}***{normalized[at..]}";
    }
}
