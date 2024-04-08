using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using Org.BouncyCastle.Asn1.Ocsp;
using PeterO.Cbor;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using TestAPILayer.Contracts;
using TestAPILayer.ReedSolomon;
using TestAPILayer.Requests;

namespace TestAPILayer.Controllers
{

    [Route("api/[controller]")]
    [ApiController]
    public class TransactionsController : ControllerBase
    {
        private readonly ILogger<TransactionsController> _logger;
        private readonly IClientService _clientService;

        public TransactionsController(ILogger<TransactionsController> logger, IClientService clientService)
        {
            _logger = logger;
            _clientService = clientService;
        }

        // Extracts the shards from the JSON string an puts the to a 2D byte array (matrix)
        // needed for rebuilding the data using Reed-Solomon.
        private static byte[][] GetShardsFromCBOR(byte[] shardsCBORBytes, ref byte[] src)
        {      
            CBORObject shardsCBOR = CBORObject.DecodeFromBytes(shardsCBORBytes);         
          
            // allocate memory for the data shards byte matrix
            // Last element in the string array is not a shard but the loginID array 
            int numShards = shardsCBOR.Values.Count - 1;
          
            src = shardsCBOR[shardsCBOR.Values.Count - 1].GetByteString();           

            byte[][] shards = new byte[numShards][];
            for (int i = 0; i < numShards; i++)
            {  
                byte[] encryptedShard = shardsCBOR[i].GetByteString();
                             
                // copy shard to shard matrix
                shards[i] = new byte[encryptedShard.Length];
                Array.Copy(encryptedShard, shards[i], encryptedShard.Length);
            }                          

            return shards;          
        }

        private static byte[][] GetShardsFromCBOR(byte[] shardsCBORBytes, ref byte[] src, bool useLogins)
        {
            CBORObject shardsCBOR = CBORObject.DecodeFromBytes(shardsCBORBytes);

            // allocate memory for the data shards byte matrix
            // Last element in the string array is not a shard but the loginID array 
            int numShards = shardsCBOR.Values.Count - 1;
            int numShardsPerServer = numShards / Servers.NUM_SERVERS;

            src = shardsCBOR[shardsCBOR.Values.Count - 1].GetByteString();

            List<byte[]> encrypts = !useLogins ? KeyStore.Inst.GetENCRYPTS(src) : KeyStore.Inst.GetLOGINS(src);

            byte[][] shards = new byte[numShards][];
            for (int i = 0; i < numShards; i++)
            {
                // we may have more than on shard per server 
                int keyIndex = (i / numShardsPerServer) + 1;

                byte[] encryptedShard = shardsCBOR[i].GetByteString();

                // decrypt shard                
                byte[] shard = CryptoUtils.Decrypt(encryptedShard, encrypts[keyIndex], src);

                // copy shard to shard matrix
                shards[i] = new byte[shard.Length];
                Array.Copy(shard, shards[i], shard.Length);
            }

            return shards;
        }

        public static string GetAndVerifyTransactionFromCBOR(byte[] requestBytes, ref byte[][] shards, ref byte[] src, bool useLogins)
        {
            // Decode request's CBOR bytes  
            CBORObject requestCBOR = CBORObject.DecodeFromBytes(requestBytes);

            byte[] shardsCBORBytes = requestCBOR[0].GetByteString();
            byte[] hmacResultBytes = requestCBOR[1].GetByteString();

            shards = GetShardsFromCBOR(shardsCBORBytes, ref src, useLogins);

            List<byte[]> signs = !useLogins ? KeyStore.Inst.GetSIGNS(src) : KeyStore.Inst.GetLOGINS(src);

            bool verified = CryptoUtils.HashIsValid(signs[0], shardsCBORBytes, hmacResultBytes);

            Console.WriteLine($"CBOR Shards Verified: {verified}");

            // Extract the shards from shards CBOR and put them in byte matrix (2D array of bytes).

            byte[] cborData = ReedSolomonUtils.RebuildDataUsingReeedSolomon(shards);

            CBORObject rebuiltTransactionCBOR = CBORObject.DecodeFromBytes(cborData);

            string rebuiltDataJSON = rebuiltTransactionCBOR.ToJSONString();

            return rebuiltDataJSON;
        }

