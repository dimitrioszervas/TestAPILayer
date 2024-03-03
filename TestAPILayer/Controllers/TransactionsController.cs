using Microsoft.AspNetCore.Mvc;
using System.Text;
using PeterO.Cbor;
using Newtonsoft.Json;
using System.Collections;

namespace TestAPILayer.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class TransactionsController : ControllerBase
    {
        // JSON shard object
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

        private static byte[] GetCBORBytesFromJSON(string jsonString)
        {
            jsonString = jsonString.Replace("{", "");
            jsonString = jsonString.Replace("}", "");
            jsonString = jsonString.Replace("[", "");
            jsonString = jsonString.Replace("]", "");
            jsonString = jsonString.Replace(",", "");
            jsonString = jsonString.Replace("\"", "");
            
            Console.WriteLine(jsonString);
            string base64String = ConvertStringToBase64(jsonString);
            Console.WriteLine(base64String);
            byte[] bytes = Convert.FromBase64String(base64String);
            Console.WriteLine($"Number of bytes: {bytes.Length}");
            return bytes;
        }

        // we map the JSON array string to a C# List object.
        private static List<string> JSONArrayToList(string jsonArrayString)
        {
            return JsonConvert.DeserializeObject<JSONArray>("{\"values\":" + jsonArrayString + "}").values;
        }

        // Extracts the shards from the JSON string an puts the to a 2D byte array (matrix)
        // needed for rebuilding the data using Reed-Solomon.
        private static byte[][] GetShardsFromJSON(string jsonArrayString)
        {        
            // we map the JSON array string to a C# object.
            var stringShards = JSONArrayToList(jsonArrayString);

            // allocate memory for the data shards byte matrix  
            byte[][] dataShards = new byte[stringShards.Count][];
            for (int i = 0; i < stringShards.Count; i++)
            {
                // convert byte string to a base64 string 
                string shardBase64String = ConvertStringToBase64(stringShards[i]);  
                
                // convert base64 string to bytes
                byte[] shardBytes = Convert.FromBase64String(shardBase64String);

                // Write to console out for debug
                Console.WriteLine($"shard[{i}]: {Encoding.UTF8.GetString(shardBytes)}");

                // copy shard to shard matrix
                dataShards[i] = new byte [shardBytes.Length];
                Array.Copy(shardBytes, dataShards[i], shardBytes.Length);
            }

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

            
            //var transactionBytes = GetCBORBytesFromJSON(binaryStringCBOR.ToJSONString());

            //CBORObject transCBOR = CBORObject.DecodeFromBytes(transactionBytes);
           
            //Console.WriteLine(transCBOR.ToJSONString());
           
           
            // Extract the shards from the JSON string and put them in byte matrix (2D array of bytes).
            byte [][] shards = GetShardsFromJSON(binaryStringCBOR.ToJSONString());
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
            
            //return Ok("Hello from API Layer");
        }
    }
}
