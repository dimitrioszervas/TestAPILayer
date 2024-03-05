using Microsoft.AspNetCore.Mvc;
using System.Text;
using PeterO.Cbor;
using Newtonsoft.Json;
using System.Security.Cryptography;


namespace TestAPILayer.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class TransactionsController : ControllerBase
    {
        private enum KeyType {
            SIGN,
            ENCRYPT
        }

        private const string SECRET_STRING = "SECRET";
        private const string ENCRYPT_STRING = "ENCRYPT";
        private const string SIGN_STRING = "SIGN";
        private const string ENCRYPTS_STRING = "ENCRYPTS";
        private const string SIGNS_STRING = "SIGNS";

        private static byte[] SECRET;
        private static byte[] SRC;
        private static byte[] ENCRYPT;
        private static byte[] SIGN;

        private static List<byte[]> ENCRYPTS;
        private static List<byte[]> SIGNS;

        // Class that represents a JSON Array of strings used to map a JSON array of strings
        // CBOR arrives as string arrays from frontend and we use this object to
        // map the CBOR JSON arrays.
        sealed class JSONArray
        {
            public List<string> values { set; get; } = new List<string>();
        }

 
        // Converts a byte array to a CBOR C# object using memory stream.
        private static CBORObject BytesToCBORObject(byte[] bytes)
        {
            using (var stream = new MemoryStream(bytes))
            {
                // Read the CBOR object from the stream
                var cbor = CBORObject.Read(stream);
                return cbor;
            }
        } 

        private static List<byte []> GenerateNKeys(int n, byte [] SRC, KeyType type, byte [] baseKey)
        {
            List<byte[]> keys = new List<byte[]>();
            byte[] salt, info;

            if (type == KeyType.SIGN)
            {
                salt = SRC; // salt needed to generate keys
                info = Encoding.UTF8.GetBytes(SIGNS_STRING + n);
            }
            else
            {
               
                salt = SRC; // salt needed to generate keys
                info = Encoding.UTF8.GetBytes(ENCRYPTS_STRING + n);                
            }

            for (int i = 0; i < n; i++)
            {              
                byte[] key = HKDF.DeriveKey(HashAlgorithmName.SHA256, baseKey, 32, salt, info);
                keys.Add(key);
            }

            return keys;
        }

        // Genarates keys in the same way as the JavaScript
        // protocol.js module but the key that are generated
        // are not matching the JavaScript version
        private static void GenerateKeys(int n)
        {
            byte[] secretBytes = Encoding.UTF8.GetBytes(SECRET_STRING);
            //SECRET = HKDF.Extract(HashAlgorithmName.SHA256, secretBytes);
            SECRET = HKDF.DeriveKey(HashAlgorithmName.SHA256, secretBytes, secretBytes.Length);
            Console.WriteLine(SECRET.Length);
            SRC = HKDF.DeriveKey(HashAlgorithmName.SHA256, SECRET, 8, null, null);
            Console.WriteLine(SRC.Length);
            Console.WriteLine($"API Layer's SRC: {Encoding.UTF8.GetString(SRC)}");
            ENCRYPT = HKDF.DeriveKey(HashAlgorithmName.SHA256, SECRET, 32, SRC, Encoding.UTF8.GetBytes(ENCRYPT_STRING));
            //ENCRYPT = HKDF.DeriveKey(HashAlgorithmName.SHA256, ENCRYPT, 32, SRC, Encoding.UTF8.GetBytes(ENCRYPT_STRING));
            SIGN = HKDF.DeriveKey(HashAlgorithmName.SHA256, SECRET, 32, SRC, Encoding.UTF8.GetBytes(SIGN_STRING));

            ENCRYPTS = GenerateNKeys(n, SRC, KeyType.ENCRYPT, ENCRYPT);
            SIGNS = GenerateNKeys(n, SRC, KeyType.SIGN, SIGN);
        }

        // Converts a byte string to a Base64 string
        private static string ConvertStringToBase64(string encoded)
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

        // Strips padding addded during the Reed-Solomon sharding
        private static byte[] StripPadding(byte[] paddedData)
        {
            try
            {
                int padding = 1;
                for (int i = paddedData.Length - 1; i >= 0; i--)
                {
                    if (paddedData[i] == 0)
                    {
                        padding++;
                    }
                    else
                    {
                        break;
                    }
                }

                byte[] strippedData = new byte[paddedData.Length - padding];
                Array.Copy(paddedData, 0, strippedData, 0, strippedData.Length);

                return strippedData;
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
                throw;
            }
        }       

        // we map the JSON array string to a C# List object.
        private static List<string> JSONArrayToList(string jsonArrayString)
        {
            return JsonConvert.DeserializeObject<JSONArray>("{\"values\":" + jsonArrayString + "}").values;
        }

        // converts a CBOR byte string to bytes
        private static byte [] StringToBytes(string str)
        {
            string base64String = ConvertStringToBase64(str);

            // convert base64 string to bytes
            return Convert.FromBase64String(base64String);
        }

        public static byte[] AESDecrypt(byte[] encryptedBytes, byte[] aesKey, byte[] iv)
        {

            byte[] decryptedBytes;
            // Create an Aes object
            // with the specified aesKey and IV.
            using (Aes aes = Aes.Create())
            {

                try
                {
                    aes.Key = aesKey;
                    aes.IV = new byte[16];
                    Array.Copy(iv, aes.IV, iv.Length);

                    // Create an decryptor to perform the stream transform.
                    ICryptoTransform decryptor = aes.CreateDecryptor(aes.Key, aes.IV);

                    // Create the streams used for decryption.
                    using (MemoryStream msDecrypt = new MemoryStream(encryptedBytes))
                    {
                        using (CryptoStream csDecrypt = new CryptoStream(msDecrypt, decryptor, CryptoStreamMode.Read))
                        {
                            decryptedBytes = new byte[encryptedBytes.Length];
                            csDecrypt.Read(decryptedBytes, 0, decryptedBytes.Length);
                        }
                    }

                }
                catch (Exception ex)
                {
                    throw new Exception($"{ex.Message} aesKey size: {aesKey.Length}");
                }
            }

            return decryptedBytes;
        }

        // Extracts the shards from the JSON string an puts the to a 2D byte array (matrix)
        // needed for rebuilding the data using Reed-Solomon.
        private static byte[][] GetShardsFromJSON(string jsonArrayString)
        {
            Console.WriteLine();
            // we map the JSON array string to a C# object.
            var stringArray = JSONArrayToList(jsonArrayString);
            
            byte [] src = StringToBytes(stringArray[stringArray.Count - 1]);
            Console.WriteLine($"Received SRC from test-ui: {Encoding.UTF8.GetString(src)}");
            Console.WriteLine();

            // allocate memory for the data shards byte matrix
            // Last element in the string array is not a shard but the SRC array 
            int numShards = stringArray.Count - 1;
            GenerateKeys(numShards);
            
            byte[][] dataShards = new byte[numShards][];
            for (int i = 0; i < numShards; i++)
            {
                // convert string to bytes
                byte[] encryptedShardBytes = StringToBytes(stringArray[i]);
                byte[] shardBytes = AESDecrypt(encryptedShardBytes, ENCRYPTS[i], SRC);

                // Write to console out for debug
                Console.WriteLine($"shard[{i}]: {Encoding.UTF8.GetString(shardBytes)}");

                // copy shard to shard matrix
                dataShards[i] = new byte [shardBytes.Length];
                Array.Copy(shardBytes, dataShards[i], shardBytes.Length);
            }
          
            Console.WriteLine();

            return dataShards;
        }

        // Converts the binary string received from the request to bytes.
        private static byte [] BinaryStringToBytes(string binaryString)
        {
            byte[] binaryStringBytes = new byte[binaryString.Length];
            for (int i = 0; i < binaryStringBytes.Length; i++)
            {
                binaryStringBytes[i] = (byte)binaryString.ElementAt(i);
            }

            return binaryStringBytes;
        }
              
        // Transaction endpoint
        [HttpPost]
        [Route("PostTransaction")]       
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult> PostTransaction()
        {          
            // Get the binary string from the request 
            string binaryString = "";
            using (var reader = new StreamReader(Request.Body))
            {
                binaryString = await reader.ReadToEndAsync();
                Console.WriteLine("----------------------------------------------------------------------");
                Console.WriteLine($"Binary String Received: {binaryString}");               
            }

            // Convert binary string to a byte array.  
            byte[] binaryStringBytes = BinaryStringToBytes(binaryString);

            // Decode binary string's CBOR bytes  
            CBORObject binaryStringCBOR = CBORObject.DecodeFromBytes(binaryStringBytes);
           
            Console.WriteLine("----------------------------------------------------------------------");
            Console.WriteLine($"Binary String CBOR to JSON: {binaryStringCBOR.ToJSONString()}");
            Console.WriteLine("----------------------------------------------------------------------");

            var transanctionValues = JSONArrayToList(binaryStringCBOR.ToJSONString());

            byte[] shardsCBORBytes = StringToBytes(transanctionValues[0]);
            byte[] hmacResultCBORBytes = StringToBytes(transanctionValues[1]);


            CBORObject shardsCBOR = CBORObject.DecodeFromBytes(shardsCBORBytes);

            //CBORObject hmacResultCBOR = CBORObject.DecodeFromBytes(hmacResultCBORBytes);

            Console.WriteLine($"Shards CBOR to JSON: {shardsCBOR.ToJSONString()}");
            //Console.WriteLine($"hmac result CBOR to JSON: {hmacResultCBOR.ToJSONString()}");

            // Extract the shards from the JSON string and put them in byte matrix (2D array of bytes).
            byte [][] shards = GetShardsFromJSON(shardsCBOR.ToJSONString());
            Console.WriteLine("----------------------------------------------------------------------");
            
            // Get shard length (all shards are of equal length).
            int shardLength = shards[0].Length;

            int nTotalShards = shards.Length;
            int nParityShards = nTotalShards / 2;
            int nDataShards = nTotalShards - nParityShards;

            Console.WriteLine($"Total number of shards: {nTotalShards}");
            Console.WriteLine($"Number of data shards: {nDataShards}");
            Console.WriteLine($"Number of parity shards: {nParityShards}");
            Console.WriteLine("----------------------------------------------------------------------");

            // Set which shards are present - you have to have a minimum number = number of data shards
            bool[] shardsPresent = new bool[nTotalShards];

            for (int i = 0; i < nDataShards; i++)
            {
                shardsPresent[i] = true;
            }                       

            // Replicate the other shards using Reeed-Solomom.
            var reedSolomon = new ReedSolomon.ReedSolomon(shardsPresent.Length - nParityShards, nParityShards);
            reedSolomon.DecodeMissing(shards, shardsPresent, 0, shardLength);
            
            // Write the Reed-Solomon matrix of shards to a 1D array of bytes
            byte[] rebuiltDataBytes = new byte[shards.Length * shardLength];
            int offSet = 0;
            for (int j = 0; j < shards.Length - nParityShards; j++)
            {
                Array.Copy(shards[j], 0, rebuiltDataBytes, offSet, shardLength);
                offSet += shardLength;
            }

            // Decode rebuilt CBOR data bytes, after stripping the padding needed for the Reed-Solomon
            // which requires that all shards have to be equal in length. 
            byte[] cborDataBytes = StripPadding(rebuiltDataBytes);
            Console.WriteLine($"CBOR Data bytes: {Encoding.UTF8.GetString(cborDataBytes)}");

            CBORObject rebuiltDataCBOR = CBORObject.DecodeFromBytes(cborDataBytes);
            
            string rebuiltDataString = rebuiltDataCBOR.ToJSONString();
                                 
            Console.WriteLine($"Rebuilt Data: {rebuiltDataString}");
            Console.WriteLine();

            return Ok(rebuiltDataCBOR.ToJSONString());
         

            return Ok("API Layer Rsponded!");
        }
    }
}
