namespace TestAPILayer.Requests
{
    public sealed class InviteUserRequest : BaseRequest
    {        
        public string encKEY { get; set; }
        public List<string> encrypts {  get; set; }
        public List<string> signs { get; set; }
        
    }
}
