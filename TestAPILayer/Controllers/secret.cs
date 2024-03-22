using System;
using System.Security.Cryptography;
using System.Text;

public class CryptoService
{
    public static byte[] DeriveKey(string secret, byte[] salt, byte[] info, int outputBytes)
    {
        var secretBytes = new UTF8Encoding().GetBytes(secret);
        using (var hkdf = new HKDFSHA256())
        {
            return hkdf.DeriveKey(salt, secretBytes, info, outputBytes);
        }
    }
}