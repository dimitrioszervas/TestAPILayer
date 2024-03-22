using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using PeterO.Cbor;
using System.Net;
using System.Net.Http.Headers;
using TestAPILayer.ReedSolomon;
using TestAPILayer.Requests;



namespace TestAPILayer.Controllers
{

    [Route("api/[controller]")]
    [ApiController]
    public class TransactionsController : ControllerBase
    {  

        // Extracts the shards from the JSON string an puts the to a 2D byte array (matrix)
        // needed for rebuilding the data using Reed-Solomon.
        private static byte[][] GetShardsFromCBOR(byte[] shardsCBORBytes, ref byte[] src)
        {      
            CBORObject shardsCBOR = CBORObject.DecodeFromBytes(shardsCBORBytes);         
          
            // allocate memory for the data shards byte matrix
            // Last element in the string array is not a shard but the SRC array 
            int numShards = shardsCBOR.Values.Count - 1;
            int numShardsPerServer = numShards / CryptoUtils.NUM_SERVERS;

            src = shardsCBOR[shardsCBOR.Values.Count - 1].GetByteString();          

            byte[][] encrypts = MemStorage.KEYS[CryptoUtils.ByteArrayToString(src)].ENCRYPTS;

            byte[][] dataShards = new byte[numShards][];
            for (int i = 0; i < numShards; i++)
            {
                // we start ENCRYPTS[1] we don't use ENCRYPTS[0]
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
       
        public static string GetTransactionFromCBOR(byte[] requestBytes, ref byte[] src)
        {
            // Decode request's CBOR bytes  
            CBORObject requestCBOR = CBORObject.DecodeFromBytes(requestBytes);

            byte[] transanctionShardsCBORBytes = requestCBOR[0].GetByteString();
            byte[] hmacResultBytes = requestCBOR[1].GetByteString();
           
            byte[][] transactionShards = GetShardsFromCBOR(transanctionShardsCBORBytes, ref src);
            
            byte[][] signs = MemStorage.KEYS[CryptoUtils.ByteArrayToString(src)].SIGNS; 

            bool verified = CryptoUtils.HashIsValid(signs[0], transanctionShardsCBORBytes, hmacResultBytes);

            Console.WriteLine($"CBOR Shard Data Verified: {verified}");

            // Extract the shards from shards CBOR and put them in byte matrix (2D array of bytes).

            byte[] cborTransactionBytes = ReedSolomonUtils.RebuildDataUsingReeedSolomon(transactionShards);

            CBORObject rebuiltTransactionCBOR = CBORObject.DecodeFromBytes(cborTransactionBytes);

            string rebuiltDataJSON = rebuiltTransactionCBOR.ToJSONString();

            return rebuiltDataJSON;
        }

        private static HttpResponseMessage ReturnBytes(byte[] bytes)
        {
            HttpResponseMessage result = new HttpResponseMessage(HttpStatusCode.OK);
            result.Content = new ByteArrayContent(bytes);
            result.Content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");

            Console.WriteLine();
            Console.WriteLine(bytes.Length);
            Console.WriteLine(CryptoUtils.ByteArrayToString(result.Content.ReadAsByteArrayAsync().Result));

            return result;
        }

        // Invite endpoint
        [HttpPost]
        [Route("Invite")]       
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult> Invite()
        //public async Task<HttpResponseMessage> Invite()
        {
            byte[] requestBytes;
            using (var ms = new MemoryStream())
            {
                await Request.Body.CopyToAsync(ms);
                requestBytes = ms.ToArray();  
            }

            // Decode request's CBOR bytes   
            byte[] src = new byte[CryptoUtils.SRC_SIZE];
            string rebuiltDataJSON = GetTransactionFromCBOR(requestBytes, ref src);

            Console.WriteLine("Invite:");
            Console.WriteLine($"Rebuilt Data: {rebuiltDataJSON} ");
            Console.WriteLine();

            InviteRequest transactionObj =
               JsonConvert.DeserializeObject<InviteRequest>(rebuiltDataJSON);

            //byte[] thresholdCBORBytes = CryptoUtils.CBORBinaryStringToBytes(transactionObj.REQ[0].encKEY);
            //byte[][] thresholdShards = GetShardsFromCBOR(thresholdCBORBytes, encrypts, src);
            //byte [] rebuiltEncKey = ReedSolomonUtils.RebuildDataUsingReeedSolomon(thresholdShards);

            // servers store KEYS (SIGNS + ENCRYPTS)           
            string inviteID = CryptoUtils.ByteArrayToString(CryptoUtils.CBORBinaryStringToBytes(transactionObj.inviteID));
            MemStorage.KEYS.TryAdd(inviteID, new MemStorage.Keys());
            for (int i = 0; i <= CryptoUtils.NUM_SERVERS; i++) {
                MemStorage.KEYS[inviteID].ENCRYPTS[i] = CryptoUtils.CBORBinaryStringToBytes(transactionObj.ENCRYPTS[i]);
                MemStorage.KEYS[inviteID].SIGNS[i] = CryptoUtils.CBORBinaryStringToBytes(transactionObj.SIGNS[i]);
            }           

            // response is OK using OWN_KEYS    
            var cbor = CBORObject.NewMap().Add("INVITE", "SUCCESS");

            //return Ok(cbor.ToJSONString());
            //return ReturnBytes(cbor.EncodeToBytes());
            return Ok(cbor.EncodeToBytes());
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
            byte[] src = new byte[CryptoUtils.SRC_SIZE];
            string rebuiltDataJSON = GetTransactionFromCBOR(requestBytes,ref src);
            Console.WriteLine("Register:");
            Console.WriteLine($"Rebuilt Data: {rebuiltDataJSON} ");
            Console.WriteLine();

            RegisterRequest transactionObj =
               JsonConvert.DeserializeObject<RegisterRequest>(rebuiltDataJSON);

            // servers store DS.PUB + DE.PUB + NONCE
            MemStorage.DS_PUB = CryptoUtils.CBORBinaryStringToBytes(transactionObj.DS_PUB);
            MemStorage.DE_PUB = CryptoUtils.CBORBinaryStringToBytes(transactionObj.DE_PUB);
            MemStorage.NONCE = CryptoUtils.CBORBinaryStringToBytes(transactionObj.NONCE);
            MemStorage.wTOKEN = CryptoUtils.CBORBinaryStringToBytes(transactionObj.wTOKEN);

            // servers create SE[] = create ECDH key pair          
            byte[][] SE_PUB = new byte[CryptoUtils.NUM_SERVERS][];           
            for (int i = 0; i < CryptoUtils.NUM_SERVERS; i++)
            {
                var key = CryptoUtils.CreateECDH();
                SE_PUB[i] = key.PublicKey;
                
                // servers store SE.PRIV[]
                MemStorage.SE_PRIV[i] = key.PrivateKey;
            }

            // response is SE.PUB[] 
            var cbor = CBORObject.NewMap().Add("SE_PUB", CBORObject.NewArray().Add(SE_PUB));

            return Ok(cbor.EncodeToBytes());
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
            byte[] src = new byte[CryptoUtils.SRC_SIZE]; 
            string rebuiltDataJSON = GetTransactionFromCBOR(requestBytes, ref src);
            Console.WriteLine("Login");
            Console.WriteLine($"Rebuilt Data: {rebuiltDataJSON} ");
            Console.WriteLine();

            LoginRequest transactionObj =
               JsonConvert.DeserializeObject<LoginRequest>(rebuiltDataJSON);

            // servers unwrap + store KEYS to memory
             
            for (int i = 0; i <= CryptoUtils.NUM_SERVERS; i++)
            {
                byte[] wENCRYPTS = CryptoUtils.CBORBinaryStringToBytes(transactionObj.wENCRYPTS[i]);
                MemStorage.KEYS[CryptoUtils.ByteArrayToString(src)].ENCRYPTS[i] = wENCRYPTS;// CryptoUtils.Unwrap(wENCRYPTS, MemStorage.NONCE);

                byte[] wSIGNS = CryptoUtils.CBORBinaryStringToBytes(transactionObj.wSIGNS[i]);
                MemStorage.KEYS[CryptoUtils.ByteArrayToString(src)].SIGNS[i] = wSIGNS;// CryptoUtils.Unwrap(wSIGNS, MemStorage.NONCE);
            }

            // servers store DS.PUB + DE.PUB + NONCE
            MemStorage.DS_PUB = CryptoUtils.CBORBinaryStringToBytes(transactionObj.DS_PUB);
            MemStorage.DE_PUB = CryptoUtils.CBORBinaryStringToBytes(transactionObj.DE_PUB);
            MemStorage.NONCE = CryptoUtils.CBORBinaryStringToBytes(transactionObj.NONCE);

            // servers create SE[] = create ECDH key pair
            byte[][] SE_PUB = new byte[CryptoUtils.NUM_SERVERS][];
            for (int i = 0; i < CryptoUtils.NUM_SERVERS; i++)
            {
                //ECDiffieHellmanCng key = CryptoUtils.CreateECDH();
                SE_PUB[i] = new byte[32];// key.PublicKey.ToByteArray();

                //servers store SE.PRIV[]
                MemStorage.SE_PRIV[i] = new byte[32];// key.ExportECPrivateKey();
            }

            // response is wTOKEN, SE.PUB[]
            var cbor = CBORObject.NewMap()
                .Add("wTOKEN", MemStorage.wTOKEN)
                .Add("SE_PUB", CBORObject.NewArray().Add(SE_PUB));

            return Ok(cbor.EncodeToBytes());
        }
    }
}
