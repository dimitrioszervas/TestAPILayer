using Microsoft.AspNetCore.DataProtection.KeyManagement;
using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.Crypto.Engines;
using Org.BouncyCastle.Crypto.Parameters;
using System.Security.Cryptography;
using System.Text;

namespace TestAPILayer
{

    static class CryptoUtils
    {
        public class ECDHKeyPair
        {
            public byte[] PublicKey {  get; set; }
            public byte[] PrivateKey {  get; set; }
        }

        private enum KeyType
        {
            SIGN,
            ENCRYPT
        }
        
        public const string OWNER_CODE = "1234";
       
        public const int NUM_KEYS = Servers.NUM_SERVERS + 1;

        public const int KEY_SIZE32 = 32;

        public const int TAG_SIZE = 16;
        public const int IV_SIZE = 12;

        public const int SRC_SIZE8 = 8;

        public static byte[] Decrypt(byte[] cipherData, byte[] key, byte[] nonceIn)
        {

            // get raw privateKey spans
            var encryptedData = cipherData.AsSpan();

            var tagSizeBytes = 16; // 128 bit encryption / 8 bit = 16 privateKey           

            // ciphertext size is whole data - nonce - tag
            var cipherSize = encryptedData.Length - tagSizeBytes;

            // extract nonce (nonce) 12 privateKey prefix          
            byte[] nonce = new byte[12];
            Array.Copy(nonceIn, nonce, 8);

            // followed by the real ciphertext
            var cipherBytes = encryptedData.Slice(0, cipherSize);

            // followed by the tag (trailer)
            var tagStart = cipherSize;
            var tag = encryptedData.Slice(tagStart);

            // now that we have all the parts, the decryption
            Span<byte> plainBytes = cipherSize < 1024
                ? stackalloc byte[cipherSize]
                : new byte[cipherSize];
            using var aes = new AesGcm(key);
            aes.Decrypt(nonce, cipherBytes, tag, plainBytes);
            return plainBytes.ToArray();
        }

        /*

        public static byte[] Decrypt(byte[] encryptedData, byte[] cngPublicKey, byte[] nonceIn)
        {
            var ciphertext = encryptedData[0..^16];
            var tag = encryptedData[^16..];
            byte[] decrytedBytes = new byte[ciphertext.Length];
            try
            {
                var aes = new AesGcm(cngPublicKey);

                byte[] nonce = new byte[12];
                Array.Copy(nonceIn, nonce, 8);

                aes.Decrypt(nonce, ciphertext, tag, decrytedBytes);
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
                Console.WriteLine(ex.StackTrace);
            }

            return decrytedBytes;
        }
        */
       
        public static byte[] Decrypt(byte[] encryptedData, byte[] key, byte[] src, byte[] tag)
        {
            var ciphertext = encryptedData;// encryptedData[0..^16];
                                           //var tag = new byte[16];// encryptedData[^16..];
            byte[] decrytedBytes = new byte[ciphertext.Length];
            try
            {
                var aes = new AesGcm(key, TAG_SIZE);

                byte[] iv = new byte[IV_SIZE];
                Array.Copy(src, iv, 8);

                aes.Decrypt(iv, ciphertext, tag, decrytedBytes);
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
                Console.WriteLine(ex.StackTrace);
            }

            return decrytedBytes;
        }

        public static void Encrypt(byte[] plainBytes, byte[] key, byte[] src, ref byte[] ciphertext, ref byte[] tag)
        {
            var aes = new AesGcm(key, TAG_SIZE);

            byte[] iv = new byte[IV_SIZE];
            Array.Copy(src, iv, 8);

            aes.Encrypt(iv, plainBytes, ciphertext, tag);
        }

        public static string ByteArrayToStringDebug(byte[] bytes)
        {
            var sb = new StringBuilder("[");
            sb.Append(string.Join(", ", bytes));
            sb.Append("]");
            return sb.ToString();
        }

        public static byte[] DeriveHKDFKey(byte[] ikm, int outputLength, byte [] salt, byte [] info)
        {
            byte[] key = HKDF.DeriveKey(hashAlgorithmName: HashAlgorithmName.SHA256,
                                       ikm: ikm,
                                       outputLength: outputLength,
                                       salt: salt,
                                       info: info);
            return key;
        }

        public static byte[] DeriveHKDF32Key(byte[] ikm, byte[] salt, byte[] info)
        {
            byte[] key = DeriveHKDFKey(ikm: ikm,
                                       outputLength: 32,
                                       salt: salt,
                                       info: info);
            return key;
        }

        public static byte[] DeriveHKDF8Key(byte[] ikm, byte[] salt, byte[] info)
        {
            byte[] key = DeriveHKDFKey(ikm: ikm,
                                       outputLength: 8,
                                       salt: salt,
                                       info: info);
            return key;
        }

        // For ECDH instead of ECDSA, change 0x53 to 0x4B.
        //private static readonly byte[] s_cngBlobPrefix = { 0x45, 0x43, 0x53, 0x31, 0x20, 0, 0, 0 };
        private static readonly byte[] s_cngBlobPrefix = { 0x45, 0x43, 0x4B, 0x31, 0x20, 0, 0, 0 };

