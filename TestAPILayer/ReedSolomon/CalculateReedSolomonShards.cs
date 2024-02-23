namespace TestAPILayer.ReedSolomon
{
    public class CalculateReedSolomonShards
    {

        public byte[][] Shards { get; set; }
        public int TotalNShards { get; set; }
        public int ParityNShards { get; set; }
        public int DataNShards { get; set; }
        public int NumShardsPerServer { get; set; }
        public int ShardLength { get; set; }

        public CalculateReedSolomonShards(byte[] data, int numServers)
        {

            TotalNShards = CalculateNShards(data.Length, numServers);
            ParityNShards = TotalNShards / 2;
            DataNShards = TotalNShards - ParityNShards;
            NumShardsPerServer = TotalNShards / numServers;

            Shards = CalculateShardsUsingReedSolomon(data, TotalNShards, ParityNShards, DataNShards);

        }

        private int CalculateDataPadding(int dataSize, int numShards)
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

        private int CalculateNShards(int dataSize, int nServers)
        {

            int nShards = (1 + dataSize / 256) * nServers;

            if (nShards > 255)
            {
                nShards = 255;
            }

            return nShards;
        }

        private byte[][] CalculateShardsUsingReedSolomon(
            byte[] dataBytes,
            int totalNShards,
            int parityNShards,
            int dataNShards)
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

    }
}
