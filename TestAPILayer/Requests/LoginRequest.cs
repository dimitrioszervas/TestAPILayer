namespace TestAPILayer.Requests
{
    public sealed class LoginRequest : BaseRequest
    {
        public string encKEY { get; set; }

        public static byte[] DS_PUB;
        public static byte[] DE_PUB;
        public static byte[] NONCE;

        public static List<byte[]> WENCRYPTS { get; set; }
        public static List<byte[]> WSIGNS { get; set; }
    }
}
