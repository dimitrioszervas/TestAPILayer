using PeterO.Cbor;

namespace TestAPILayer
{
    public sealed class ShardsPacket
    {
        const string SESSION_ID = "SESSION_ID";
        const string NUM_SHARDS_PER_SERVER = "NSHARDS";
        const string DATA_SHARDS = "DSHARDS";
        const string METADATA_SHARDS = "MSHARDS";
        const string SHARD_INDICES = "SHARD_IND";
        const string NUM_TOTAL_SHARDS = "NTSHARDS";
        const string NUM_DATA_SHARDS = "NDSHARDS";
        const string NUM_PARITY_SHARDS = "NPSHARDS";
        const string METADATA_SHARD_LENGTH = "MSHARD_LEN";
        const string DATA_SHARD_LENGTH = "DSHARD_LEN";
        const string SOURCE = "SRC";
        const string HMAC_RESULT = "HMAC_RES";

        public Guid SessionId { get; set; }
        public List<int> ShardNo { get; set; } = new List<int>();
        public int NumShardsPerServer { get; set; }
        public int NumTotalShards { get; set; }
        public int NumDataShards { get; set; }
        public int NumParityShards { get; set; }
        public int MetadataShardLength { get; set; }
        public int DataShardLength { get; set; }
        public List<byte[]> MetadataShards { get; set; } = new List<byte[]>();
        public List<byte[]> DataShards { get; set; } = new List<byte[]>();
        public byte[] SRC { get; set; }
        public byte[] hmacResult {  get; set; }

        public ShardsPacket()
        {
        }

        public ShardsPacket(byte[] cborBytes)
        {
            DecodeFromCBORBytes(cborBytes);
        }

        public void AddMetadataShard(byte[] metadataShard)
        {
            MetadataShards.Add(metadataShard);
        }

        public void AddDataShard(byte[] dataShard)
        {
            DataShards.Add(dataShard);
        }

        public void AddShardNo(int shardNo)
        {
            ShardNo.Add(shardNo);
        }

        public byte[] EncodeToCBORBytes()
        {
            var cborObject = CBORObject.NewMap()
                .Add(SESSION_ID, SessionId.ToString())
                .Add(NUM_SHARDS_PER_SERVER, NumShardsPerServer)
                .Add(DATA_SHARDS, DataShards)
                .Add(METADATA_SHARDS, MetadataShards)
                .Add(SHARD_INDICES, ShardNo)
                .Add(NUM_TOTAL_SHARDS, NumTotalShards)
                .Add(NUM_DATA_SHARDS, NumDataShards)
                .Add(NUM_PARITY_SHARDS, NumParityShards)
                .Add(METADATA_SHARD_LENGTH, MetadataShardLength)
                .Add(DATA_SHARD_LENGTH, DataShardLength)
                .Add(SOURCE, SRC)
                .Add(HMAC_RESULT, hmacResult);
            
            return cborObject.EncodeToBytes();
        }

        public void DecodeFromCBORBytes(byte[] bytes)
        {
            CBORObject cborObject = CBORObject.DecodeFromBytes(bytes);

            this.SessionId = Guid.Parse(cborObject[SESSION_ID].AsString());
            this.NumShardsPerServer = cborObject[NUM_SHARDS_PER_SERVER].AsInt32();
            this.DataShards = cborObject[DATA_SHARDS].ToObject<List<byte[]>>();
            this.MetadataShards = cborObject[METADATA_SHARDS].ToObject<List<byte[]>>();
            this.ShardNo = cborObject[SHARD_INDICES].ToObject<List<int>>();
            this.NumTotalShards = cborObject[NUM_TOTAL_SHARDS].AsInt32();
            this.NumDataShards = cborObject[NUM_DATA_SHARDS].AsInt32();
            this.NumParityShards = cborObject[NUM_PARITY_SHARDS].AsInt32();
            this.MetadataShardLength = cborObject[METADATA_SHARD_LENGTH].AsInt32();
            this.DataShardLength = cborObject[DATA_SHARD_LENGTH].AsInt32();
            this.SRC = cborObject[SOURCE].GetByteString();
            this.hmacResult = cborObject[HMAC_RESULT].GetByteString();   
        }
    }
}
