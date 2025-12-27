using System;
using System.Collections.Generic;
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

        public async Task<List<string>> GetFeaturesForRunAsync(string batchNumber)
        {
            var features = new List<string>();
            if (string.IsNullOrWhiteSpace(_connectionString))
                return features;

            // Query assumes ActiveRun contains feature definitions or is the main run table
            // Based on GetStationName usage of TOP 1, ActiveRun likely contains multiple rows per run (one per feature?)
            string query = @"
                SELECT FeatureName
                FROM dbo.ActiveRun
                WHERE RunName = @BatchNumber";

            try
            {
                using (var connection = new SqlConnection(_connectionString))
                {
                    await connection.OpenAsync();
                    using (var command = new SqlCommand(query, connection))
                    {
                        command.Parameters.AddWithValue("@BatchNumber", batchNumber);
                        using (var reader = await command.ExecuteReaderAsync())
                        {
                            while (await reader.ReadAsync())
                            {
                                if (!reader.IsDBNull(0))
                                {
                                    features.Add(reader.GetString(0));
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception)
            {
                // Return empty list on error to prevent crash
            }
            return features;
        }

        public async Task<IEnumerable<string>> GetAvailableDatabasesAsync(string connectionString)
        {
            var databases = new List<string>();
            string query = "SELECT name FROM sys.databases WHERE name NOT IN ('master', 'tempdb', 'model', 'msdb')";

            try
            {
                using (var connection = new SqlConnection(connectionString))
                {
                    await connection.OpenAsync();
                    using (var command = new SqlCommand(query, connection))
                    {
                        using (var reader = await command.ExecuteReaderAsync())
                        {
                            while (await reader.ReadAsync())
                            {
                                databases.Add(reader.GetString(0));
                            }
                        }
                    }
                }
            }
            catch (Exception)
            {
                // Should propagate error or return empty to indicate failure in UI
                throw;
            }

            return databases;
        }
    }
}
