namespace TestAPILayer.Dtos
{
    public class CryptoAESKeyDto
    {
        public string alg { get; set; }
        public bool ext { get; set; }
        public string k { get; set; }
        public string[] key_ops { get; set; } = new string[2];
        public string kty { get; set; }
    }
}
