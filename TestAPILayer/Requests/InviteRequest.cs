namespace TestAPILayer.Requests
{
    public sealed class InviteRequest : BaseRequest
    {        
        public string encKEY { get; set; }
        public List<string> OWN_ENCRYPTS {  get; set; }
        public List<string> OWN_SIGNS { get; set; }
        
    }
}
