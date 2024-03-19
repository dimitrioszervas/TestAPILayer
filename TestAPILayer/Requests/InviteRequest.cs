namespace TestAPILayer.Requests
{
    public sealed class InviteRequest : BaseRequest
    {        
        public string encKEY { get; set; }
        public List<string> ENCRYPTS {  get; set; }
        public List<string> SIGNS { get; set; }
        
    }
}
