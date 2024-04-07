
using Newtonsoft.Json;
using PeterO.Cbor;
using System.Net.Http.Headers;
using System.Text;

namespace TestAPILayer
{
    /// <summary>
    /// Singleton class that deals with server communication.
    /// </summary>
    public sealed class Servers
    {
        public const int NUM_SERVERS = 3;

        // Servers as HTTP clients
        private HttpClient[] HttpClients { get; set; }

        // This is required to make the Servers singleton class thread safe
        private static readonly Lazy<Servers> lazy = new Lazy<Servers>(() => new Servers());

        public HttpClient[] GetHttpClients() { return HttpClients; }

        // Static instance of the Servers class
        public static Servers Instance { get { return lazy.Value; } }

        /// <summary>
        /// Loads the settings file containing the remote host (servers) addresses. 
        /// </summary>
        /// <returns>bool</returns>
        public bool LoadSettings()
        {
            
            HttpClients = new HttpClient[NUM_SERVERS];

            try
            {
                HttpClients[0] = new()
                {
                    BaseAddress = new Uri("http://localhost:5110"), // new Uri("http://poc.sealstone:5275"),
                };
                HttpClients[0].Timeout = TimeSpan.FromSeconds(5 * 60);
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
                return false;
            }

            try
            {
                HttpClients[1] = new()
                {
                    BaseAddress = new Uri("http://localhost:5099"), // new Uri("http://poc.sealstone:5276"),
                };
                HttpClients[1].Timeout = TimeSpan.FromSeconds(5 * 60);
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
                return false;
            }

            try
            {
                HttpClients[2] = new()
                {
                    BaseAddress = new Uri("http://localhost:5200"), // new Uri("http://poc.sealstone:5277"),
                };
                HttpClients[2].Timeout = TimeSpan.FromSeconds(5 * 60);
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
                return false;
            }

            Console.WriteLine("HttpClients Initialised!");

            return true;
        }
            
      
        private async Task<string> PostAsync(HttpClient httpClient, byte[] bytes, string endPoint)
        {
            using (var content = new ByteArrayContent(bytes))
            {
                content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
                using HttpResponseMessage response = await httpClient.PostAsync(endPoint, content);

                try
                {
                    response.EnsureSuccessStatusCode();
                }
                catch (HttpRequestException e)
                {
                    Console.WriteLine($"PostAsync returned unsuccessful status code - {e.Message}");
                    return null;
                }

                var jsonResponse = await response.Content.ReadAsStringAsync();

                if (response.StatusCode == System.Net.HttpStatusCode.OK)
                {
                    return jsonResponse;
                }
            }                     

            return null;
        }     
       
    
        private byte[] StripPadding(byte[] paddedData)
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
            catch (Exception e)
            {
                Console.WriteLine(e.ToString());
                throw;
            }
        }
     
        public async Task<byte[]> PostTransactionAsync(byte[][] shards, byte[] src, byte[] hmacResult, string endPoint)
        {
            try
            {              
                ShardsPacket[] shardsPackets = new ShardsPacket[HttpClients.Length];

                int nTotalShards = shards.Length;
                int nParityShards = nTotalShards / 2;
                int nDataShards = nTotalShards - nParityShards;
                int numShardsPerServer = nTotalShards / Servers.NUM_SERVERS;
                int shardLength = shards[0].Length;

                // Create shards packets (packet number is equal to server number)
                int currentShard = 0;
                Guid sessionId = Guid.NewGuid();
                for (int i = 0; i < shardsPackets.Length; i++)
                {
                    shardsPackets[i] = new ShardsPacket
                    {
                        SessionId = sessionId,
                        NumShardsPerServer = numShardsPerServer,
                        NumTotalShards = nTotalShards,
                        NumDataShards = nDataShards,
                        NumParityShards = nParityShards,

                        MetadataShardLength = shardLength
                    };


                    shardsPackets[i].SRC = new byte[src.Length];
                    Array.Copy(src, shardsPackets[i].SRC, src.Length);

                    shardsPackets[i].hmacResult = new byte[hmacResult.Length];
                    Array.Copy(hmacResult, shardsPackets[i].hmacResult, hmacResult.Length);

                    for (int shardNo = 0; shardNo < numShardsPerServer; shardNo++)
                    {
                        shardsPackets[i].AddMetadataShard(shards[currentShard]);
                        shardsPackets[i].AddShardNo(currentShard);

                        // Progress to next msgNo //////////////////////////////////////
                        currentShard++;
                    }

                }

                // Send shards packets to Servers
                Task[] tasks = new Task[shardsPackets.Length];
                ReceivedShards receivedShards = null;

                for (int packetNo = 0; packetNo < shardsPackets.Length; packetNo++)
                {
                    int n = packetNo;
                    HttpClient client = HttpClients[n];
                    ShardsPacket shardsPacket = shardsPackets[n];

                    tasks[n] = Task.Run(async () =>
                    {
                        string jsonResponse = null;
                        try
                        {
                            byte[] packetBytes = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(shardsPacket));
                            var packetCBOR = CBORObject.NewArray().Add(packetBytes);

                            jsonResponse = await PostAsync(client, packetCBOR.EncodeToBytes(), endPoint);
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"PostTransactionAsync to Server {n + 1} failed: {ex.Message}");
                            return;
                        }

                        if (endPoint.Equals("api/Transactions/Rekey"))
                        {
                            Console.WriteLine($"{jsonResponse}");
                        }
                        if (jsonResponse != null)
                        {
                            ShardsPacket receivedShardPacket = JsonConvert.DeserializeObject<ShardsPacket>(jsonResponse);

                            if (receivedShardPacket == null)
                            {
                                return;
                            }

                            int shardLength = receivedShardPacket.DataShardLength;
                            int numTotalShards = receivedShardPacket.NumTotalShards;
                            int numDataShards = receivedShardPacket.NumDataShards;
                            int numParityShards = receivedShardPacket.NumParityShards;

                            lock (this)
                            {
                                if (receivedShards == null)
                                {
                                    receivedShards = new ReceivedShards(shardLength,
                                                                        numTotalShards,
                                                                        numDataShards,
                                                                        numParityShards);
                                }
                                else
                                {
                                    if (receivedShards.AreEnoughShardsReceived())
                                    {
                                        return;
                                    }
                                }

                                for (int shardNo = 0; shardNo < receivedShardPacket.DataShards.Count; shardNo++)
                                {
                                    receivedShards.SetShard(receivedShardPacket.ShardNo[shardNo],
                                                            receivedShardPacket.DataShards[shardNo]);

                                    if (receivedShards.AreEnoughShardsReceived())
                                    {
                                        Console.WriteLine("Enough shards have been received");
                                        return;
                                    }

                                }
                            }
                        }
                        else
                        {
                            return;
                        }
                    });
                }

                Task.WaitAll(tasks);

                foreach (Task task in tasks)
                {
                    task.Dispose();
                }

                if (receivedShards == null)
                {
                    return null;
                }

                if (receivedShards.AreEnoughShardsReceived())
                {
                    receivedShards.ReconstructData();

                    return StripPadding(receivedShards.GetReconstructedData());
                }

                return null;
            }
            catch (Exception e)
            {
                Console.WriteLine(e.ToString());
                return null;
            }
        }
       
    }
}
