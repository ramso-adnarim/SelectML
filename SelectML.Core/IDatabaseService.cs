using System.Collections.Generic;
using System.Threading.Tasks;

namespace SelectML.Core
{
    public interface IDatabaseService
    {
        Task<string?> GetStationNameAsync(string batchNumber);
        Task<string?> GetRoutineNameAsync(string batchNumber);
        Task<List<string>> GetFeaturesForRunAsync(string batchNumber);
        Task<List<InspectionFeatureInfo>> GetInspectionRoutineFeaturesAsync(string runName);
        Task<List<string>> GetAllStationsAsync();
        Task<IEnumerable<string>> GetAvailableDatabasesAsync(string connectionString);
        Task<List<TraceabilityFieldDefinition>> GetAllAvailableTraceabilitiesAsync();
        Task<List<string>> GetRunTraceabilitiesAsync(string runName);
        Task<bool> TestConnectionAsync();
        Task TestConnectionThrowingAsync();
    }
}
