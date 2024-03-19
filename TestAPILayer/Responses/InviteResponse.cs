namespace TestAPILayer.Responses
{
    public sealed class InviteResponse : BaseResponse
    {
        public List<byte[]> OWN_ENCRYPTS { get; set; } = new List<byte[]>();
        public List<byte[]> OWN_SIGNS { get; set; } = new List<byte[]>();
    }
}
