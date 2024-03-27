﻿using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using PeterO.Cbor;
using System.ComponentModel.DataAnnotations;
using System.Net;
using System.Net.Http.Headers;
using System.Transactions;
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
        private static byte[][] GetShardsFromCBOR(byte[] shardsCBORBytes, ref byte[] src, bool useLogins)
        {      
            CBORObject shardsCBOR = CBORObject.DecodeFromBytes(shardsCBORBytes);         
          
            // allocate memory for the data shards byte matrix
            // Last element in the string array is not a shard but the loginID array 
            int numShards = shardsCBOR.Values.Count - 1;
            int numShardsPerServer = numShards / Servers.NUM_SERVERS;

            src = shardsCBOR[shardsCBOR.Values.Count - 1].GetByteString();

            List<byte[]> encrypts = !useLogins ? KeyStore.Inst.GetENCRYPTS(src) : KeyStore.Inst.GetLOGIN_ENCRYPTS(src);

            byte[][] dataShards = new byte[numShards][];
            for (int i = 0; i < numShards; i++)
            {
                // we start inviteENCRYPTS[1] we don't use inviteENCRYPTS[0]
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
       
        public static string GetTransactionFromCBOR(byte[] requestBytes, ref byte[] src, bool useLogins)
        {
            // Decode request's CBOR bytes  
            CBORObject requestCBOR = CBORObject.DecodeFromBytes(requestBytes);

            byte[] transanctionShardsCBORBytes = requestCBOR[0].GetByteString();
            byte[] hmacResultBytes = requestCBOR[1].GetByteString();
           
            byte[][] transactionShards = GetShardsFromCBOR(transanctionShardsCBORBytes, ref src, useLogins);
            
            List<byte[]> signs = !useLogins ? KeyStore.Inst.GetSIGNS(src) : KeyStore.Inst.GetLOGIN_SIGNS(src); 

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

            //servers receive + validate the invite transaction

            // Decode request's CBOR bytes   
            byte[] src = new byte[CryptoUtils.SRC_SIZE_8];
            string rebuiltDataJSON = GetTransactionFromCBOR(requestBytes, ref src, false);

            Console.WriteLine("Invite:");
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
            byte[] src = new byte[CryptoUtils.SRC_SIZE_8];
            string rebuiltDataJSON = GetTransactionFromCBOR(requestBytes,ref src, false);
            Console.WriteLine("Register:");
            Console.WriteLine($"Rebuilt Data: {rebuiltDataJSON} ");
            Console.WriteLine();

            RegisterRequest transactionObj =
               JsonConvert.DeserializeObject<RegisterRequest>(rebuiltDataJSON);
          
            byte[] DS_PUB = CryptoUtils.CBORBinaryStringToBytes(transactionObj.DS_PUB);
            byte[] DE_PUB = CryptoUtils.CBORBinaryStringToBytes(transactionObj.DE_PUB);
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

            // servers store DS PUB + wTOKEN + oldNONCE
            KeyStore.Inst.StoreDS_PUB(deviceID, DS_PUB);            
            KeyStore.Inst.StoreNONCE(deviceID, NONCE);
            KeyStore.Inst.StoreWTOKEN(deviceID, wTOKEN);         

            // servers foreach (n > 0),  store LOGINS[n] = ECDH.derive (SE.PRIV[n], DE.PUB) for device.id
            List<byte[]> LOGIN_ENCRYPTS = new List<byte[]>();
            List<byte[]> LOGIN_SIGNS = new List<byte[]>();
            for (int n = 0; n <= Servers.NUM_SERVERS; n++)
            {
                byte[] derivedECDHEncrypt = CryptoUtils.ECDHDeriveEncrypt(SE_PRIV[n], DE_PUB);
                LOGIN_ENCRYPTS.Add(derivedECDHEncrypt);
                
                Console.WriteLine($"{derivedECDHEncrypt.Length}: {CryptoUtils.ByteArrayToStringDebug(derivedECDHEncrypt)}");

                byte[] derivedECDHSign = CryptoUtils.ECDHDeriveSign(SE_PRIV[n], DE_PUB);
                LOGIN_SIGNS.Add(derivedECDHSign);
               
            }
            KeyStore.Inst.StoreLOGIN_ENCRYPTS(deviceID, LOGIN_ENCRYPTS);
            KeyStore.Inst.StoreLOGIN_SIGNS(deviceID, LOGIN_SIGNS);

            //  response is wTOKEN, SE.PUB[] 
            var cbor = CBORObject.NewMap()              
                .Add("SE_PUB", SE_PUB);

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
            byte[] deviceID = new byte[CryptoUtils.SRC_SIZE_8]; 
            string rebuiltDataJSON = GetTransactionFromCBOR(requestBytes, ref deviceID, true);
            Console.WriteLine("Login");
            Console.WriteLine($"Rebuilt Data: {rebuiltDataJSON} ");
            Console.WriteLine();

            LoginRequest transactionObj =
               JsonConvert.DeserializeObject<LoginRequest>(rebuiltDataJSON);

            // servers unwrap wKEYS using oldNONCE + store KEYS
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

            // servers create SE[] = create ECDH key pair        
            List<byte[]> SE_PUB = new List<byte[]>();
            List<byte[]> SE_PRIV = new List<byte[]>();
            for (int n = 0; n < Servers.NUM_SERVERS; n++)
            {
                var keyPairECDH = CryptoUtils.CreateECDH();
                SE_PUB.Add(CryptoUtils.ConverCngKeyBlobToRaw(keyPairECDH.PublicKey));
                SE_PRIV.Add(keyPairECDH.PrivateKey);
            }

            //servers store SE.PRIV[]
            KeyStore.Inst.StoreSE_PRIV(deviceID, SE_PRIV);

            //  servers store DS PUB  + oldNONCE  
            KeyStore.Inst.StoreDS_PUB(deviceID, CryptoUtils.CBORBinaryStringToBytes(transactionObj.DS_PUB));
            KeyStore.Inst.StoreNONCE(deviceID, CryptoUtils.CBORBinaryStringToBytes(transactionObj.NONCE));         

            // response is wTOKEN, SE.PUB[]
            var cbor = CBORObject.NewMap()
                .Add("wTOKEN", KeyStore.Inst.GetWTOKEN(deviceID))
                .Add("SE_PUB", SE_PUB);

            return Ok(cbor.EncodeToBytes());
        }
    }
}
