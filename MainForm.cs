// MainForm.cs – full implementation for Library of Ai‑Lexandria
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;
using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using Microsoft.Win32; // registry for auto‑run

namespace LibraryOfAiLexandria
{
    public class MainForm : Form
    {
        private WebView2 webView;
        private NotifyIcon trayIcon;
        private MenuStrip menuStrip;
        private StatusStrip statusStrip;
        private ToolStripStatusLabel statusLabel;
        private FileSystemWatcher? logWatcher;
        private BotManager _botManager;
        private AutoUpdater _autoUpdater;
        private static object _logLock = new object();
        
        private string AppDataPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "LibraryOfAiLexandria");
        private string BrainPath => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "LibraryOfAiLexandria", "brain");

        public MainForm()
        {
            // ----- Window basics -----
            this.Text = "Library of Ai‑Lexandria";
            this.Width = 1200;
            this.Height = 800;
            this.StartPosition = FormStartPosition.CenterScreen;
            // Load custom icon if present, otherwise use default
            var icoPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "app_icon.ico");
            try 
            {
                if (File.Exists(icoPath))
                    this.Icon = new Icon(icoPath);
                else
                    this.Icon = SystemIcons.Application;
            }
            catch 
            {
                this.Icon = SystemIcons.Application;
            }

            // ----- Menu -----
            menuStrip = new MenuStrip();
            var fileMenu = new ToolStripMenuItem("File");
            var loadConfigItem = new ToolStripMenuItem("Load Config", null, (_, __) => PostMessageToPage("readConfig"));
            var saveConfigItem = new ToolStripMenuItem("Save Config", null, (_, __) => PostMessageToPage("requestConfig"));
            var loadBotsItem = new ToolStripMenuItem("Load Bots", null, (_, __) => PostMessageToPage("readBots"));
            var saveBotsItem = new ToolStripMenuItem("Save Bots", null, (_, __) => PostMessageToPage("requestBots"));
            var enableAutoRunItem = new ToolStripMenuItem("Enable Auto‑Run", null, (_, __) => InstallStartup());
            var disableAutoRunItem = new ToolStripMenuItem("Disable Auto‑Run", null, (_, __) => UninstallStartup());
            var exitItem = new ToolStripMenuItem("Exit", null, (_, __) => Application.Exit());
            fileMenu.DropDownItems.AddRange(new ToolStripItem[] {
                loadConfigItem, saveConfigItem, new ToolStripSeparator(),
                loadBotsItem, saveBotsItem, new ToolStripSeparator(),
                enableAutoRunItem, disableAutoRunItem, new ToolStripSeparator(), exitItem});

            var helpMenu = new ToolStripMenuItem("Help");
            var aboutItem = new ToolStripMenuItem("About", null, (_, __) => ShowAbout());
            helpMenu.DropDownItems.Add(aboutItem);

            menuStrip.Items.AddRange(new ToolStripItem[] { fileMenu, helpMenu });
            this.MainMenuStrip = menuStrip;
            this.Controls.Add(menuStrip);

            // ----- Status bar -----
            statusStrip = new StatusStrip();
            statusLabel = new ToolStripStatusLabel("Bot status: idle");
            statusStrip.Items.Add(statusLabel);
            this.Controls.Add(statusStrip);

            // ----- WebView2 -----
            webView = new WebView2 { Dock = DockStyle.Fill };
            this.Controls.Add(webView);
            this.Load += MainForm_Load;

            // ----- System tray -----
            trayIcon = new NotifyIcon
            {
                Icon = SystemIcons.Application,
                Text = "Library of Ai‑Lexandria",
                Visible = true,
                ContextMenuStrip = new ContextMenuStrip()
            };
            trayIcon.ContextMenuStrip.Items.Add("Show", null, (_, __) => this.Show());
            trayIcon.ContextMenuStrip.Items.Add("Hide", null, (_, __) => this.Hide());
            trayIcon.ContextMenuStrip.Items.Add("Exit", null, (_, __) => Application.Exit());
            this.FormClosing += (_, __) => trayIcon.Visible = false;
        }

        private async void MainForm_Load(object? sender, EventArgs e)
        {
            // Show splash while WebView2 initializes
            using var splash = new SplashForm();
            splash.Show();
            var env = await CoreWebView2Environment.CreateAsync(null, Path.Combine(AppDataPath, "WebView2"));
            await webView.EnsureCoreWebView2Async(env);
            splash.Close();

            var indexPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "wwwroot", "index.html");
            webView.Source = new Uri(indexPath);

            // Bridge for messages from the page
            webView.CoreWebView2.WebMessageReceived += CoreWebView2_WebMessageReceived;

            // Setup log watcher for brain\bot.log
            var logFile = Path.Combine(BrainPath, "bot.log");
            var logDir = Path.GetDirectoryName(logFile)!;
            if (!Directory.Exists(logDir)) Directory.CreateDirectory(logDir);
            if (!File.Exists(logFile)) File.WriteAllText(logFile, "");
            logWatcher = new FileSystemWatcher(logDir, Path.GetFileName(logFile))
            {
                NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size,
                EnableRaisingEvents = true
            };
            logWatcher.Changed += LogWatcher_Changed;

            _botManager = new BotManager(msg => 
            {
                lock (_logLock)
                {
                    File.AppendAllText(logFile, $"[{DateTime.Now:HH:mm:ss}] {msg}\n");
                }
            });

            _autoUpdater = new AutoUpdater((title, msg) => 
            {
                Invoke(new Action(() => ShowToast(title, msg)));
            }, 
            msg => 
            {
                lock (_logLock)
                {
                    File.AppendAllText(logFile, $"[{DateTime.Now:HH:mm:ss}] {msg}\n");
                }
            });

            _botManager.StartBotRequested += OnStartBotRequested;
            _botManager.StopBotRequested += OnStopBotRequested;
            _botManager.ToggleMentionModeRequested += OnToggleMentionModeRequested;
            
            // Check for updates and post boot status
            Task.Run(async () => 
            {
                await Task.Delay(3000);
                var settingsPath = Path.Combine(BrainPath, "settings.json");
                if (File.Exists(settingsPath))
                {
                    var settings = System.Text.Json.JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(settingsPath), new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                    if (settings != null) 
                    {
                        // Set the global NovelAI key from P.A.I.G.E.'s settings
                        if (!string.IsNullOrWhiteSpace(settings.NovelAiKey))
                        {
                            _botManager.SetGlobalNovelAiKey(settings.NovelAiKey);
                        }

                        if (Version.TryParse(AutoUpdater.CurrentVersion, out var currV))
                        {
                            if (string.IsNullOrWhiteSpace(settings.LastRunVersion) || (Version.TryParse(settings.LastRunVersion, out var lastV) && currV > lastV))
                            {
                                settings.LastRunVersion = AutoUpdater.CurrentVersion;
                                var jsonOpts = new System.Text.Json.JsonSerializerOptions { PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase };
                                File.WriteAllText(settingsPath, System.Text.Json.JsonSerializer.Serialize(settings, jsonOpts));
                                await _botManager.PostStatusAsync(settings.StatusChannelId, $"*P.A.I.G.E. is online and has just updated to version {AutoUpdater.CurrentVersion}!*");
                            }
                            else
                            {
                                await _botManager.PostStatusAsync(settings.StatusChannelId, "*P.A.I.G.E. system boot sequence complete. Online and standing by.*");
                            }
                        }

                        await _autoUpdater.CheckForUpdatesAsync(settings);
                    }
                }
            });

            // Trigger auto-start bots
            Task.Run(() => 
            {
                var botsPath = Path.Combine(BrainPath, "bots.json");
                if (!File.Exists(botsPath)) return;
                var botsContent = File.ReadAllText(botsPath);
                var botsArray = System.Text.Json.JsonSerializer.Deserialize<System.Collections.Generic.List<BotConfig>>(botsContent, new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                if (botsArray != null)
                {
                    for (int i = 0; i < botsArray.Count; i++)
                    {
                        if (botsArray[i].AutoStart)
                        {
                            StartBotFromUi(i);
                        }
                    }
                }
            });
        }

        private void LogWatcher_Changed(object sender, FileSystemEventArgs e)
        {
            try
            {
                var lines = File.ReadAllLines(e.FullPath);
                var line = lines.Length > 0 ? lines[^1] : string.Empty;
                var msg = System.Text.Json.JsonSerializer.Serialize(new { action = "logUpdate", line });
                webView.CoreWebView2.PostWebMessageAsJson(msg);
            }
            catch { /* ignore */ }
        }

        private void PostMessageToPage(string action, object? payload = null)
        {
            var msg = new { action, payload };
            var json = System.Text.Json.JsonSerializer.Serialize(msg);
            webView.CoreWebView2.PostWebMessageAsJson(json);
        }

        private async void CoreWebView2_WebMessageReceived(object? sender, Microsoft.Web.WebView2.Core.CoreWebView2WebMessageReceivedEventArgs e)
        {
            try
            {
                var json = System.Text.Json.JsonDocument.Parse(e.WebMessageAsJson).RootElement;
                var action = json.GetProperty("action").GetString();
                switch (action)
                {
                    case "readConfig":
                        var cfgPath = Path.Combine(BrainPath, "config.json");
                        if (!Directory.Exists(BrainPath)) Directory.CreateDirectory(BrainPath);
                        if (!File.Exists(cfgPath)) File.WriteAllText(cfgPath, "{}");
                        var cfgContent = File.ReadAllText(cfgPath);
                        var cfgReply = System.Text.Json.JsonSerializer.Serialize(new { action = "configData", content = System.Text.Json.JsonDocument.Parse(cfgContent).RootElement });
                        webView.CoreWebView2.PostWebMessageAsJson(cfgReply);
                        break;
                    case "saveConfig":
                        var cfgPath2 = Path.Combine(BrainPath, "config.json");
                        if (!Directory.Exists(BrainPath)) Directory.CreateDirectory(BrainPath);
                        var newCfg = json.GetProperty("content").GetRawText();
                        File.WriteAllText(cfgPath2, newCfg);
                        var saved = System.Text.Json.JsonSerializer.Serialize(new { action = "saveResult", success = true });
                        webView.CoreWebView2.PostWebMessageAsJson(saved);
                        ShowToast("Config Saved", "Configuration written to disk.");
                        break;
                    case "readBots":
                        var botsPath = Path.Combine(BrainPath, "bots.json");
                        if (!Directory.Exists(BrainPath)) Directory.CreateDirectory(BrainPath);
                        if (!File.Exists(botsPath)) File.WriteAllText(botsPath, "[]");
                        var botsContent = File.ReadAllText(botsPath);
                        var botsReply = System.Text.Json.JsonSerializer.Serialize(new { action = "botsData", bots = System.Text.Json.JsonDocument.Parse(botsContent).RootElement });
                        webView.CoreWebView2.PostWebMessageAsJson(botsReply);
                        break;
                    case "saveBots":
                        var botsPath2 = Path.Combine(BrainPath, "bots.json");
                        if (!Directory.Exists(BrainPath)) Directory.CreateDirectory(BrainPath);
                        var botsArray = json.GetProperty("bots");
                        if (botsArray.GetArrayLength() > 10)
                        {
                            var err = System.Text.Json.JsonSerializer.Serialize(new { action = "saveResult", success = false, error = "Maximum of 10 bots allowed." });
                            webView.CoreWebView2.PostWebMessageAsJson(err);
                            break;
                        }
                        File.WriteAllText(botsPath2, botsArray.GetRawText());
                        var botsSaved = System.Text.Json.JsonSerializer.Serialize(new { action = "saveResult", success = true });
                        webView.CoreWebView2.PostWebMessageAsJson(botsSaved);
                        ShowToast("Bots Saved", $"{botsArray.GetArrayLength()} bots stored.");
                        break;
                    case "requestLogTail":
                        var logFile = Path.Combine(BrainPath, "bot.log");
                        string fullLog = "";
                        lock (_logLock) { fullLog = File.Exists(logFile) ? File.ReadAllText(logFile) : ""; }
                        var logMsg = System.Text.Json.JsonSerializer.Serialize(new { action = "logFull", content = fullLog });
                        webView.CoreWebView2.PostWebMessageAsJson(logMsg);
                        break;
                    case "startBot":
                        StartBotFromUi(json.GetProperty("botIndex").GetInt32());
                        break;
                    case "stopBot":
                        _botManager.StopBot(json.GetProperty("botIndex").GetInt32());
                        break;
                    case "requestStatuses":
                    {
                        var statusesPath = Path.Combine(BrainPath, "bots.json");
                        if (File.Exists(statusesPath))
                        {
                            var list = System.Text.Json.JsonSerializer.Deserialize<System.Collections.Generic.List<BotConfig>>(File.ReadAllText(statusesPath));
                            var statusArray = new bool[list.Count];
                            for (int i = 0; i < list.Count; i++)
                            {
                                statusArray[i] = _botManager.IsBotRunning(i);
                            }
                            var payload = new { action = "statusUpdate", statuses = statusArray };
                            webView.CoreWebView2.PostWebMessageAsJson(System.Text.Json.JsonSerializer.Serialize(payload));
                        }
                        break;
                    }
                    case "readAppSettings":
                        var settingsPath = Path.Combine(BrainPath, "settings.json");
                        var jsonOptsRead = new System.Text.Json.JsonSerializerOptions { PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase };
                        if (!File.Exists(settingsPath)) File.WriteAllText(settingsPath, System.Text.Json.JsonSerializer.Serialize(new AppSettings(), jsonOptsRead));
                        var settingsContent = File.ReadAllText(settingsPath);
                        var settingsReply = System.Text.Json.JsonSerializer.Serialize(new { action = "settingsData", settings = System.Text.Json.JsonDocument.Parse(settingsContent).RootElement });
                        webView.CoreWebView2.PostWebMessageAsJson(settingsReply);
                        break;
                    case "saveAppSettings":
                        var settingsPath2 = Path.Combine(BrainPath, "settings.json");
                        var newSettings = json.GetProperty("settings").GetRawText();
                        File.WriteAllText(settingsPath2, newSettings);
                        webView.CoreWebView2.PostWebMessageAsJson(System.Text.Json.JsonSerializer.Serialize(new { action = "saveResult", success = true }));
                        ShowToast("Settings Saved", "Auto-updater and Master Token saved.");
                        var s = System.Text.Json.JsonSerializer.Deserialize<AppSettings>(newSettings, new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                        if (s != null)
                        {
                            // Update the global NovelAI key whenever settings are saved
                            if (!string.IsNullOrWhiteSpace(s.NovelAiKey))
                            {
                                _botManager.SetGlobalNovelAiKey(s.NovelAiKey);
                            }
                            if (!string.IsNullOrWhiteSpace(s.MasterDiscordToken))
                            {
                                await _botManager.StartMasterAsync(s.MasterDiscordToken);
                            }
                        }
                        break;
                    case "importCard":
                        using (var ofd = new OpenFileDialog())
                        {
                            ofd.Filter = "SillyTavern Cards|*.png;*.json|PNG Image|*.png|JSON File|*.json";
                            ofd.Title = "Select a SillyTavern Character Card";
                            if (ofd.ShowDialog() == DialogResult.OK)
                            {
                                var config = SillyTavernImporter.ImportFromCard(ofd.FileName);
                                if (config != null)
                                {
                                    var options = new System.Text.Json.JsonSerializerOptions { PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase };
                                    webView.CoreWebView2.PostWebMessageAsJson(System.Text.Json.JsonSerializer.Serialize(new { action = "cardImported", bot = config }, options));
                                }
                                else
                                {
                                    ShowToast("Import Failed", "Could not parse the character card.");
                                }
                            }
                        }
                        break;
                    case "checkUpdates":
                        var settingsPath3 = Path.Combine(BrainPath, "settings.json");
                        if (File.Exists(settingsPath3))
                        {
                            var currentSettings = System.Text.Json.JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(settingsPath3), new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                            if (currentSettings != null) await _autoUpdater.CheckForUpdatesAsync(currentSettings);
                        }
                        break;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"WebMessage error: {ex.Message}");
            }
        }

        private void StartBotFromUi(int index)
        {
            var botsPath = Path.Combine(BrainPath, "bots.json");
            if (!File.Exists(botsPath)) return;
            var botsContent = File.ReadAllText(botsPath);
            var botsArray = System.Text.Json.JsonSerializer.Deserialize<System.Collections.Generic.List<BotConfig>>(botsContent, new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            if (botsArray != null && index >= 0 && index < botsArray.Count)
            {
                var botConfig = botsArray[index];
                _botManager.StartBot(index, botConfig);
                
                // Let's also make sure master is started if we know the token!
                var settingsPath = Path.Combine(BrainPath, "settings.json");
                if (File.Exists(settingsPath))
                {
                    var s = System.Text.Json.JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(settingsPath), new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                    if (s != null && !string.IsNullOrWhiteSpace(s.MasterDiscordToken))
                    {
                        _ = _botManager.StartMasterAsync(s.MasterDiscordToken);
                    }
                }
            }
        }

        private void OnStartBotRequested(string name)
        {
            var botsPath = Path.Combine(BrainPath, "bots.json");
            if (!File.Exists(botsPath)) return;
            var list = System.Text.Json.JsonSerializer.Deserialize<System.Collections.Generic.List<BotConfig>>(File.ReadAllText(botsPath), new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            if (list == null) return;

            for (int i = 0; i < list.Count; i++)
            {
                if (list[i].Name.Equals(name, StringComparison.OrdinalIgnoreCase))
                {
                    _botManager.StartBot(i, list[i]);
                    break;
                }
            }
        }

        private void OnStopBotRequested(string name)
        {
            var botsPath = Path.Combine(BrainPath, "bots.json");
            if (!File.Exists(botsPath)) return;
            var list = System.Text.Json.JsonSerializer.Deserialize<System.Collections.Generic.List<BotConfig>>(File.ReadAllText(botsPath), new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            if (list == null) return;

            for (int i = 0; i < list.Count; i++)
            {
                if (list[i].Name.Equals(name, StringComparison.OrdinalIgnoreCase))
                {
                    _botManager.StopBot(i);
                    break;
                }
            }
        }

        private void OnToggleMentionModeRequested(string name)
        {
            var botsPath = Path.Combine(BrainPath, "bots.json");
            if (!File.Exists(botsPath)) return;
            var list = System.Text.Json.JsonSerializer.Deserialize<System.Collections.Generic.List<BotConfig>>(File.ReadAllText(botsPath), new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            if (list == null) return;

            for (int i = 0; i < list.Count; i++)
            {
                if (list[i].Name.Equals(name, StringComparison.OrdinalIgnoreCase))
                {
                    list[i].MentionMode = !list[i].MentionMode;
                    File.WriteAllText(botsPath, System.Text.Json.JsonSerializer.Serialize(list));

                    // If it's running, restart it to apply config immediately
                    if (_botManager.IsBotRunning(i))
                    {
                        _botManager.StopBot(i);
                        _botManager.StartBot(i, list[i]);
                    }
                    
                    var botsReply = System.Text.Json.JsonSerializer.Serialize(new { action = "botsData", bots = list });
                    webView.CoreWebView2.PostWebMessageAsJson(botsReply);
                    break;
                }
            }
        }

        private void ShowAbout()
        {
            MessageBox.Show($"Library of Ai-Lexandria\nVersion {AutoUpdater.CurrentVersion}\nA Windows companion UI for your Discord bot's brain.", "About", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void ShowToast(string title, string message)
        {
            // Simple fallback using MessageBox; a real toast would require a manifest and the UWP toast package.
            MessageBox.Show(message, title);
        }

        private void InstallStartup()
        {
            try
            {
                var exePath = Application.ExecutablePath;
                using var key = Registry.CurrentUser.OpenSubKey("Software\\Microsoft\\Windows\\CurrentVersion\\Run", true);
                key?.SetValue("LibraryOfAiLexandria", exePath);
                ShowToast("Auto‑Run Enabled", "The app will start with Windows.");
            }
            catch (Exception ex)
            {
                ShowToast("Auto‑Run Error", ex.Message);
            }
        }

        private void UninstallStartup()
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey("Software\\Microsoft\\Windows\\CurrentVersion\\Run", true);
                key?.DeleteValue("LibraryOfAiLexandria", false);
                ShowToast("Auto‑Run Disabled", "The app will no longer start with Windows.");
            }
            catch (Exception ex)
            {
                ShowToast("Auto‑Run Error", ex.Message);
            }
        }
    }

    // Simple splash screen form
    public class SplashForm : Form
    {
        public SplashForm()
        {
            this.FormBorderStyle = FormBorderStyle.None;
            this.StartPosition = FormStartPosition.CenterScreen;
            this.Width = 400;
            this.Height = 250;
            this.BackColor = Color.FromArgb(30, 30, 45);
            var imgPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "app_icon.png");
            Image? splashImage = null;
            try { if (File.Exists(imgPath)) splashImage = Image.FromFile(imgPath); } catch { }
            var pic = new PictureBox
            {
                Image = splashImage ?? SystemIcons.Application.ToBitmap(),
                SizeMode = PictureBoxSizeMode.Zoom,
                Dock = DockStyle.Fill
            };
            this.Controls.Add(pic);
        }
    }
}