        public static byte [][] GetAndVerifyShardsFromCBOR(byte[] requestBytes, ref byte[] src, bool useLogins)
        {
            // Decode request's CBOR bytes  
            CBORObject requestCBOR = CBORObject.DecodeFromBytes(requestBytes);

            byte[] transanctionShardsCBORBytes = requestCBOR[0].GetByteString();
            byte[] hmacResultBytes = requestCBOR[1].GetByteString();

            byte[][] shards = GetShardsFromCBOR(transanctionShardsCBORBytes, ref src);

            List<byte[]> signs = !useLogins ? KeyStore.Inst.GetSIGNS(src) : KeyStore.Inst.GetLOGINS(src);

            bool verified = CryptoUtils.HashIsValid(signs[0], transanctionShardsCBORBytes, hmacResultBytes);

            Console.WriteLine($"CBOR Shard Data Verified: {verified}");

            return shards;
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

        // PostTransaction endpoint
        [HttpPost]
        [Route("Invite")]       
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult> Invite()
        //public async Task<HttpResponseMessage> PostTransaction()
        {
            Console.WriteLine("TransactionsController Invite");
           
            byte[] requestBytes;
            using (var ms = new MemoryStream())
            {
                await Request.Body.CopyToAsync(ms);
                requestBytes = ms.ToArray();  
            }
            byte[] src = new byte[CryptoUtils.SRC_SIZE_8];
            CBORObject requestCBOR = CBORObject.DecodeFromBytes(requestBytes);

            byte[] transanctionShardsCBORBytes = requestCBOR[0].GetByteString();
            byte[] hmacResultBytes = requestCBOR[1].GetByteString();

            byte[][] shards = GetShardsFromCBOR(transanctionShardsCBORBytes, ref src);

            //List<byte[]> signs = KeyStore.Inst.GetSIGNS(src);

            //bool verified = CryptoUtils.HashIsValid(signs[0], shardsCBORBytes, hmacResultBytes);

            //Console.WriteLine($"CBOR Shard Data Verified: {verified}");


            string endPoint = Servers.INVITE_ENDPOINT;
            byte [] response = await _clientService.PostTransaction(shards, src, hmacResultBytes, endPoint);

            if (response == null)
            {
                return BadRequest("failed to Invite!");
            }        

            return Ok(response);
            /*
            //servers receive + validate the invite transaction
            
            byte[] requestBytes;
            using (var ms = new MemoryStream())
            {
                await Request.Body.CopyToAsync(ms);
                requestBytes = ms.ToArray();
            }

            byte[] src = new byte[CryptoUtils.SRC_SIZE_8];
            byte[][] shards = new byte[1][];
            string rebuiltDataJSON = GetAndVerifyTransactionFromCBOR(requestBytes, ref shards, ref src, false);
           
            Console.WriteLine("PostTransaction:");
            Console.WriteLine($"Rebuilt Data: {rebuiltDataJSON} ");
            Console.WriteLine();

            InviteRequest transactionObj =
               JsonConvert.DeserializeObject<InviteRequest>(rebuiltDataJSON);

            // servers store invite.SIGNS + invite.ENCRYPTS for device.id = invite.id         
            byte[] inviteID = CryptoUtils.CBORBinaryStringToBytes(transactionObj.inviteID);
            KeyStore.Inst.StoreENCRYPTS(inviteID, transactionObj.inviteENCRYPTS);
            KeyStore.Inst.StoreSIGNS(inviteID, transactionObj.inviteSIGNS);

            // response is just OK, but any response data must be encrypted + signed using owner.KEYS
            var cbor = CBORObject.NewMap().Add("INVITE", "SUCCESS");

            //return Ok(cbor.ToJSONString());
            //return ReturnBytes(cbor.EncodeToBytes());
            return Ok(cbor.EncodeToBytes());
            */
        }

        // Register endpoint
        [HttpPost]
        [Route("Register")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult> Register()
        {
            Console.WriteLine("TransactionsController Register");
           
            byte[] requestBytes;
            using (var ms = new MemoryStream())
            {
                await Request.Body.CopyToAsync(ms);
                requestBytes = ms.ToArray();
            }

            byte[] src = new byte[CryptoUtils.SRC_SIZE_8];
            CBORObject requestCBOR = CBORObject.DecodeFromBytes(requestBytes);

            byte[] transanctionShardsCBORBytes = requestCBOR[0].GetByteString();
            byte[] hmacResultBytes = requestCBOR[1].GetByteString();

            byte[][] shards = GetShardsFromCBOR(transanctionShardsCBORBytes, ref src);

            string endPoint = Servers.REGISTER_ENDPOINT;
            byte[] response = await _clientService.PostTransaction(shards, src, hmacResultBytes, endPoint);

            if (response == null)
            {
                return BadRequest("failed to Register!");
            }

            return Ok(response);

            /*
            
            byte[] requestBytes;
            using (var ms = new MemoryStream())
            {
                await Request.Body.CopyToAsync(ms);
                requestBytes = ms.ToArray();
            }

            // Decode request's CBOR bytes   
            byte[] src = new byte[CryptoUtils.SRC_SIZE_8];
            byte[][] shards = new byte[1][];
            string rebuiltDataJSON = GetAndVerifyTransactionFromCBOR(requestBytes, ref shards, ref src, false);
            Console.WriteLine("Register:");
            Console.WriteLine($"Rebuilt Data: {rebuiltDataJSON} ");
            Console.WriteLine();

            RegisterRequest transactionObj =
               JsonConvert.DeserializeObject<RegisterRequest>(rebuiltDataJSON);
                   
            byte[] NONCE = CryptoUtils.CBORBinaryStringToBytes(transactionObj.NONCE);
            byte[] wTOKEN = CryptoUtils.CBORBinaryStringToBytes(transactionObj.wTOKEN);
            byte[] deviceID = CryptoUtils.CBORBinaryStringToBytes(transactionObj.deviceID);

            // servers create SE[] = create ECDH key pair        
            List<byte[]> SE_PUB = new List<byte[]>();
            List<byte[]> SE_PRIV = new List<byte[]>();
            for (int n = 0; n <= Servers.NUM_SERVERS; n++)
            {
                var keyPairECDH = CryptoUtils.CreateECDH();
                SE_PUB.Add(CryptoUtils.ConverCngKeyBlobToRaw(keyPairECDH.PublicKey));               
                SE_PRIV.Add(keyPairECDH.PrivateKey);
            }

            // servers store wTOKEN + NONCE                    
            KeyStore.Inst.StoreNONCE(deviceID, NONCE);
            KeyStore.Inst.StoreWTOKEN(deviceID, wTOKEN);

            // server response is ok
            var cbor = CBORObject.NewMap().Add("REGISTER", "SUCCESS");

            return Ok(cbor.EncodeToBytes());    
            */
        }

        // Register endpoint
        [HttpPost]
        [Route("Rekey")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult> Rekey()
        {
            Console.WriteLine("TransactionsController Rekey");
            
            byte[] requestBytes;
            using (var ms = new MemoryStream())
            {
                await Request.Body.CopyToAsync(ms);
                requestBytes = ms.ToArray();
            }

            byte[] src = new byte[CryptoUtils.SRC_SIZE_8];
            CBORObject requestCBOR = CBORObject.DecodeFromBytes(requestBytes);

            byte[] transanctionShardsCBORBytes = requestCBOR[0].GetByteString();
            byte[] hmacResultBytes = requestCBOR[1].GetByteString();

            byte[][] shards = GetShardsFromCBOR(transanctionShardsCBORBytes, ref src);

            string endPoint = Servers.REKEY_ENDPOINT;
            byte[] response = await _clientService.PostTransaction(shards, src, hmacResultBytes, endPoint);

            if (response == null)
            {
                return BadRequest("failed to Rekey!");
            }

            int shardLength = response.Length / Servers.NUM_SERVERS;
            List<CBORObject> cbors = new List<CBORObject>();
                        
            int offset = 0;
            for (int i = 0; i < Servers.NUM_SERVERS; i++)
            {
                byte[] cborShard = new byte[shardLength];
                Array.Copy(response, offset, cborShard, 0, shardLength);
                var cborObj = CBORObject.DecodeFromBytes(cborShard);
                cbors.Add(cborObj);
                offset += shardLength;
            }

            byte[] wTOKEN = cbors[0]["wTOKEN"].GetByteString();

            List<byte[]> SE_PUB = new List<byte[]>();
            SE_PUB.Add(cbors[0]["SE_PUB"].GetByteString());

            for (int i = 0; i < Servers.NUM_SERVERS; i++)
            {
                SE_PUB.Add(cbors[i]["SE_PUB"].GetByteString());
            } 

            //  response is wTOKEN, SE.PUB[] 
            var cbor = CBORObject.NewMap()
            .Add("wTOKEN", wTOKEN)
            .Add("SE_PUB", SE_PUB);

            return Ok(cbor.EncodeToBytes());
            /*
            byte[] requestBytes;
            using (var ms = new MemoryStream())
            {
                await Request.Body.CopyToAsync(ms);
                requestBytes = ms.ToArray();
            }

            // Decode request's CBOR bytes   
            byte[] deviceID = new byte[CryptoUtils.SRC_SIZE_8];
            byte[][] shards = new byte[1][];
            string rebuiltDataJSON = GetAndVerifyTransactionFromCBOR(requestBytes, ref shards, ref deviceID, false);
            Console.WriteLine("Rekey:");
            Console.WriteLine($"Rebuilt Data: {rebuiltDataJSON} ");
            Console.WriteLine();

            RekeyRequest transactionObj =
               JsonConvert.DeserializeObject<RekeyRequest>(rebuiltDataJSON);

            byte[] DS_PUB = CryptoUtils.CBORBinaryStringToBytes(transactionObj.DS_PUB);
            byte[] DE_PUB = CryptoUtils.CBORBinaryStringToBytes(transactionObj.DE_PUB);
            byte[] NONCE = CryptoUtils.CBORBinaryStringToBytes(transactionObj.NONCE);
         
            // servers create SE[] = create ECDH key pair        
            List<byte[]> SE_PUB = new List<byte[]>();
            List<byte[]> SE_PRIV = new List<byte[]>();
            for (int n = 0; n <= Servers.NUM_SERVERS; n++)
            {
                var keyPairECDH = CryptoUtils.CreateECDH();
                SE_PUB.Add(CryptoUtils.ConverCngKeyBlobToRaw(keyPairECDH.PublicKey));
                SE_PRIV.Add(keyPairECDH.PrivateKey);
            }

            // servers unwrap wKEYS using NONCE + store KEYS
            List<byte[]> ENCRYPTS = new List<byte[]>();
            List<byte[]> SIGNS = new List<byte[]>();
            byte[] oldNONCE = KeyStore.Inst.GetNONCE(deviceID);
            for (int n = 0; n < CryptoUtils.NUM_SIGNS_OR_ENCRYPTS; n++)
            {
                byte[] wENCRYPT = CryptoUtils.CBORBinaryStringToBytes(transactionObj.wENCRYPTS[n]);
                byte[] unwrapENCRYPT = CryptoUtils.Unwrap(wENCRYPT, oldNONCE);
                ENCRYPTS.Add(unwrapENCRYPT);

                byte[] wSIGN = CryptoUtils.CBORBinaryStringToBytes(transactionObj.wSIGNS[n]);
                byte[] unwrapSIGN = CryptoUtils.Unwrap(wSIGN, oldNONCE);
                SIGNS.Add(unwrapSIGN);
            }
            KeyStore.Inst.StoreENCRYPTS(deviceID, ENCRYPTS);
            KeyStore.Inst.StoreSIGNS(deviceID, SIGNS);

            // servers store DS PUB + NONCE
            KeyStore.Inst.StoreDS_PUB(deviceID, DS_PUB);
            KeyStore.Inst.StoreNONCE(deviceID, NONCE);           

            // servers foreach (n > 0),  store LOGINS[n] = ECDH.derive (SE.PRIV[n], DE.PUB) for device.id
            List<byte[]> LOGINS = new List<byte[]>();
            for (int n = 0; n <= Servers.NUM_SERVERS; n++)
            {
                byte[] derived = CryptoUtils.ECDHDerive(SE_PRIV[n], DE_PUB);
                LOGINS.Add(derived);
            }
            KeyStore.Inst.StoreLOGINS(deviceID, LOGINS);

            byte[] wTOKEN = KeyStore.Inst.GetWTOKEN(deviceID);       

            //  response is wTOKEN, SE.PUB[] 
            var cbor = CBORObject.NewMap()
                .Add("wTOKEN", wTOKEN)
                .Add("SE_PUB", SE_PUB);

            return Ok(cbor.EncodeToBytes());
           */
        }


        // Login endpoint
        [HttpPost]
        [Route("Login")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult> Login()
        {
            Console.WriteLine("TransactionsController Login");
           
            byte[] requestBytes;
            using (var ms = new MemoryStream())
            {
                await Request.Body.CopyToAsync(ms);
                requestBytes = ms.ToArray();
            }

            byte[] src = new byte[CryptoUtils.SRC_SIZE_8];
            CBORObject requestCBOR = CBORObject.DecodeFromBytes(requestBytes);

            byte[] transanctionShardsCBORBytes = requestCBOR[0].GetByteString();
            byte[] hmacResultBytes = requestCBOR[1].GetByteString();

            byte[][] shards = GetShardsFromCBOR(transanctionShardsCBORBytes, ref src);

            string endPoint = Servers.LOGIN_ENDPOINT;
            byte[] response = await _clientService.PostTransaction(shards, src, hmacResultBytes, endPoint);

            if (response == null)
            {
                return BadRequest("failed to Login!");
            }

            return Ok(response);
            /*
            byte[] requestBytes;
            using (var ms = new MemoryStream())
            {
                await Request.Body.CopyToAsync(ms);
                requestBytes = ms.ToArray();
            }

            // Decode request's CBOR bytes
            // servers receive + validate the login transaction
            byte[] deviceID = new byte[CryptoUtils.SRC_SIZE_8];
            byte[][] shards = new byte[1][];
            string rebuiltDataJSON = GetAndVerifyTransactionFromCBOR(requestBytes, ref shards, ref deviceID, true);
            Console.WriteLine("Login");
            Console.WriteLine($"Rebuilt Data: {rebuiltDataJSON} ");
            Console.WriteLine();

            LoginRequest transactionObj =
               JsonConvert.DeserializeObject<LoginRequest>(rebuiltDataJSON);

            // servers get LOGINS[] for device
            // servers SIGNS[] = ENCRYPTS[] = LOGINS[]                
            List<byte[]> LOGINS = KeyStore.Inst.GetLOGINS(deviceID);

            // servers unwrap + store wSIGNS + wENCRPTS using stored NONCE for device.
            List<byte[]> ENCRYPTS = new List<byte[]>();
            List<byte[]> SIGNS = new List<byte[]>();
            byte[] NONCE = CryptoUtils.CBORBinaryStringToBytes(transactionObj.NONCE);// KeyStore.Inst.GetNONCE(deviceID);
            for (int n = 0; n < CryptoUtils.NUM_SIGNS_OR_ENCRYPTS; n++)
            {
                byte[] wENCRYPT = CryptoUtils.CBORBinaryStringToBytes(transactionObj.wENCRYPTS[n]);
                byte[] unwrapENCRYPT = CryptoUtils.Unwrap(wENCRYPT, NONCE);
                ENCRYPTS.Add(unwrapENCRYPT);

                byte[] wSIGN = CryptoUtils.CBORBinaryStringToBytes(transactionObj.wSIGNS[n]);
                byte[] unwrapSIGN = CryptoUtils.Unwrap(wSIGN, NONCE);
                SIGNS.Add(unwrapSIGN);
            }
            KeyStore.Inst.StoreENCRYPTS(deviceID, ENCRYPTS);
            KeyStore.Inst.StoreSIGNS(deviceID, SIGNS);

            // servers store NONCE? 
            // KeyStore.Inst.StoreNONCE(deviceID, CryptoUtils.CBORBinaryStringToBytes(transactionObj.NONCE));  

            // servers response = wTOKEN   
            var cbor = CBORObject.NewMap().Add("wTOKEN", KeyStore.Inst.GetWTOKEN(deviceID));

            return Ok(cbor.EncodeToBytes());  
            */
        }

        // Session endpoint
        [HttpPost]
        [Route("Session")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult> Session()
        {
            Console.WriteLine("TransactionsController Session");
            
            byte[] requestBytes;
            using (var ms = new MemoryStream())
            {
                await Request.Body.CopyToAsync(ms);
                requestBytes = ms.ToArray();
            }

            byte[] src = new byte[CryptoUtils.SRC_SIZE_8];
            CBORObject requestCBOR = CBORObject.DecodeFromBytes(requestBytes);

            byte[] transanctionShardsCBORBytes = requestCBOR[0].GetByteString();
            byte[] hmacResultBytes = requestCBOR[1].GetByteString();

            byte[][] shards = GetShardsFromCBOR(transanctionShardsCBORBytes, ref src);

            string endPoint = Servers.SESSION_ENDPOINT;
            byte[] response = await _clientService.PostTransaction(shards, src, hmacResultBytes, endPoint);

            if (response == null)
            {
                return BadRequest("failed to Session!");
            }

            return Ok(response);
            /*
            byte[] requestBytes;
            using (var ms = new MemoryStream())
            {
                await Request.Body.CopyToAsync(ms);
                requestBytes = ms.ToArray();
            }

            // Decode request's CBOR bytes
            // servers receive + validate the login transaction
            byte[] deviceID = new byte[CryptoUtils.SRC_SIZE_8];
            byte[][] shards = new byte[1][];
            string rebuiltDataJSON = GetAndVerifyTransactionFromCBOR(requestBytes, ref shards, ref deviceID, false);
            Console.WriteLine("Session");
            Console.WriteLine($"Rebuilt Data: {rebuiltDataJSON} ");
            Console.WriteLine();

            SessionRequest transactionObj =
               JsonConvert.DeserializeObject<SessionRequest>(rebuiltDataJSON);
           

            // servers response = Ok   
            var cbor = CBORObject.NewMap().Add("MSG", transactionObj.MSG);

            return Ok(cbor.EncodeToBytes()); 
            */
        }
    }
}
