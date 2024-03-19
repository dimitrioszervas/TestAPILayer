using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using PeterO.Cbor;
using System.Security.Cryptography;
using TestAPILayer.ReedSolomon;
using TestAPILayer.Requests;
using TestAPILayer.Responses;



namespace TestAPILayer.Controllers
{

    [Route("api/[controller]")]
    [ApiController]
    public class TransactionsController : ControllerBase
    {  

        // Extracts the shards from the JSON string an puts the to a 2D byte array (matrix)
        // needed for rebuilding the data using Reed-Solomon.
        private static byte[][] GetShardsFromCBOR(byte[] shardsCBORBytes, List<byte[]> encrypts, byte[] src)
        {      
            CBORObject shardsCBOR = CBORObject.DecodeFromBytes(shardsCBORBytes);         
          
            // allocate memory for the data shards byte matrix
            // Last element in the string array is not a shard but the SRC array 
            int numShards = shardsCBOR.Values.Count - 1;
            int numShardsPerServer = numShards / CryptoUtils.NUM_SERVERS;   
                       
            byte[][] dataShards = new byte[numShards][];
            for (int i = 0; i < numShards; i++)
            {
                // we start OWN_ENCRYPTS[1] we don't use OWN_ENCRYPTS[0]
                // we may have more than on shard per server 
                int encryptsIndex = (i / numShardsPerServer) + 1; 

                byte[] encryptedShard = shardsCBOR[i].GetByteString();

                // decrypt string array                
                byte[] shardBytes = CryptoUtils.Decrypt(encryptedShard, encrypts[encryptsIndex], src);

                //Console.WriteLine($"Encrypts Index: {encryptsIndex}");

                // copy shard to shard matrix
                dataShards[i] = new byte[shardBytes.Length];
                Array.Copy(shardBytes, dataShards[i], shardBytes.Length);
            }                          

            return dataShards;          
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
 
            byte[] transanctionShardsCBORBytes = requestCBOR[0].GetByteString();
            byte[] hmacResultBytes = requestCBOR[1].GetByteString();

            List<byte[]> encrypts = new List<byte[]>();
            List<byte[]> signs = new List<byte[]>();
            byte[] src = new byte[8];
            string ownerCode = CryptoUtils.OWNER_CODE;

            CryptoUtils.GenerateKeys(ref encrypts, ref signs, ref src, ownerCode, CryptoUtils.NUM_SERVERS);

            bool verified = CryptoUtils.HashIsValid(signs[0], transanctionShardsCBORBytes, hmacResultBytes);

            Console.WriteLine($"CBOR Shard Data Verified: {verified}");

            // Extract the shards from shards CBOR and put them in byte matrix (2D array of bytes).
            byte [][] transactionShards = GetShardsFromCBOR(transanctionShardsCBORBytes, encrypts, src);

            if (transactionShards == null)
            {
                return Ok("Received data not verified");
            }
         
            byte[] cborTransactionBytes = ReedSolomonUtils.RebuildDataUsingReeedSolomon(transactionShards);
            

            CBORObject rebuiltTransactionCBOR = CBORObject.DecodeFromBytes(cborTransactionBytes);

            string rebuiltDataJSON = rebuiltTransactionCBOR.ToJSONString();
            Console.WriteLine($"Rebuilt Data: {rebuiltDataJSON} ");
            
            UnsignedTransaction<CreateFolderRequest> transactionObj =
                JsonConvert.DeserializeObject<UnsignedTransaction<CreateFolderRequest>>(rebuiltDataJSON);
         

            string threshold = CryptoUtils.ConvertStringToBase64(transactionObj.REQ[0].encKEY);           

            byte[] thresholdCBORBytes = Convert.FromBase64String(threshold);
                       
            byte[][] thresholdShards = GetShardsFromCBOR(thresholdCBORBytes, encrypts, src);
            byte [] rebuiltEncKey = ReedSolomonUtils.RebuildDataUsingReeedSolomon(thresholdShards);
            string stringEncKey = CryptoUtils.ByteArrayToString(rebuiltEncKey);
         
            //Console.WriteLine($"Rebuilt encKEY({rebuiltEncKey.Length}): {stringEncKey} ");
            //Console.WriteLine();            
           
            return Ok(rebuiltDataJSON); 
           
        }

