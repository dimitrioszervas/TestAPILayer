namespace TestAPILayer
{
    /// <summary>
    /// Rebuilds data from received shards from the servers.
    /// </summary>
    public class ReceivedShards
    {
        private byte[][] shards;
        private bool[] shardPresent;

        private byte[] data;

        private int numReceivedShards = 0;

        private int shardLength;
        private int numTotalShards;
        private int numDataShards;
        private int numParityShards;

        private bool dataReconstructed = false;

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="shardLengthIn"></param>
        /// <param name="numTotalShardsIn"></param>
        /// <param name="numDataShardsIn"></param>
        /// <param name="numParityShardsIn"></param>
        public ReceivedShards(int shardLengthIn,
                                 int numTotalShardsIn,
                                 int numDataShardsIn,
                                 int numParityShardsIn)
        {
            try
            {
                numReceivedShards = 0;

                shards = new byte[numTotalShardsIn][];
                for (int row = 0; row < numTotalShardsIn; row++)
                {
                    shards[row] = new byte[shardLengthIn];
                }

                shardPresent = new bool[numTotalShardsIn];

                shardLength = shardLengthIn;
                numTotalShards = numTotalShardsIn;
                numDataShards = numDataShardsIn;
                numParityShards = numParityShardsIn;
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
                throw;
            }

        }

        /// <summary>
        /// Checks if data are reconstructed.
        /// </summary>
        /// <returns>bool</returns>
        public bool AreDataReconstructed()
        {
            return dataReconstructed;
        }

        /// <summary>
        /// Gets reconctructed data.
        /// </summary>
        /// <returns>byte array</returns>
        public byte[] GetReconstructedData()
        {
            return data;
        }

        /// <summary>
        /// Gets data size.
        /// </summary>
        /// <returns>int</returns>
        public int GetSize()
        {
            return data.Length;
        }

        /// <summary>
        /// Gets total number of shards.
        /// </summary>
        /// <returns>int</returns>
        public int GetTotalShards()
        {
            return numTotalShards;
        }

        /// <summary>
        /// Gets number of shards.
        /// </summary>
        /// <returns>int</returns>
        public int GetNumDataShards()
        {
            return numDataShards;
        }

        /// <summary>
        /// Get number of Parity shards.
        /// </summary>
        /// <returns>int</returns>
        public int GetParityShards()
        {
            return numParityShards;
        }

        /// <summary>
        /// Gets shard length. 
        /// </summary>
        /// <returns>int</returns>
        public int GetShardLenght()
        {
            return shardLength;
        }

        /// <summary>
        /// Sets a received shard to the right index (order).
        /// </summary>
        /// <param name="shardInNo"></param>
        /// <param name="shardIn"></param>
        public void SetShard(int shardInNo, byte[] shardIn)
        {
            try
            {
                if (!shardPresent[shardInNo])
                {
                    Array.Copy(shardIn, 0, shards[shardInNo], 0, shardIn.Length);
                    shardPresent[shardInNo] = true;
                    numReceivedShards++;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
                throw;
            }
        }

        /// <summary>
        /// Gets the current number of received shards.
        /// </summary>
        /// <returns>int</returns>
        public int GetNumReceivedShards()
        {
            return numReceivedShards;
        }

        /// <summary>
        /// Checks if are enough shards received in order to rebuild then using Reed-Solomon.
        /// </summary>
        /// <returns>bool</returns>
        public bool AreEnoughShardsReceived()
        {
            return numDataShards <= numReceivedShards;
        }

        /// <summary>
        /// Reconstruct data using Reed-Solomon if enough shards are received.
        /// </summary>
        /// <returns>bool</returns>
        public bool ReconstructData()
        {
            try
            {
                if (AreEnoughShardsReceived())
                {

                    // Reeed-Solomom.
                    var reedSolomon =
                            new ReedSolomon.ReedSolomon(shardPresent.Length - numParityShards,
                                    numParityShards);

                    reedSolomon.DecodeMissing(shards, shardPresent, 0, shardLength);

                    // Write the Reed-Solomon matrix of shards to a 1D array of bytes
                    data = new byte[shardLength * numDataShards];

                    int offset = 0;
                    for (int j = 0; j < shards.Length - numParityShards; j++)
                    {
                        Array.Copy(shards[j], 0, data, offset, shardLength);
                        offset += shardLength;
                    }

                    dataReconstructed = true;

                    return true;
                }
                return false;
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
                throw;
            }
        }
    }
}
