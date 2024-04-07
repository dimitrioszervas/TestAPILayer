using static System.Runtime.InteropServices.JavaScript.JSType;

namespace TestAPILayer.ReedSolomon
{
    /// <summary>
    /// Calculates the shards of a byte array using Reed-Solomon.
    /// </summary>
    public class CalculateReedSolomonShards
    {

        public byte[][] Shards { get; set; }
        public int TotalNShards { get; set; }
        public int ParityNShards { get; set; }
        public int DataNShards { get; set; }
        public int NumShardsPerServer { get; set; }
        public int ShardLength { get; set; }

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="data"></param>
        /// <param name="numServers"></param>
        public CalculateReedSolomonShards(byte[] data, int numServers)
        {
            try
            {

                TotalNShards = CalculateNShards(data.Length, numServers);
                ParityNShards = TotalNShards / 2;
                DataNShards = TotalNShards - ParityNShards;
                NumShardsPerServer = TotalNShards / numServers;

                Shards = CalculateShardsUsingReedSolomon(data, TotalNShards, ParityNShards, DataNShards);
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
                throw;
            }

        }

        /// <summary>
        /// Constructor. 
        /// </summary>
        /// <param name="data"></param>
        /// <param name="totalNShards"></param>
        /// <param name="numServers"></param>
        public CalculateReedSolomonShards(byte[] data, int totalNShards, int numServers)
        {
            try
            {

                TotalNShards = totalNShards;
                ParityNShards = TotalNShards / 2;
                DataNShards = TotalNShards - ParityNShards;
                NumShardsPerServer = TotalNShards / numServers;

                Shards = CalculateShardsUsingReedSolomon(data, TotalNShards, ParityNShards, DataNShards);
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
                throw;
            }

        }

        /// <summary>
        /// Calculates data padding.
        /// </summary>
        /// <param name="dataSize"></param>
        /// <param name="numShards"></param>
        /// <returns>int</returns>
        private int CalculateDataPadding(int dataSize, int numShards)
        {
            try
            {
                if (dataSize < numShards)
                {
                    return numShards;
                }

                int rem = dataSize % numShards;
                if (rem != 0)
                {
                    int newSize = numShards * (int)(dataSize / (double)numShards + 0.9);
                    if (newSize < dataSize)
                    {
                        newSize += numShards;
                    }
                    return dataSize + (newSize - dataSize);
                }
                else
                {
                    return dataSize;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
                throw;
            }
        }

        /// <summary>
        /// Calculates N shards.
        /// </summary>
        /// <param name="dataSize"></param>
        /// <param name="nServers"></param>
        /// <returns>int</returns>
        private int CalculateNShards(int dataSize, int nServers)
        {
            try
            {
                int nShards = (1 + dataSize / 256) * nServers;

                if (nShards > 255)
                {
                    nShards = 255;
                }

                return nShards;
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
                throw;
            }
        }

        /// <summary>
        /// Produces a matrix containing the Reed-Solomon shards.
        /// </summary>
        /// <param name="dataBytes"></param>
        /// <param name="totalNShards"></param>
        /// <param name="parityNShards"></param>
        /// <param name="dataNShards"></param>
        /// <returns>2d byte array</returns>
        private byte[][] CalculateShardsUsingReedSolomon(
            byte[] dataBytes,
            int totalNShards,
            int parityNShards,
            int dataNShards)
        {
            try
            {
                int paddedDataSize = CalculateDataPadding(dataBytes.Length + 1, dataNShards);

                int dataShardLength = paddedDataSize / dataNShards;

                byte[][] dataShards = new byte[totalNShards][];
                for (int row = 0; row < totalNShards; row++)
                {
                    dataShards[row] = new byte[dataShardLength];
                }

                byte[] paddedDataBytes = new byte[paddedDataSize];
                Array.Copy(dataBytes, 0, paddedDataBytes, 0, dataBytes.Length);
                paddedDataBytes[dataBytes.Length] = 1;

                int shardNo = 0;
                int metadataOffset = 0;

                ShardLength = dataShardLength;

                for (int i = 1; i <= dataNShards; i++)
                {

                    Array.Copy(paddedDataBytes, metadataOffset, dataShards[shardNo], 0, dataShardLength);
                    metadataOffset += dataShardLength;

                    shardNo++;
                }

                ReedSolomon reedSolomon = new ReedSolomon(dataNShards, parityNShards);

                reedSolomon.EncodeParity(dataShards, 0, dataShardLength);
                return dataShards;
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
                throw;
            }
        }

    }
}