        // InviteUser  endpoint
        [HttpPost]
        [Route("InviteUser")]       
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult> InviteUser()
        {
            byte[] requestBytes;
            using (var ms = new MemoryStream())
            {
                await Request.Body.CopyToAsync(ms);
                requestBytes = ms.ToArray();  
            }           
           
            // Decode request's CBOR bytes  
            CBORObject requestCBOR = CBORObject.DecodeFromBytes(requestBytes);           
 
            byte[] transanctionShardsCBORBytes = requestCBOR[0].GetByteString();
            byte[] hmacResultBytes = requestCBOR[1].GetByteString();

            List<byte[]> encrypts = new List<byte[]>();
            List<byte[]> signs = new List<byte[]>();
            byte[] src = new byte[8];
            string ownerCode = CryptoUtils.OWNER_CODE;

            CryptoUtils.GenerateKeys(ref encrypts, ref signs, ref src, ownerCode, CryptoUtils.NUM_SERVERS);

            bool verified = CryptoUtils.HashIsValid(signs[0], transanctionShardsCBORBytes, hmacResultBytes);

            Console.WriteLine($"CBOR Shard Data Verified: {verified}");

            // Extract the shards from shards CBOR and put them in byte matrix (2D array of bytes).
            byte [][] transactionShards = GetShardsFromCBOR(transanctionShardsCBORBytes, encrypts, src);
         
            byte[] cborTransactionBytes = ReedSolomonUtils.RebuildDataUsingReeedSolomon(transactionShards);
            

            CBORObject rebuiltTransactionCBOR = CBORObject.DecodeFromBytes(cborTransactionBytes);

            string rebuiltDataJSON = rebuiltTransactionCBOR.ToJSONString();
            Console.WriteLine($"Rebuilt Data: {rebuiltDataJSON} ");            
          
            UnsignedTransaction<InviteRequest> transactionObj =
               JsonConvert.DeserializeObject<UnsignedTransaction<InviteRequest>>(rebuiltDataJSON);

            //byte[] thresholdCBORBytes = CryptoUtils.CBORBinaryStringToBytes(transactionObj.REQ[0].encKEY);
            //byte[][] thresholdShards = GetShardsFromCBOR(thresholdCBORBytes, encrypts, src);
            //byte [] rebuiltEncKey = ReedSolomonUtils.RebuildDataUsingReeedSolomon(thresholdShards);

            //Store received OWN_ENCRYPTS & OWN_SIGNS to memory
            MemStorage.Clear();
            for (int i = 0; i <= CryptoUtils.NUM_SERVERS; i++) {
                MemStorage.ENCRYPTS.Add(CryptoUtils.CBORBinaryStringToBytes(transactionObj.REQ[0].OWN_ENCRYPTS[i]));
                MemStorage.SIGNS.Add(CryptoUtils.CBORBinaryStringToBytes(transactionObj.REQ[0].OWN_SIGNS[i]));
            }

            InviteResponse response = new InviteResponse();

            response.OWN_ENCRYPTS.AddRange(MemStorage.ENCRYPTS);
            response.OWN_SIGNS.AddRange(MemStorage.SIGNS);

            var cbor = CBORObject.NewMap()
                .Add("OWN_ENCRYPTS", CBORObject.NewArray().Add(response.OWN_ENCRYPTS))
                .Add("OWN_SIGNS", CBORObject.NewArray().Add(response.OWN_SIGNS));           

            Console.WriteLine(cbor.ToJSONString());

            return Ok(cbor.ToJSONString()); 
           
        }

