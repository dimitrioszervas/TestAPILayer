namespace TestAPILayer.Requests
{
    public sealed class LoginRequest : BaseRequest
    {        
        public List<string> wENCRYPTS { get; set; }
        public List<string> wSIGNS { get; set; }
        public string NONCE { get; set; }
    }
}
