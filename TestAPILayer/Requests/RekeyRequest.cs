namespace TestAPILayer.Requests
{
    public sealed class RekeyRequest : BaseRequest
    {
        public string DS_PUB { get; set; }
        public string DE_PUB { get; set; }
        public List<string> wSIGNS { get; set; } = new List<string>();
        public List<string> wENCRYPTS { get; set; } = new List<string>();
        public string NONCE { get; set; }
    }
}
