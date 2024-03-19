namespace TestAPILayer
{
    public static class MemStorage
    {
        public static List<byte[]> ENCRYPTS = new List<byte[]>();
        public static List<byte[]> SIGNS = new List<byte[]>();

        public static List<byte[]> SE_PRIV = new List<byte[]>();

        public static byte[] DS_PUB;
        public static byte[] DE_PUB;
        public static byte[] NONCE;

        public static void Clear()
        {
            ENCRYPTS.Clear();
            SIGNS.Clear();
            SE_PRIV.Clear();
        }
    }
}
