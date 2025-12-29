using System.Collections.Generic;
using System.Threading.Tasks;

namespace SelectML.Core
{
    public interface IDatabaseService
    {
        Task<string?> GetStationNameAsync(string batchNumber);
        Task<List<string>> GetFeaturesForRunAsync(string batchNumber);
        Task<IEnumerable<string>> GetAvailableDatabasesAsync(string connectionString);
    }
}
