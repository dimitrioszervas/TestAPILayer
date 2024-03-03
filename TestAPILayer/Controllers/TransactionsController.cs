using Microsoft.AspNetCore.Mvc;
using System.Text;
using PeterO.Cbor;
using Newtonsoft.Json;
using System.Transactions;

namespace TestAPILayer.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class TransactionsController : ControllerBase
    {
        sealed class ShardsJSON
        {
            public List<string> Shards { set; get; } = new List<string>();
        }

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

        private static byte[][] GetShardsFromJSON(string jsonArrayString)
        {        

            var cborShards = JsonConvert.DeserializeObject<ShardsJSON>("{'shards':" + jsonArrayString + "}");

            byte[][] dataShards = new byte[cborShards.Shards.Count][];
            for (int i = 0; i < cborShards.Shards.Count; i++)
            {
                string shardString = ConvertStringToBase64(cborShards.Shards[i]);               
                byte[] shardBytes = Convert.FromBase64String(shardString);
                Console.WriteLine($"shard[{i}]: {Encoding.UTF8.GetString(shardBytes)}");
                dataShards[i] = new byte [shardBytes.Length];
                Array.Copy(shardBytes, dataShards[i], shardBytes.Length);
            }

            return dataShards;

        }

        private static byte [] BinaryStringToBytes(string binaryString)
        {
            byte[] binaryStringBytes = new byte[binaryString.Length];
            for (int i = 0; i < binaryStringBytes.Length; i++)
            {
                binaryStringBytes[i] = (byte)binaryString.ElementAt(i);
            }

            return binaryStringBytes;
        }
               
        [HttpPost]
        [Route("PostTransaction")]       
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult> PostTransaction()
        {          
            
            string binaryString = "";
            using (var reader = new StreamReader(Request.Body))
            {
                binaryString = await reader.ReadToEndAsync();
                Console.WriteLine("----------------------------------------------------------------------");
                Console.WriteLine($"Binary String Received: {binaryString}");               
            }

            byte[] binaryStringBytes = BinaryStringToBytes(binaryString);
            CBORObject binaryStringCBOR = CBORObject.DecodeFromBytes(binaryStringBytes);
           

            Console.WriteLine("----------------------------------------------------------------------");
            Console.WriteLine($"Binary String CBOR to JSON: {binaryStringCBOR.ToJSONString()}");
            Console.WriteLine("----------------------------------------------------------------------");
            byte [][] shards = GetShardsFromJSON(binaryStringCBOR.ToJSONString());
            Console.WriteLine("----------------------------------------------------------------------");

            int shardLength = shards[0].Length;

            int nTotalShards = shards.Length;
            int nParityShards = nTotalShards / 2;
            int nDataShards = nTotalShards - nParityShards;

            Console.WriteLine($"totalNShards: {nTotalShards}");
            Console.WriteLine($"dataNShards: {nDataShards}");
            Console.WriteLine($"parityNShards: {nParityShards}");
            Console.WriteLine("----------------------------------------------------------------------");

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

            CBORObject rebuiltDataCBOR = CBORObject.DecodeFromBytes(StripPadding(rebuiltDataBytes));
            string rebuiltDataString = rebuiltDataCBOR.ToJSONString();            
                     
            Console.WriteLine($"Rebuilt Data: {rebuiltDataString}");
            Console.WriteLine();

            return Ok(rebuiltDataCBOR.ToJSONString());
        }
    }
}
