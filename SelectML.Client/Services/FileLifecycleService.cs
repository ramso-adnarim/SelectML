using System;
using System.IO;
using System.Threading.Tasks;
using Serilog;

namespace SelectML.Client.Services
{
    public class FileLifecycleService
    {
        /// <summary>
        /// Move o arquivo de entrada para um diretório de Backup imediatamente após a leitura.
        /// Realiza uma operação atômica de Copiar-Verificar-Deletar para garantir a integridade.
        /// </summary>
        public void ArchiveInputFile(string filePath, string watchDirectory)
        {
            string backupDir = Path.Combine(watchDirectory, "Backup");
            if (!Directory.Exists(backupDir))
            {
                Directory.CreateDirectory(backupDir);
            }

            string fileName = Path.GetFileName(filePath);
            string destPath = Path.Combine(backupDir, fileName);

            try
            {
                // 1. Copiar (sobrescreve se existir, embora nomes únicos sejam esperados)
                File.Copy(filePath, destPath, true);

                // 2. Verificar Integridade (Checagem de Tamanho)
                var sourceInfo = new FileInfo(filePath);
                var destInfo = new FileInfo(destPath);

                if (sourceInfo.Length == destInfo.Length)
                {
                    // 3. Deletar Origem
                    File.Delete(filePath);
                    Log.Information("Arquivo arquivado: {Source} -> {Dest}", filePath, destPath);
                }
                else
                {
                    // Falha na checagem de integridade
                    throw new IOException($"Falha na verificação de integridade do backup para {fileName}. Tam Origem: {sourceInfo.Length}, Tam Destino: {destInfo.Length}");
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Falha ao arquivar arquivo {File}. A origem NÃO foi deletada.", filePath);
                // Relançar para abortar o processamento deste arquivo
                throw;
            }
        }

        /// <summary>
        /// Limpa arquivos antigos do diretório de Backup e Logs baseado na política de retenção.
        /// Projetado para rodar como uma tarefa em segundo plano.
        /// </summary>
        public async Task PerformCleanupAsync(string watchDirectory, int retentionDays)
        {
            if (string.IsNullOrEmpty(watchDirectory) || !Directory.Exists(watchDirectory)) return;

            await Task.Run(() =>
            {
                var cutoffDate = DateTime.Now.AddDays(-retentionDays);
                Log.Information("Starting cleanup for files older than {Date} ({Days} days retention)", cutoffDate, retentionDays);

                // 1. Cleanup Backup Directory
                string backupDir = Path.Combine(watchDirectory, "Backup");
                CleanupDirectory(backupDir, cutoffDate);

                // 2. Cleanup Logs Directory (assuming relative to executable)
                string logsDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "logs");
                CleanupDirectory(logsDir, cutoffDate);
            });
        }

        private void CleanupDirectory(string path, DateTime cutoffDate)
        {
            if (!Directory.Exists(path)) return;

            try
            {
                var files = Directory.GetFiles(path);
                foreach (var file in files)
                {
                    try
                    {
                        var fi = new FileInfo(file);
                        if (fi.CreationTime < cutoffDate)
                        {
                            fi.Delete();
                            Log.Information("Deleted old file: {File}", file);
                        }
                    }
                    catch (Exception ex)
                    {
                        // Log but continue - file might be in use
                        Log.Warning(ex, "Could not delete old file: {File}", file);
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error during cleanup of directory {Dir}", path);
            }
        }
    }
}
