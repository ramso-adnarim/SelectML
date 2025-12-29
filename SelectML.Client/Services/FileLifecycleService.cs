using System;
using System.IO;
using System.Threading.Tasks;
using Serilog;

namespace SelectML.Client.Services
{
    public class FileLifecycleService
    {
        /// <summary>
        /// Moves the input file to a Backup directory immediately after reading.
        /// Performs an atomic Copy-Verify-Delete operation.
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
                // 1. Copy (overwrite if exists, though unique names are expected usually)
                File.Copy(filePath, destPath, true);

                // 2. Verify Integrity (Size Check)
                var sourceInfo = new FileInfo(filePath);
                var destInfo = new FileInfo(destPath);

                if (sourceInfo.Length == destInfo.Length)
                {
                    // 3. Delete Source
                    File.Delete(filePath);
                    Log.Information("Archived file: {Source} -> {Dest}", filePath, destPath);
                }
                else
                {
                    // Integrity check failed
                    throw new IOException($"Backup integrity check failed for {fileName}. Source size: {sourceInfo.Length}, Dest size: {destInfo.Length}");
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to archive file {File}. Source was NOT deleted.", filePath);
                // Re-throw to abort processing of this file
                throw;
            }
        }

        /// <summary>
        /// Cleans up old files from the Backup directory and Logs directory based on retention policy.
        /// Designed to run as a background task.
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
