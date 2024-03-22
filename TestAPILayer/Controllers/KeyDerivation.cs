using System;
using System.Security.Cryptography;
using System.Text;

public static class KeyGenerationService
{
    public static byte[] DeriveKey(string secret, string src, string type, int n, int outputBytes)
    {
        byte[] salt = Encoding.UTF8.GetBytes(src);
        byte[] info = Encoding.UTF8.GetBytes(type == "sign" ? $"SIGNS{n}" : $"ENCRYPTS{n}");
        byte[] secretBytes = Encoding.UTF8.GetBytes(secret);

        using (var hkdf = new HKDFSHA256())
        {
            return hkdf.DeriveKey(salt, secretBytes, info, outputBytes);
        }
    }
}