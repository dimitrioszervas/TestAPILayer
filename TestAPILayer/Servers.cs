
using System.IO.Compression;
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
            /*
            const string SETTINGS_FILE = "settings.txt";
            List<string> hosts = new List<string>();
            try
            {
                string[] lines = File.ReadAllLines(@SETTINGS_FILE);

                foreach (string line in lines)
                {
                    string[] fields = line.Split(" ");

                    string host = fields[1];

                    hosts.Add(host);
                }
            }
            catch(Exception ex)
            {
                Console.WriteLine(ex.Message);
                return false;
            }

            HttpClients = new HttpClient[hosts.Count];

            for (int i = 0; i < hosts.Count; i++)
            {
                try
                {
                    HttpClients[i] = new()
                    {
                        BaseAddress = new Uri("http://" + hosts[i]),
                    };
                    HttpClients[i].Timeout = TimeSpan.FromSeconds(5 * 60);
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex.Message);
                    return false;
                }
            } // end for
            */
            HttpClients = new HttpClient[NUM_SERVERS];

            try
            {
                HttpClients[0] = new()
                {
                    BaseAddress = new Uri("http://poc.sealstone:5275"),
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
                    BaseAddress = new Uri("http://poc.sealstone:5276"),
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
                    BaseAddress = new Uri("http://poc.sealstone:5277"),
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

        /*
        /// <summary>
        /// Makes an asyncrinoous Post request.
        /// </summary>
        /// <param name="httpClient"></param>
        /// <param name="shardsPacket"></param>
        /// <param name="endPoint"></param>
        /// <returns>json string</returns>
        private async Task<string> PostAsync(HttpClient httpClient, ShardsPacket shardsPacket, string endPoint)
        {
            using StringContent jsonContent = new(
                System.Text.Json.JsonSerializer.Serialize(shardsPacket),
                Encoding.UTF8,
                "application/json");

            using HttpResponseMessage response = await httpClient.PostAsync(endPoint, jsonContent);

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

            return null;
        }
        */
        /// <summary>
        /// Compresses data.
        /// </summary>
        /// <param name="byteArray"></param>
        /// <returns>a byte array</returns>
        private byte[] CompressData(byte[] byteArray)
        {
            try
            {
                MemoryStream strm = new MemoryStream();
                GZipStream GZipStrem = new GZipStream(strm, CompressionMode.Compress, true);
                GZipStrem.Write(byteArray, 0, byteArray.Length);
                GZipStrem.Flush();
                strm.Flush();
                byte[] ByteArrayToreturn = strm.GetBuffer();
                GZipStrem.Close();
                strm.Close();
                return ByteArrayToreturn;
            }
            catch (Exception e)
            {
                Console.WriteLine(e.ToString());
                throw;
            }
        }

        /// <summary>
        /// Strips padding from data.
        /// </summary>
        /// <param name="paddedData"></param>
        /// <returns>a byte array stripped from padding</returns>
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
        /*
        /// <summary>
        /// Makes an asynchronous Post transaction to a server. 
        /// </summary>
        /// <param name="transactionBytes"></param>
        /// <param name="endPoint"></param>
        /// <param name="fileData"></param>
        /// <returns>a byte array</returns>
        public async Task<byte[]> PostTransactionAsync(byte[] transactionBytes, string endPoint, byte[] fileData)
        {
            try
            {

                CalculateReedSolomonShards calculateMetadataShards = null;
                byte[][] metadataShards = null;

                CalculateReedSolomonShards calculateFileShards = null;
                byte[][] fileShards = null;

                if (fileData != null)
                {
                    byte[] compressedFileData = CompressData(fileData);
                    calculateFileShards = new CalculateReedSolomonShards(compressedFileData, HttpClients.Length);
                    fileShards = calculateFileShards.Shards;

                    calculateMetadataShards = new CalculateReedSolomonShards(transactionBytes, calculateFileShards.TotalNShards, HttpClients.Length);
                    metadataShards = calculateMetadataShards.Shards;
                }
                else
                {
                    calculateMetadataShards = new CalculateReedSolomonShards(transactionBytes, HttpClients.Length);
                    metadataShards = calculateMetadataShards.Shards;
                }

                ShardsPacket[] shardsPackets = new ShardsPacket[HttpClients.Length];

                // Create shards packets (packet number is equal to server number)
                int currentShard = 0;
                Guid sessionId = Guid.NewGuid();
                for (int i = 0; i < shardsPackets.Length; i++)
                {
                    shardsPackets[i] = new ShardsPacket
                    {
                        SessionId = sessionId,
                        NumShardsPerServer = calculateMetadataShards.NumShardsPerServer,
                        NumTotalShards = calculateMetadataShards.TotalNShards,
                        NumDataShards = calculateMetadataShards.DataNShards,
                        NumParityShards = calculateMetadataShards.ParityNShards,

                        MetadataShardLength = calculateMetadataShards.ShardLength
                    };

                    if (calculateFileShards != null)
                    {
                        shardsPackets[i].DataShardLength = calculateFileShards.ShardLength;
                    }

                    for (int shardNo = 0; shardNo < calculateMetadataShards.NumShardsPerServer; shardNo++)
                    {
                        shardsPackets[i].AddMetadataShard(metadataShards[currentShard]);
                        shardsPackets[i].AddShardNo(currentShard);

                        if (fileShards != null)
                        {
                            shardsPackets[i].AddDataShard(fileShards[currentShard]);
                        }

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
                            jsonResponse = await PostAsync(client, shardsPacket, endPoint);
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"PostTransactionAsync to Server {n + 1} failed: {ex.Message}");
                            return;
                        }

                        //Console.WriteLine($"{jsonResponse}");
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
        */
    }
}
