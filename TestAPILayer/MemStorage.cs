using System.Collections.Concurrent;

namespace TestAPILayer
{  
    public static class MemStorage
    {
        public class Keys
        {
            public byte[][] ENCRYPTS = new byte[CryptoUtils.NUM_KEYS][];
            public byte[][] SIGNS = new byte[CryptoUtils.NUM_KEYS][];
        }

        public static ConcurrentDictionary<string, Keys> KEYS = new ConcurrentDictionary<string, Keys>();       
        

        public static byte[][] SE_PRIV = new byte[CryptoUtils.NUM_SERVERS][];

        public static byte[] DS_PUB;
        public static byte[] DE_PUB;
        public static byte[] NONCE;
        public static byte[] wTOKEN;
    }
}
