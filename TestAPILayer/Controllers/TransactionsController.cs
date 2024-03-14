using Microsoft.AspNetCore.Mvc;
using PeterO.Cbor;



namespace TestAPILayer.Controllers
{


    [Route("api/[controller]")]
    [ApiController]
    public class TransactionsController : ControllerBase
    {               

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

        public static byte[] RebuildDataUsingReeedSolomon(byte[][] shards)
        {
            // Get shard length (all shards are of equal length).
            int shardLength = shards[0].Length;

            int nTotalShards = shards.Length;
            int nParityShards = nTotalShards / 2;
            int nDataShards = nTotalShards - nParityShards;                      

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
            return StripPadding(rebuiltDataBytes);
        }

        // Extracts the shards from the JSON string an puts the to a 2D byte array (matrix)
        // needed for rebuilding the data using Reed-Solomon.
        private static byte[][] GetShardsFromCBOR(byte[] shardsCBORBytes, byte[] hmacResultBytes)
        {           

            CBORObject shardsCBOR = CBORObject.DecodeFromBytes(shardsCBORBytes);
         
            //Console.WriteLine($"Shards CBOR to JSON: {shardsCBOR.ToJSONString()}"); 
                     
            // allocate memory for the data shards byte matrix
            // Last element in the string array is not a shard but the SRC array 
            int numShards = shardsCBOR.Values.Count - 1;
            int numShardsPerServer = numShards / CryptoUtils.NUM_SERVERS;

            List<byte[]> encrypts = new List<byte[]>();
            List<byte[]> signs = new List<byte[]>();
            byte[] src = new byte[8];
            string secretString = "secret";
           
            CryptoUtils.GenerateKeys(ref encrypts, ref signs, ref src, secretString, CryptoUtils.NUM_SERVERS);

            bool verified = CryptoUtils.HashIsValid(signs[0], shardsCBORBytes, hmacResultBytes);

            Console.WriteLine($"CBOR Shard Data Verified: {verified}");

            if (verified)
            {
                byte[][] dataShards = new byte[numShards][];
                for (int i = 0; i < numShards; i++)
                {
                    // we start encrypts[1] we don't use encrypts[0]
                    // we may have more than on shard per server 
                    int encryptsIndex = (i / numShardsPerServer) + 1; 

                    byte[] encryptedShard = shardsCBOR.Values.ElementAt(i).GetByteString();

                    // decrypt string array                
                    byte[] shardBytes = CryptoUtils.Decrypt(encryptedShard, encrypts[encryptsIndex], src);

                    //Console.WriteLine($"Encrypts Index: {encryptsIndex}");

                    // Write to console out for debug
                    //Console.WriteLine($"shard[{i}]: {CryptoUtils.ByteArrayToString(shardBytes)}");

                    // copy shard to shard matrix
                    dataShards[i] = new byte[shardBytes.Length];
                    Array.Copy(shardBytes, dataShards[i], shardBytes.Length);
                }

                //Console.WriteLine();

                return dataShards;
            }
            else
            {
                return null;
            }
        }                    
       
        // Transaction endpoint
        [HttpPost]
        [Route("PostTransaction")]       
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult> PostTransaction()
        {
            byte[] requestBytes;
            using (var ms = new MemoryStream())
            {
                await Request.Body.CopyToAsync(ms);
                requestBytes = ms.ToArray();  
            }           
           
            // Decode request's CBOR bytes  
            CBORObject requestCBOR = CBORObject.DecodeFromBytes(requestBytes);           
 
            byte[] transanctionShardsCBORBytes = requestCBOR.Values.ElementAt(0).GetByteString();
            byte[] hmacResultBytes = requestCBOR.Values.ElementAt(1).GetByteString();

            // Extract the shards from shards CBOR and put them in byte matrix (2D array of bytes).
            byte [][] transactionShards = GetShardsFromCBOR(transanctionShardsCBORBytes, hmacResultBytes);

            if (transactionShards == null)
            {
                return Ok("Received data not verified");
            }
         
            byte[] cborDataBytes = RebuildDataUsingReeedSolomon(transactionShards);
            //Console.WriteLine($"CBOR Data bytes: {Encoding.UTF8.GetString(cborDataBytes)}");

            CBORObject rebuiltDataCBOR = CBORObject.DecodeFromBytes(cborDataBytes);
            
            string rebuiltDataString = rebuiltDataCBOR.ToJSONString();
                                 
            Console.WriteLine($"Rebuilt Data: {rebuiltDataString}");
            Console.WriteLine();

            return Ok(rebuiltDataCBOR.ToJSONString());  
           
        }
    }
}
