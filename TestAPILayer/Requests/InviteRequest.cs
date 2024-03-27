namespace TestAPILayer.Requests
{
    public sealed class InviteRequest : BaseRequest
    {             
        public List<string> inviteENCRYPTS {  get; set; }
        public List<string> inviteSIGNS { get; set; }
        public string inviteID { get; set; }
    }
}
