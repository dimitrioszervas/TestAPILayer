namespace TestAPILayer.Requests
{
    public sealed class LoginRequest : BaseRequest
    {      

        public string DS_PUB { get; set; }
        public string DE_PUB { get; set; }
        public List<string> wENCRYPTS { get; set; }
        public List<string> wSIGNS { get; set; }
        public string NONCE { get; set; }
    }
}
