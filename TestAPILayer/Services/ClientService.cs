using TestAPILayer.Contracts;

namespace TestAPILayer.Services
{
    /// <summary>
    /// Handles client services.
    /// </summary>
    public class ClientService : IClientService
    {
        public async Task<byte[]> PostTransaction(byte[][] shards, byte [] src, byte[] hmacResult, string endPoint)
        {

            try
            {    
                // post transaction to servers
                byte[] dataBytes = await Servers.Instance.PostTransactionAsync(shards, src, hmacResult, endPoint);

                if (dataBytes == null)
                {
                    return null;
                }

                return dataBytes;
            }
            catch (Exception e)
            {
                Console.WriteLine(e.ToString());
                return null;
            }
        }

    }
}
