using Microsoft.AspNetCore.Mvc;
using System.Text;
using PeterO.Cbor;
using TestAPILayer.ReedSolomon;

namespace TestAPILayer.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class TransactionsController : ControllerBase
    {

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

        byte[] StripPadding(byte[] paddedData)
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

        private async Task<byte[][]> GetShards(string str)
        {

            str = str.Replace('[', ' ').Replace(']', ' ').Replace('"', ' ').Replace('\'', ' ');

            string[] shards = str.Split(',');

            byte[][] dataShards = new byte[shards.Length][];
            for (int i = 0; i < shards.Length; i++)
            {
                shards[i] = ConvertStringToBase64(shards[i].Trim());
                //Console.WriteLine(shards[i].Length);
                //Console.WriteLine(shards[i].Trim());
                byte[] bytes = Convert.FromBase64String(shards[i]);
                //Console.WriteLine(Encoding.UTF8.GetString(bytes));
                dataShards[i] = new byte [bytes.Length];
                Array.Copy(bytes, dataShards[i], bytes.Length);
            }

            return dataShards;

        }
               
        [HttpPost]
        [Route("PostTransaction")]       
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult> PostTransaction()
        {
            
            Console.WriteLine("Entered PostTransaction");
            
            string body = "";
            using (var reader = new StreamReader(Request.Body))
            {
                body = await reader.ReadToEndAsync();                
                //Console.WriteLine(body);               
            }

            byte[] bytes = new byte[body.Length];
            for (int i = 0; i < bytes.Length; i++)
            {
                bytes[i] = (byte)body.ElementAt(i);
            }
            //byte[] bytes = Encoding.ASCII.GetBytes(body);           
            CBORObject cbor;
            using (MemoryStream ms = new MemoryStream(bytes))
            {

                // Read the CBOR object from the stream
                cbor = CBORObject.Read(ms);
                // The rest of the example follows the one given above.
                if (ms.Position != ms.Length)
                {
                    // The end of the stream wasn't reached yet.
                    Console.WriteLine("The end of the stream wasn't reached yet.");
                }
                else
                {
                    // The end of the stream was reached.
                    Console.WriteLine("The end of the stream was reached.");
                    //Console.WriteLine(cbor.ToJSONString());
                }
            }

            byte [][] shards = await GetShards(cbor.ToJSONString());

            int shardLength = shards[0].Length;


            int totalNShards = shards.Length;
            int nParityShards = totalNShards / 2;
            int nDataShards = totalNShards - nParityShards;

            Console.WriteLine($"totalNShards: {totalNShards}");
            Console.WriteLine($"parityNShards: {nParityShards}");
            Console.WriteLine($"dataNShards: {nDataShards}");

            bool[] shardsPresent = new bool[totalNShards];


            for (int i = 0; i < nDataShards; i++)
            {
                shardsPresent[i] = true;
            }

            Console.WriteLine(bytes.Length);

            // Replicate the other shards using Reeed-Solomom.
            var reedSolomon = new ReedSolomon.ReedSolomon(shardsPresent.Length - nParityShards, nParityShards);
            reedSolomon.DecodeMissing(shards, shardsPresent, 0, shardLength);

            // Write the Reed-Solomon matrix of shards to a 1D array of bytes
            byte[] buffer = new byte[shards.Length * shardLength];
            int offSet = 0;

            for (int j = 0; j < shards.Length - nParityShards; j++)
            {
                Array.Copy(shards[j], 0, buffer, offSet, shardLength);
                offSet += shardLength;
            }

            Console.WriteLine("Reassembled Output text:");
            string output = Encoding.ASCII.GetString(StripPadding(buffer));
            Console.WriteLine(output);
            Console.WriteLine();

            return Ok(output);
        }
    }
}
