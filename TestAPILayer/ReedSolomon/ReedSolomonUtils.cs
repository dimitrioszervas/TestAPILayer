namespace TestAPILayer.ReedSolomon
{
    public static class ReedSolomonUtils
    {
        // Strips padding addded during the Reed-Solomon sharding
        private static byte[] StripPadding(byte[] paddedData)
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
            catch (Exception ex)
            {
                Console.WriteLine(ex);
                throw;
            }
        }

        public static byte[] RebuildDataUsingReeedSolomon(byte[][] shards)
        {
            // Get shard length (all shards are of equal length).
            int shardLength = shards[0].Length;

            int nTotalShards = shards.Length;
            int nParityShards = nTotalShards / 2;
            int nDataShards = nTotalShards - nParityShards;

            // Set which shards are present - you have to have a minimum number = number of data shards
            bool[] shardsPresent = new bool[nTotalShards];

            for (int i = 0; i < nDataShards; i++)
            {
                shardsPresent[i] = true;
            }

            // Replicate the other shards using Reeed-Solomom.
            var reedSolomon = new ReedSolomon(shardsPresent.Length - nParityShards, nParityShards);
            reedSolomon.DecodeMissing(shards, shardsPresent, 0, shardLength);

            // Write the Reed-Solomon matrix of shards to a 1D array of bytes
            byte[] rebuiltDataBytes = new byte[shards.Length * shardLength];
            int offSet = 0;
            for (int j = 0; j < shards.Length - nParityShards; j++)
            {
                Array.Copy(shards[j], 0, rebuiltDataBytes, offSet, shardLength);
                offSet += shardLength;
            }

            // Decode rebuilt CBOR data bytes, after stripping the padding needed for the Reed-Solomon
            // which requires that all shards have to be equal in length. 
            return StripPadding(rebuiltDataBytes);
        }
    }
}
