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

        public async Task<string?> GetRoutineNameAsync(string batchNumber)
        {
            if (string.IsNullOrWhiteSpace(_connectionString)) return null;

            // Prompt: RoutineName from DB
            string query = @"SELECT TOP 1 RoutineName FROM dbo.ActiveRun WHERE RunName = @BatchNumber";

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
            catch { return null; }
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

        public async Task<List<InspectionFeatureInfo>> GetInspectionRoutineFeaturesAsync(string runName)
        {
            var features = new List<InspectionFeatureInfo>();
            if (string.IsNullOrWhiteSpace(_connectionString) || string.IsNullOrWhiteSpace(runName))
                return features;

            string query = @"
                DECLARE @TargetRunID INT;

                SELECT TOP 1 @TargetRunID = RunID 
                FROM dbo.ActiveRun WITH (NOLOCK) 
                WHERE RunName = @RunName;

                IF @TargetRunID IS NULL
                BEGIN
                    SELECT TOP 1 @TargetRunID = RunID 
                    FROM dbo.Run WITH (NOLOCK) 
                    WHERE RunName = @RunName 
                    ORDER BY RunID DESC;
                END

                IF @TargetRunID IS NOT NULL
                BEGIN
                    SELECT 
                        fr.OrderNo,
                        f.FeatureID,
                        f.FeatureName,
                        f.FeatureType,
                        CASE 
                            WHEN f.FeatureType = 1 THEN 'Variável'
                            WHEN f.FeatureType IN (2, 4) THEN 'Atributo'
                            WHEN f.FeatureType = 3 THEN 'Posição'
                            ELSE 'Outro'
                        END AS TipoDescricao,
                        fp.Target AS Nominal,
                        fp.LowerToleranceLimit AS LSL,
                        fp.UpperToleranceLimit AS USL,
                        fp.DecimalLength
                    FROM FeatureRun fr WITH (NOLOCK)
                    INNER JOIN Feature f WITH (NOLOCK) 
                        ON fr.FeatureID = f.FeatureID
                    LEFT JOIN FeatureProperties fp WITH (NOLOCK) 
                        ON fp.FeatureID = f.FeatureID 
                       AND fp.FeaturePropID = COALESCE(fr.FeaturePropID, f.FeaturePropID)
                    WHERE fr.RunID = @TargetRunID
                    ORDER BY fr.OrderNo ASC;
                END";

            try
            {
                using (var connection = new SqlConnection(_connectionString))
                {
                    await connection.OpenAsync();
                    using (var command = new SqlCommand(query, connection))
                    {
                        command.Parameters.AddWithValue("@RunName", runName.Trim());
                        using (var reader = await command.ExecuteReaderAsync())
                        {
                            while (await reader.ReadAsync())
                            {
                                var item = new InspectionFeatureInfo
                                {
                                    OrderNo = reader.IsDBNull(0) ? 0 : Convert.ToInt32(reader[0]),
                                    FeatureID = reader.IsDBNull(1) ? 0 : Convert.ToInt32(reader[1]),
                                    FeatureName = reader.IsDBNull(2) ? string.Empty : reader.GetString(2),
                                    FeatureType = reader.IsDBNull(3) ? (short)0 : Convert.ToInt16(reader[3]),
                                    TipoDescricao = reader.IsDBNull(4) ? string.Empty : reader.GetString(4),
                                    Nominal = reader.IsDBNull(5) ? null : Convert.ToDouble(reader[5]),
                                    LSL = reader.IsDBNull(6) ? null : Convert.ToDouble(reader[6]),
                                    USL = reader.IsDBNull(7) ? null : Convert.ToDouble(reader[7]),
                                    DecimalLength = reader.IsDBNull(8) ? null : Convert.ToInt32(reader[8])
                                };
                                features.Add(item);
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Serilog.Log.Error(ex, "Erro ao obter características da rotina de inspeção para RunName: {RunName}", runName);
            }

            return features;
        }

        public async Task<List<string>> GetAllStationsAsync()
        {
            var stations = new List<string>();
            if (string.IsNullOrWhiteSpace(_connectionString))
                return stations;

            string query = "SELECT StationName FROM dbo.Station ORDER BY StationName";

            try
            {
                using (var connection = new SqlConnection(_connectionString))
                {
                    await connection.OpenAsync();
                    using (var command = new SqlCommand(query, connection))
                    {
                        using (var reader = await command.ExecuteReaderAsync())
                        {
                            while (await reader.ReadAsync())
                            {
                                if (!reader.IsDBNull(0))
                                {
                                    stations.Add(reader.GetString(0));
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                // Fallback silencioso, mas vamos logar para diagnóstico
                Serilog.Log.Error(ex, "Erro ao obter lista de estações");
            }

            return stations;
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

        public async Task<bool> TestConnectionAsync()
        {
            if (string.IsNullOrWhiteSpace(_connectionString))
                return false;

            try
            {
                using (var connection = new SqlConnection(_connectionString))
                {
                    await connection.OpenAsync();
                    using (var command = new SqlCommand("SELECT 1", connection))
                    {
                        await command.ExecuteScalarAsync();
                        return true;
                    }
                }
            }
            catch
            {
                return false;
            }
        }

        public async Task TestConnectionThrowingAsync()
        {
            using (var connection = new SqlConnection(_connectionString))
            {
                await connection.OpenAsync();
                using (var command = new SqlCommand("SELECT 1", connection))
                {
                    await command.ExecuteScalarAsync();
                }
            }
        }
    }
}
