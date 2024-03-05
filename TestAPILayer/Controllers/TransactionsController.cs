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

        public static byte[] AESDecrypt(byte[] encryptedBytes, byte[] aesKey)
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
            Console.WriteLine($"SRC: {Encoding.UTF8.GetString(src)}");
            Console.WriteLine();

            // allocate memory for the data shards byte matrix
            // Last element in the string array is not a shard but the SRC array 
            int numShards = stringArray.Count - 1;
            byte[][] dataShards = new byte[numShards][];
            for (int i = 0; i < numShards; i++)
            {
                // convert string to bytes
                byte[] shardBytes = StringToBytes(stringArray[i]);

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

            CBORObject shardsCBOR = CBORObject.DecodeFromBytes(shardsCBORBytes);

            Console.WriteLine(shardsCBOR.ToJSONString());
           
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
            CBORObject rebuiltDataCBOR = CBORObject.DecodeFromBytes(StripPadding(rebuiltDataBytes));
            string rebuiltDataString = rebuiltDataCBOR.ToJSONString();
                                 
            Console.WriteLine($"Rebuilt Data: {rebuiltDataString}");
            Console.WriteLine();

            return Ok(rebuiltDataCBOR.ToJSONString());            
           
        }
    }
}
