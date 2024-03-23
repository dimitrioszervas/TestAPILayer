namespace TestAPILayer.Requests
{
    public sealed class InviteRequest : BaseRequest
    {             
        public List<string> invENCRYPTS {  get; set; }
        public List<string> invSIGNS { get; set; }
        public string inviteID { get; set; }
    }
}
