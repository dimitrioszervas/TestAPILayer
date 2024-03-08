using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace TestAPILayer
{
    static class KeyDerivation
    {
        private enum KeyType
        {
            SIGN,
            ENCRYPT
        }

        public static string ByteArrayToString(byte[] bytes)
        {
            var sb = new StringBuilder("[");
            sb.Append(string.Join(", ", bytes));
            sb.Append("]");
            return sb.ToString();
        }

        private static List<byte[]> GenerateNKeys(int n, byte[] src, KeyType type, byte[] baseKey)
        {
            List<byte[]> keys = new List<byte[]>();
            byte[] salt, info;

            if (type == KeyType.SIGN)
            {
                salt = src; // salt needed to generate keys
                info = Encoding.UTF8.GetBytes("signs");
            }
            else
            {

                salt = src; // salt needed to generate keys
                info = Encoding.UTF8.GetBytes("encrypts");
            }

            for (int i = 0; i <= n; i++)
            {
                byte[] key = HKDF.DeriveKey(HashAlgorithmName.SHA256, baseKey, 32, salt, info);
                keys.Add(key);
            }

            return keys;
        }

        public static void GenerateKeys(ref List<byte[]> encrypts, ref List<byte[]> signs, ref byte[] srcOut, string secretString, int n)
        {
            string saltString = "";


            Console.WriteLine($"secret string: {secretString}");
            Console.WriteLine();

            byte[] secret = Encoding.UTF8.GetBytes(secretString);
            byte[] salt = Encoding.UTF8.GetBytes(saltString);


            Console.WriteLine($"secret: {ByteArrayToString(secret)}");
            Console.WriteLine();

            byte[] src = HKDF.DeriveKey(hashAlgorithmName: HashAlgorithmName.SHA256,
                                        ikm: secret,
                                        outputLength: 8,
                                        salt: salt,
                                        info: Encoding.UTF8.GetBytes("src"));

            salt = src;

            Console.WriteLine($"src: {ByteArrayToString(src)}");
            Console.WriteLine();

            byte[] sign = HKDF.DeriveKey(hashAlgorithmName: HashAlgorithmName.SHA256,
                                         ikm: secret,
                                         outputLength: 32,
                                         salt: salt,
                                         info: Encoding.UTF8.GetBytes("sign"));

            Console.WriteLine($"sign: {ByteArrayToString(sign)}");
            Console.WriteLine();

            byte[] encrypt = HKDF.DeriveKey(hashAlgorithmName: HashAlgorithmName.SHA256,
                                           ikm: secret,
                                           outputLength: 32,
                                           salt: salt,
                                           info: Encoding.UTF8.GetBytes("encrypt"));

            Console.WriteLine($"encrypt: {ByteArrayToString(encrypt)}");
            Console.WriteLine();

            encrypts = GenerateNKeys(n, salt, KeyType.ENCRYPT, encrypt);
            signs = GenerateNKeys(n, salt, KeyType.SIGN, sign);

            srcOut = new byte[src.Length];
            Array.Copy(src, srcOut, src.Length);
        }
    }
}
