using System.Threading.Tasks;

namespace TestAPILayer.Contracts
{
    public interface IClientService
    {
        Task<byte[]> PostTransaction(byte[][] shards, byte [] src, byte[] hmacResult, string endPoint);
       
    }
}