        // Register endpoint
        [HttpPost]
        [Route("Register")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult> Register()
        {
            byte[] requestBytes;
            using (var ms = new MemoryStream())
            {
                await Request.Body.CopyToAsync(ms);
                requestBytes = ms.ToArray();
            }

            // Decode request's CBOR bytes  
            CBORObject requestCBOR = CBORObject.DecodeFromBytes(requestBytes);

            byte[] transanctionShardsCBORBytes = requestCBOR[0].GetByteString();
            byte[] hmacResultBytes = requestCBOR[1].GetByteString();

            List<byte[]> encrypts = new List<byte[]>();
            List<byte[]> signs = new List<byte[]>();
            byte[] src = new byte[8];
            string ownerCode = CryptoUtils.OWNER_CODE;

            CryptoUtils.GenerateKeys(ref encrypts, ref signs, ref src, ownerCode, CryptoUtils.NUM_SERVERS);

            bool verified = CryptoUtils.HashIsValid(signs[0], transanctionShardsCBORBytes, hmacResultBytes);

            Console.WriteLine($"CBOR Shard Data Verified: {verified}");

            // Extract the shards from shards CBOR and put them in byte matrix (2D array of bytes).
            byte[][] transactionShards = GetShardsFromCBOR(transanctionShardsCBORBytes, encrypts, src);         

            byte[] cborTransactionBytes = ReedSolomonUtils.RebuildDataUsingReeedSolomon(transactionShards);

            CBORObject rebuiltTransactionCBOR = CBORObject.DecodeFromBytes(cborTransactionBytes);

            string rebuiltDataJSON = rebuiltTransactionCBOR.ToJSONString();
            Console.WriteLine($"Rebuilt Data: {rebuiltDataJSON} ");

            UnsignedTransaction<RegisterRequest> transactionObj =
               JsonConvert.DeserializeObject<UnsignedTransaction<RegisterRequest>>(rebuiltDataJSON);

            // servers store DS.PUB + DE.PUB + NONCE
            MemStorage.DS_PUB = CryptoUtils.CBORBinaryStringToBytes(transactionObj.REQ[0].DS_PUB);
            MemStorage.DE_PUB = CryptoUtils.CBORBinaryStringToBytes(transactionObj.REQ[0].DE_PUB);
            MemStorage.NONCE = CryptoUtils.CBORBinaryStringToBytes(transactionObj.REQ[0].NONCE);
            byte[] wTOKEN = CryptoUtils.CBORBinaryStringToBytes(transactionObj.REQ[0].wTOKEN);

            List<byte[]> SE_PUB = new List<byte[]>();
            MemStorage.SE_PRIV.Clear();
            for (int i = 0; i < CryptoUtils.NUM_SERVERS; i++)
            {
                ECDiffieHellmanCng key = CryptoUtils.CreateECDH();
                SE_PUB.Add(key.PublicKey.ToByteArray());
                MemStorage.SE_PRIV.Add(key.ExportECPrivateKey());
            }

            var cbor = CBORObject.NewMap().Add("SE_PUB", CBORObject.NewArray().Add(SE_PUB));

            Console.WriteLine(cbor.ToJSONString());

            return Ok(cbor.ToJSONString());

        }

        // Login endpoint
        [HttpPost]
        [Route("Login")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult> Login()
        {
            byte[] requestBytes;
            using (var ms = new MemoryStream())
            {
                await Request.Body.CopyToAsync(ms);
                requestBytes = ms.ToArray();
            }

            // Decode request's CBOR bytes  
            CBORObject requestCBOR = CBORObject.DecodeFromBytes(requestBytes);

            byte[] transanctionShardsCBORBytes = requestCBOR[0].GetByteString();
            byte[] hmacResultBytes = requestCBOR[1].GetByteString();

            List<byte[]> encrypts = new List<byte[]>();
            List<byte[]> signs = new List<byte[]>();
            byte[] src = new byte[8];
            string ownerCode = CryptoUtils.OWNER_CODE;

            CryptoUtils.GenerateKeys(ref encrypts, ref signs, ref src, ownerCode, CryptoUtils.NUM_SERVERS);

            bool verified = CryptoUtils.HashIsValid(signs[0], transanctionShardsCBORBytes, hmacResultBytes);

            Console.WriteLine($"CBOR Shard Data Verified: {verified}");

            // Extract the shards from shards CBOR and put them in byte matrix (2D array of bytes).
            byte[][] transactionShards = GetShardsFromCBOR(transanctionShardsCBORBytes, encrypts, src);

            byte[] cborTransactionBytes = ReedSolomonUtils.RebuildDataUsingReeedSolomon(transactionShards);

            CBORObject rebuiltTransactionCBOR = CBORObject.DecodeFromBytes(cborTransactionBytes);

            string rebuiltDataJSON = rebuiltTransactionCBOR.ToJSONString();
            Console.WriteLine($"Rebuilt Data: {rebuiltDataJSON} ");

            UnsignedTransaction<LoginRequest> transactionObj =
               JsonConvert.DeserializeObject<UnsignedTransaction<LoginRequest>>(rebuiltDataJSON);

            MemStorage.Clear();
            // servers unwrap + store KEYS to memory
            for (int i = 0; i <= CryptoUtils.NUM_SERVERS; i++)
            {
                MemStorage.ENCRYPTS.Add(CryptoUtils.CBORBinaryStringToBytes(transactionObj.REQ[0].WENCRYPTS[i]));
                MemStorage.SIGNS.Add(CryptoUtils.CBORBinaryStringToBytes(transactionObj.REQ[0].WSIGNS[i]));
            }

            // servers store DS.PUB + DE.PUB + NONCE
            MemStorage.DS_PUB = CryptoUtils.CBORBinaryStringToBytes(transactionObj.REQ[0].DS_PUB);
            MemStorage.DE_PUB = CryptoUtils.CBORBinaryStringToBytes(transactionObj.REQ[0].DE_PUB);
            MemStorage.NONCE = CryptoUtils.CBORBinaryStringToBytes(transactionObj.REQ[0].NONCE);

            List<byte[]> SE_PUB = new List<byte[]>();
            for (int i = 0; i < CryptoUtils.NUM_SERVERS; i++)
            {
                ECDiffieHellmanCng key = CryptoUtils.CreateECDH();
                SE_PUB.Add(key.PublicKey.ToByteArray());
                MemStorage.SE_PRIV.Add(key.ExportECPrivateKey());
            }

            var cbor = CBORObject.NewMap().Add("SE_PUB", CBORObject.NewArray().Add(SE_PUB));

            Console.WriteLine(cbor.ToJSONString());

            return Ok(cbor.ToJSONString());           

        }
    }
}
