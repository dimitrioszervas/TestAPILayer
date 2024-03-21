namespace TestAPILayer.Requests
{
    public sealed class InviteRequest : BaseRequest
    {             
        public List<string> ENCRYPTS {  get; set; }
        public List<string> SIGNS { get; set; }
        public string DEVICE { get; set; }
    }
}
