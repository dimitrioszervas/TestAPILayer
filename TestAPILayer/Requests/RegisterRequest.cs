namespace TestAPILayer.Requests
{
    public sealed class RegisterRequest : BaseRequest
    {  
        public string wTOKEN { get; set; }
        public string NONCE { get; set; }
        public string deviceID { get; set; }
    }
}
