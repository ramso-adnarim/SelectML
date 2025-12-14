using System.Threading.Tasks;

namespace SelectML.Core
{
    public interface IDatabaseService
    {
        Task<string?> GetStationNameAsync(string batchNumber);
    }
}
