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

            List<byte[]> encrypts = KeyStore.Inst.GetENCRYPTS(src);

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
            
            List<byte[]> signs = KeyStore.Inst.GetSIGNS(src); 

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
            Console.WriteLine(CryptoUtils.ByteArrayToStringDebug(result.Content.ReadAsByteArrayAsync().Result));

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

            // servers store _KEYS (SIGNS + ENCRYPTS)           
            byte [] inviteID = CryptoUtils.CBORBinaryStringToBytes(transactionObj.inviteID);
            KeyStore.Inst.StoreENCRYPTS(inviteID, transactionObj.ENCRYPTS);
            KeyStore.Inst.StoreSIGNS(inviteID, transactionObj.SIGNS);                

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
            KeyStore.Inst.StoreDS_PUB(src, CryptoUtils.CBORBinaryStringToBytes(transactionObj.DS_PUB));
            KeyStore.Inst.StoreDE_PUB(src, CryptoUtils.CBORBinaryStringToBytes(transactionObj.DE_PUB));
            KeyStore.Inst.StoreNONCE(src, CryptoUtils.CBORBinaryStringToBytes(transactionObj.NONCE));
            KeyStore.Inst.StoreWTOKEN(src, CryptoUtils.CBORBinaryStringToBytes(transactionObj.wTOKEN));

            // servers create SE[] = create ECDH key pair          
            List<byte[]> SE_PUB = new List<byte[]>();
            List<byte[]> SE_PRIV = new List<byte[]>();
            for (int i = 0; i < CryptoUtils.NUM_SERVERS; i++)
            {
                var key = CryptoUtils.CreateECDH();
                SE_PUB.Add(key.PublicKey);

                SE_PRIV.Add(key.PrivateKey);
            }
            // servers store SE.PRIV[]
            KeyStore.Inst.StoreSE_PRIV(src, SE_PRIV);

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

            // servers unwrap + store _KEYS to memory
            List<byte[]> ENCRYPTS = new List<byte[]>();
            List<byte[]> SIGNS = new List<byte[]>();
            byte[] NONCE = KeyStore.Inst.GetNONCE(src);
            for (int i = 0; i < CryptoUtils.NUM_KEYS; i++)
            {
                byte[] wENCRYPT = CryptoUtils.CBORBinaryStringToBytes(transactionObj.wENCRYPTS[i]);
                byte[] unwrapENCRYPT = new byte[32];// CryptoUtils.Unwrap(wENCRYPT, NONCE);
                ENCRYPTS.Add(unwrapENCRYPT);

                byte[] wSIGN = CryptoUtils.CBORBinaryStringToBytes(transactionObj.wSIGNS[i]);
                byte[] unwrapSIGN = new byte[32];//CryptoUtils.Unwrap(wSIGN, NONCE);
                SIGNS.Add(unwrapSIGN);
            }
            KeyStore.Inst.StoreENCRYPTS(src, ENCRYPTS);
            KeyStore.Inst.StoreENCRYPTS(src, SIGNS);

            // servers store DS.PUB + DE.PUB + NONCE
            KeyStore.Inst.StoreDS_PUB(src, CryptoUtils.CBORBinaryStringToBytes(transactionObj.DS_PUB));
            KeyStore.Inst.StoreDE_PUB(src, CryptoUtils.CBORBinaryStringToBytes(transactionObj.DE_PUB));
            KeyStore.Inst.StoreNONCE(src, CryptoUtils.CBORBinaryStringToBytes(transactionObj.NONCE));

            // servers create SE[] = create ECDH key pair
            List<byte[]> SE_PUB = new List<byte[]>();
            List<byte[]> SE_PRIV = new List<byte[]>();
            for (int i = 0; i < CryptoUtils.NUM_SERVERS; i++)
            {
                //ECDiffieHellmanCng key = CryptoUtils.CreateECDH();
                SE_PUB.Add(new byte[32]);// key.PublicKey.ToByteArray();

                SE_PRIV.Add(new byte[32]);// key.ExportECPrivateKey();
            }
            //servers store SE.PRIV[]
            KeyStore.Inst.StoreSE_PRIV(src, SE_PRIV);

            // response is wTOKEN, SE.PUB[]
            var cbor = CBORObject.NewMap()
                .Add("wTOKEN", KeyStore.Inst.GetWTOKEN(src))
                .Add("SE_PUB", CBORObject.NewArray().Add(SE_PUB));

            return Ok(cbor.EncodeToBytes());
        }
    }
}
