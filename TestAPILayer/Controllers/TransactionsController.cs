using Microsoft.AspNetCore.Mvc;
using System.Text;
using PeterO.Cbor;
using System.Formats.Cbor;
using Newtonsoft.Json.Linq;

namespace TestAPILayer.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class TransactionsController : ControllerBase
    {             

        private async Task<string> GetCBORString(string str)
        {

            return str.Replace("{", " ").Replace("}", " ").Replace("(", " ").Replace(")", " ").Replace("[", " ").
                       Replace("]", " ").Replace('"', ' ').Replace(',', ' ').Replace("'", " ").Trim();

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
                Console.WriteLine(body);
            }

            byte[] bytes = new byte[body.Length];
            for (int i = 0; i < bytes.Length; i++)
            {
                bytes[i] = (byte)body.ElementAt(i);
            }
            //byte[] bytes = Encoding.ASCII.GetBytes(body);

            //CborReader cborReader = new CborReader(bytes);

            //byte [] bytes2 = cborReader.ReadByteString();

            using (MemoryStream ms = new MemoryStream(bytes))
            {

                // Read the CBOR object from the stream
                var cbor = CBORObject.Read(ms);
                // The rest of the example follows the one given above.
                Console.WriteLine(cbor.ToJSONString());

            }         

            //            /*
            //            int totalNShards = 21;
            //            int parityNShards = 10;
            //            int dataNShards = 11;

            //            byte[] bytes = Encoding.Default.GetBytes(value);



            //            bool [] shardsPresent = new bool [totalNShards];


            //            for (int i = 0; i < 11; i++)
            //            {
            //                shardsPresent[i] = true;
            //            }

            //            Console.WriteLine(bytes.Length);

            //            //// Replicate the other shards using Reeed-Solomom.
            //            //var reedSolomon = new ReedSolomon(shardsPresent.Length - parityNShards, parityNShards);
            //            //reedSolomon.decodeMissing(transactionShards, shardsPresent, 0, dataShardLength);

            //            //// Write the Reed-Solomon matrix of shards to a 1D array of bytes
            //            //let metadataBytes = new Uint8Array(transactionShards.length * dataShardLength);
            //            //let offset = 0;

            //            //for (let j = 0; j < transactionShards.length - parityNShards; j++)
            //            //{
            //            //    //Array.Copy(transactionShards[j], 0, metadataBytes, offset, dataShardLength);

            //            //    for (let i = 0; i < dataShardLength; i++)
            //            //    {
            //            //        metadataBytes[offset + i] = transactionShards[j][i];
            //            //    }

            //            //    offset += dataShardLength;
            //            //}
            //          */
            //        }


            //    }


            //    // Drain any remaining section body that hasn't been consumed and
            //    // read the headers for the next section.
            //    section = await reader.ReadNextSectionAsync();     
            //}

            //Console.WriteLine();// value);


            return Ok(body);
        }
    }
}
