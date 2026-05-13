using System.Security.Cryptography;
using System.Text;

namespace ManufacturingERP.Application.Services;

public class PasswordHasherService
{
    private const int SaltSize = 16;
    private const int HashSize = 32;
    private const int Iterations = 210_000;
    private const string Algorithm = "PBKDF2-SHA256";

    public string Hash(string input)
    {
        ArgumentNullException.ThrowIfNull(input);

        var salt = RandomNumberGenerator.GetBytes(SaltSize);
        var hash = Rfc2898DeriveBytes.Pbkdf2(
            input,
            salt,
            Iterations,
            HashAlgorithmName.SHA256,
            HashSize);

        return $"{Algorithm}${Iterations}${Convert.ToBase64String(salt)}${Convert.ToBase64String(hash)}";
    }

    public bool Verify(string input, string hash)
    {
        if (string.IsNullOrWhiteSpace(hash)) return false;
        if (string.IsNullOrEmpty(input)) return false;

        if (IsLegacySha256Hash(hash))
            return string.Equals(CreateLegacySha256Hash(input), hash, StringComparison.OrdinalIgnoreCase);

        var parts = hash.Split('$');
        if (parts.Length != 4 || !string.Equals(parts[0], Algorithm, StringComparison.Ordinal))
            return false;

        if (!int.TryParse(parts[1], out var iterations) || iterations <= 0)
            return false;

        try
        {
            var salt = Convert.FromBase64String(parts[2]);
            var expectedHash = Convert.FromBase64String(parts[3]);
            var actualHash = Rfc2898DeriveBytes.Pbkdf2(
                input,
                salt,
                iterations,
                HashAlgorithmName.SHA256,
                expectedHash.Length);

            return CryptographicOperations.FixedTimeEquals(actualHash, expectedHash);
        }
        catch (FormatException)
        {
            return false;
        }
    }

    public bool NeedsRehash(string hash)
    {
        if (string.IsNullOrWhiteSpace(hash) || IsLegacySha256Hash(hash))
            return true;

        var parts = hash.Split('$');
        return parts.Length != 4 ||
            !string.Equals(parts[0], Algorithm, StringComparison.Ordinal) ||
            !int.TryParse(parts[1], out var iterations) ||
            iterations < Iterations;
    }

    private static bool IsLegacySha256Hash(string hash)
        => hash.Length == 64 && hash.All(Uri.IsHexDigit);

    private static string CreateLegacySha256Hash(string input)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(input));
        return Convert.ToHexString(bytes);
    }
}
