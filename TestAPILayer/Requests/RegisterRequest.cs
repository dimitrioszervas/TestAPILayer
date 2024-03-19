namespace TestAPILayer.Requests
{
    public sealed class RegisterRequest : BaseRequest
    {
        public string encKEY { get; set; }

        public string DS_PUB;
        public string DE_PUB;
        public string wTOKEN;
        public string NONCE;
    }
}
