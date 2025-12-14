using System;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using SelectML.Core;

namespace SelectML.Client.Services
{
    public class DatabaseService : IDatabaseService
    {
        private readonly string _connectionString;

        public DatabaseService(string connectionString)
        {
            _connectionString = connectionString;
        }

        public async Task<string?> GetStationNameAsync(string batchNumber)
        {
            if (string.IsNullOrWhiteSpace(_connectionString))
                return null;

            // Note: Robust querying dbo.ActiveRun and dbo.Station as per requirements
            string query = @"
                SELECT TOP 1 s.StationName
                FROM dbo.ActiveRun ar
                JOIN dbo.Station s ON ar.StationID = s.StationID
                WHERE ar.RunName = @BatchNumber";

            try
            {
                using (var connection = new SqlConnection(_connectionString))
                {
                    await connection.OpenAsync();
                    using (var command = new SqlCommand(query, connection))
                    {
                        command.Parameters.AddWithValue("@BatchNumber", batchNumber);
                        var result = await command.ExecuteScalarAsync();
                        return result as string;
                    }
                }
            }
            catch (Exception)
            {
                // In a production environment, logging would happen here.
                // For now, we return null so the fallback mechanism works.
                return null;
            }
        }
    }
}
