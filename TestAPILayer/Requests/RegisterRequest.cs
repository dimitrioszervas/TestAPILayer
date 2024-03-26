namespace TestAPILayer.Requests
{
    public sealed class RegisterRequest : BaseRequest
    {       
        public string DS_PUB { get; set; }
        public string DE_PUB { get; set; }
        public string wTOKEN { get; set; }
        public string NONCE { get; set; }
        public string deviceID { get; set; }
    }
}
