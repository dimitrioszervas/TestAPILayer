namespace TestAPILayer
{
    /// <summary>
    /// Class that handles an unsigned transaction of list of requests.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public sealed class UnsignedTransaction<T> where T : class
    {
        public string bID { get; set; }
        public string dID { get; set; }
        public string tID { get; set; }
        public string TS { get; set; }
        public bool RT { get; set; }
        public List<T> REQ { get; set; } = new List<T>();

    }
}
