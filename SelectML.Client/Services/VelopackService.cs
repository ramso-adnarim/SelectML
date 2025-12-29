using System;
using System.Threading.Tasks;
using Velopack;
using Velopack.Sources;
using Serilog;

namespace SelectML.Client.Services
{
    public class VelopackService
    {
        private readonly string _updateUrl;

        public VelopackService(string updateUrl)
        {
            _updateUrl = updateUrl;
        }

        public async Task<UpdateInfo?> CheckForUpdatesAsync()
        {
            if (string.IsNullOrWhiteSpace(_updateUrl))
            {
                Log.Warning("Update URL is not configured. Skipping update check.");
                return null;
            }

            try
            {
                var mgr = new UpdateManager(new SimpleWebSource(_updateUrl));

                // Check for updates
                var updateInfo = await mgr.CheckForUpdatesAsync();

                if (updateInfo == null)
                {
                    Log.Information("No updates found.");
                }
                else
                {
                     Log.Information("Update found: {Version}", updateInfo.TargetFullRelease.Version);
                }

                return updateInfo;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to check for updates");
                return null;
            }
        }

        public async Task DownloadAndInstallAsync(UpdateInfo updateInfo)
        {
             try
             {
                 var mgr = new UpdateManager(new SimpleWebSource(_updateUrl));

                 Log.Information("Downloading update...");
                 await mgr.DownloadUpdatesAsync(updateInfo);

                 Log.Information("Applying update and restarting...");
                 mgr.WaitExitThenApplyUpdates(updateInfo);
             }
             catch (Exception ex)
             {
                 Log.Error(ex, "Failed to download/install update");
                 // In a real scenario, might want to notify UI of failure
                 throw;
             }
        }
    }
}
