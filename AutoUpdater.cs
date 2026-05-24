using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;

namespace LibraryOfAiLexandria
{
    public class AppSettings
    {
        public string GithubRepo { get; set; } = string.Empty;
        public string UpdateMode { get; set; } = "prompt";
        public string MasterDiscordToken { get; set; } = string.Empty;
        public string NovelAiKey { get; set; } = string.Empty;
        public string StatusChannelId { get; set; } = "1507775019764158675";
        public string LastRunVersion { get; set; } = string.Empty;
    }

    public class AutoUpdater
    {
        public const string CurrentVersion = "1.0.16";
        private readonly Action<string, string> _uiToastCallback;
        private readonly Action<string> _logCallback;

        public AutoUpdater(Action<string, string> uiToastCallback, Action<string> logCallback)
        {
            _uiToastCallback = uiToastCallback;
            _logCallback = logCallback;
        }

        public async Task CheckForUpdatesAsync(AppSettings settings)
        {
            if (string.IsNullOrWhiteSpace(settings.GithubRepo))
            {
                _logCallback("[Updater] GitHub repo not configured. Skipping auto-update.");
                return;
            }

            try
            {
                using var client = new HttpClient();
                client.DefaultRequestHeaders.Add("User-Agent", "LibraryOfAiLexandria-AutoUpdater");
                
                _logCallback($"[Updater] Checking for updates at {settings.GithubRepo}...");
                
                var url = $"https://api.github.com/repos/{settings.GithubRepo}/releases/latest";
                var response = await client.GetAsync(url);
                
                if (!response.IsSuccessStatusCode)
                {
                    _logCallback($"[Updater] Failed to check GitHub: {response.StatusCode}");
                    return;
                }

                var jsonString = await response.Content.ReadAsStringAsync();
                using var doc = JsonDocument.Parse(jsonString);
                var tagName = doc.RootElement.GetProperty("tag_name").GetString();
                
                if (string.IsNullOrWhiteSpace(tagName)) return;

                // Simple version check (assuming tag is v1.0.1 or 1.0.1)
                var cleanTag = tagName.TrimStart('v', 'V');
                
                if (Version.TryParse(cleanTag, out var latestVersion) && Version.TryParse(CurrentVersion, out var currentVersion))
                {
                    if (latestVersion > currentVersion)
                    {
                        _logCallback($"[Updater] Update found: v{latestVersion}");
                        
                        var assets = doc.RootElement.GetProperty("assets");
                        if (assets.GetArrayLength() > 0)
                        {
                            var downloadUrl = assets[0].GetProperty("browser_download_url").GetString();
                            if (!string.IsNullOrEmpty(downloadUrl))
                            {
                                if (settings.UpdateMode == "silent")
                                {
                                    _logCallback("[Updater] Starting silent download and install...");
                                    _uiToastCallback("Updating", "A new version is downloading in the background. The app will restart shortly.");
                                    await DownloadAndInstallAsync(downloadUrl);
                                }
                                else
                                {
                                    _uiToastCallback("Update Available", $"Version {latestVersion} is available! We will install it automatically.");
                                    await DownloadAndInstallAsync(downloadUrl);
                                }
                            }
                        }
                    }
                    else
                    {
                        _logCallback("[Updater] App is up to date.");
                    }
                }
            }
            catch (Exception ex)
            {
                _logCallback($"[Updater] Error checking updates: {ex.Message}");
            }
        }

        private async Task DownloadAndInstallAsync(string downloadUrl)
        {
            try
            {
                var tempFile = Path.Combine(Path.GetTempPath(), "LibraryOfAiLexandria-Setup.exe");
                
                using var client = new HttpClient();
                var response = await client.GetAsync(downloadUrl);
                using var fs = new FileStream(tempFile, FileMode.Create, FileAccess.Write, FileShare.None);
                await response.Content.CopyToAsync(fs);
                fs.Close();

                _logCallback("[Updater] Download complete. Launching installer...");

                var startInfo = new ProcessStartInfo
                {
                    FileName = tempFile,
                    Arguments = "/VERYSILENT /SUPPRESSMSGBOXES /NORESTART",
                    UseShellExecute = true
                };

                Process.Start(startInfo);
                
                // Exit app to allow overwrite
                Environment.Exit(0);
            }
            catch (Exception ex)
            {
                _logCallback($"[Updater] Failed to install update: {ex.Message}");
            }
        }
    }
}
