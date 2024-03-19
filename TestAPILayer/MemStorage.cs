namespace TestAPILayer
{
    public static class MemStorage
    {
        public static byte[][] ENCRYPTS = new byte[CryptoUtils.NUM_SERVERS + 1][];
        public static byte[][] SIGNS = new byte[CryptoUtils.NUM_SERVERS + 1][];

        public static byte[][] SE_PRIV = new byte[CryptoUtils.NUM_SERVERS][];

        public static byte[] DS_PUB;
        public static byte[] DE_PUB;
        public static byte[] NONCE;
    }
}
