namespace TestAPILayer
{
    public sealed class CreateFolderRequest : BaseRequest
    {
        public string ID { get; set; }
        public string pID { get; set; }
        public string encKEY { get; set; }
        public string encNAM { get; set; }
    }
}