        public static CngKey ImportPublicKeyToCngKey(byte[] publicKey)
        {
            var keyType = new byte[] { 0x45, 0x43, 0x53, 0x31 };
            var keyLength = new byte[] { 0x20, 0x00, 0x00, 0x00 };

            byte[] key = new byte[publicKey.Length - 1];
            for (int i = 1; i < publicKey.Length; i++)
            {
                key[i - 1] = publicKey[i];
            }
            var keyImport = keyType.Concat(keyLength).Concat(key).ToArray();
         
            var cngKey = CngKey.Import(keyImport, CngKeyBlobFormat.EccPublicBlob);

            return cngKey;
        }

        public static byte[] DeriveECDHKey(byte[] publicKey, byte [] privateKey)
        {           
            CngKey cngPrivateKey = CngKey.Import(privateKey, CngKeyBlobFormat.EccPrivateBlob);
            
            CngKey cngPublicKey = ImportPublicKeyToCngKey(publicKey);
           
            using (ECDiffieHellmanCng ecDiffieHellmanCng = new ECDiffieHellmanCng(cngPrivateKey))
            {
                ecDiffieHellmanCng.KeyDerivationFunction = ECDiffieHellmanKeyDerivationFunction.Hash;
                ecDiffieHellmanCng.HashAlgorithm = CngAlgorithm.Sha256;

                byte[] derivedECDHKey = null;// ecDiffieHellmanCng.DeriveKeyMaterial(cngPublicKey);
                
                return derivedECDHKey;
            }
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
                byte[] key = DeriveHKDFKey(baseKey, KEY_SIZE32, salt, info);
                keys.Add(key);
            }

            return keys;
        }

        public static void GenerateKeys(ref List<byte[]> encrypts, ref List<byte[]> signs, ref byte[] srcOut, byte [] secret, byte[] salt, int n)
        {    
            byte[] src = DeriveHKDF8Key(secret, salt, Encoding.UTF8.GetBytes("src"));

            salt = src;

            byte[] sign = DeriveHKDF32Key(secret, salt, Encoding.UTF8.GetBytes("sign"));           

            byte[] encrypt = DeriveHKDF32Key(secret, salt, Encoding.UTF8.GetBytes("encrypt"));           

            encrypts = GenerateNKeys(n, salt, KeyType.ENCRYPT, encrypt);
            signs = GenerateNKeys(n, salt, KeyType.SIGN, sign);

            srcOut = new byte[src.Length];
            Array.Copy(src, srcOut, src.Length);
        }

        private static byte [] ComputeHash(byte [] key, byte [] data)
        {          
            var hmac = new HMACSHA256(key);           

            return hmac.ComputeHash(data);
        }

        public static bool HashIsValid(byte [] key, byte [] data, byte [] hmacResult)
        {
            ReadOnlySpan<byte> hashBytes = ComputeHash(key, data);          

            return CryptographicOperations.FixedTimeEquals(hashBytes, hmacResult);
        }

        public static string ConvertStringToBase64(string encoded)
        {
            encoded = encoded.Replace('-', '+').Replace('_', '/');
            var d = encoded.Length % 4;
            if (d != 0)
            {
                encoded = encoded.TrimEnd('=');
                encoded += d % 2 > 0 ? "=" : "==";
            }

            return encoded;
        }

        public static byte[] CBORBinaryStringToBytes(string s)
        {
            return Convert.FromBase64String(ConvertStringToBase64(s));
        }

     
        public static ECDHKeyPair CreateECDH()
        {
            var ecdh = new ECDiffieHellmanCng(CngKey.Create(CngAlgorithm.ECDiffieHellmanP256, null, new CngKeyCreationParameters { ExportPolicy = CngExportPolicies.AllowPlaintextExport }));
            var privateKey = ecdh.Key.Export(CngKeyBlobFormat.EccPrivateBlob);
            var publickey = ecdh.Key.Export(CngKeyBlobFormat.EccPublicBlob);
            ECDHKeyPair keyPair = new ECDHKeyPair();

            keyPair.PublicKey = publickey;
            keyPair.PrivateKey = privateKey;

            return keyPair;
        }

        public static byte[] Unwrap(byte[] wrappedData, byte[] key)
        {
            ICipherParameters keyParam = new KeyParameter(key); 
           
            var symmetricBlockCipher = new AesEngine();
            Rfc3394WrapEngine wrapEngine = new Rfc3394WrapEngine(symmetricBlockCipher);       

            wrapEngine.Init(false, keyParam);
            var unwrappedData = wrapEngine.Unwrap(wrappedData, 0, wrappedData.Length);

            return unwrappedData;            
        }

        public static void GenerateOwnerKeys()
        {
            List<byte[]> encrypts = new List<byte[]>();
            List<byte[]> signs = new List<byte[]>();
            byte[] ownerID = new byte[SRC_SIZE8];
            string ownerCode = OWNER_CODE;

            string saltString = "";

            byte[] secret = Encoding.UTF8.GetBytes(ownerCode);
            byte[] salt = Encoding.UTF8.GetBytes(saltString);

            GenerateKeys(ref encrypts, ref signs, ref ownerID, secret, salt, Servers.NUM_SERVERS);

            KeyStore.Inst.StoreENCRYPTS(ownerID, encrypts);
            KeyStore.Inst.StoreSIGNS(ownerID, signs);
        }
    }
}
