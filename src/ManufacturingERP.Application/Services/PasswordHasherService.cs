using System.Security.Cryptography;
using System.Text;

namespace ManufacturingERP.Application.Services;

public class PasswordHasherService
{
    public string Hash(string input)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(input));
        return Convert.ToHexString(bytes);
    }

    public bool Verify(string input, string hash)
    {
        if (string.IsNullOrWhiteSpace(hash)) return false;
        return string.Equals(Hash(input), hash, StringComparison.OrdinalIgnoreCase);
    }
}
