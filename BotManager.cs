using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Discord;
using Discord.WebSocket;
using Discord.Webhook;

namespace LibraryOfAiLexandria
{
    public class BotManager
    {
        private DiscordSocketClient? _client;
        private readonly Dictionary<int, CharacterInstance> _characters = new();
        private readonly Action<string> _logCallback;
        private string _currentToken = string.Empty;
        private string _globalNovelAiKey = string.Empty;

        public event Action<string>? StartBotRequested;
        public event Action<string>? StopBotRequested;
        public event Action<string>? ToggleMentionModeRequested;

        public bool IsMasterConnected => _client?.ConnectionState == ConnectionState.Connected;

        public BotManager(Action<string> logCallback)
        {
            _logCallback = logCallback;
        }

        public void SetGlobalNovelAiKey(string key)
        {
            _globalNovelAiKey = key?.Trim() ?? string.Empty;
            foreach (var charInstance in _characters.Values)
            {
                charInstance.UpdateNovelAiKey(_globalNovelAiKey);
            }
            _logCallback("[Master] Global NovelAI key updated.");
        }

        public async Task StartMasterAsync(string token)
        {
            if (string.IsNullOrWhiteSpace(token))
            {
                _logCallback("[Master] Cannot start: Discord Token is empty.");
                return;
            }

            if (_client != null && IsMasterConnected && _currentToken == token)
            {
                // Already connected with same token
                return;
            }

            await StopMasterAsync();

            _currentToken = token;
            
            var discordConfig = new DiscordSocketConfig
            {
                GatewayIntents = GatewayIntents.Guilds | GatewayIntents.GuildMessages | GatewayIntents.MessageContent
            };
            
            _client = new DiscordSocketClient(discordConfig);
            _client.Log += LogAsync;
            _client.MessageReceived += MessageReceivedAsync;
            _client.Ready += Client_ReadyAsync;
            _client.SlashCommandExecuted += SlashCommandExecutedAsync;

            try
            {
                await _client.LoginAsync(TokenType.Bot, token);
                await _client.StartAsync();
                _logCallback("[Master] Connecting to Discord...");
            }
            catch (Exception ex)
            {
                _logCallback($"[Master] Failed to start: {ex.Message}");
            }
        }

        public async Task StopMasterAsync()
        {
            if (_client != null)
            {
                try
                {
                    _client.Log -= LogAsync;
                    _client.MessageReceived -= MessageReceivedAsync;
                    _client.Ready -= Client_ReadyAsync;
                    _client.SlashCommandExecuted -= SlashCommandExecutedAsync;
                    await _client.StopAsync();
                    await _client.LogoutAsync();
                    _logCallback("[Master] Disconnected.");
                }
                catch (Exception ex)
                {
                    _logCallback($"[Master] Error during stop: {ex.Message}");
                }
                finally
                {
                    _client.Dispose();
                    _client = null;
                }
            }
        }

        public void StartBot(int index, BotConfig config)
        {
            _characters[index] = new CharacterInstance(config, _globalNovelAiKey, _logCallback);
            _logCallback($"[Master] Loaded character plugin: {config.Name} (Channel: {config.ChannelId})");
        }

        public void StopBot(int index)
        {
            if (_characters.TryGetValue(index, out var chara))
            {
                _logCallback($"[Master] Unloaded character plugin: {chara.Config.Name}");
                _characters.Remove(index);
            }
        }

        private Task LogAsync(LogMessage msg)
        {
            _logCallback($"[Master] {msg.Message ?? msg.Exception?.Message}");
            return Task.CompletedTask;
        }

        private async Task MessageReceivedAsync(SocketMessage message)
        {
            // Ignore bots
            if (message.Author.IsBot) return;

            // Must be in a text channel to use webhooks easily (and threads are IThreadChannel which implement ITextChannel sort of, but actually they don't, they are IThreadChannel but Webhooks can be sent to Threads in Discord.Net if we provide the ThreadId!)
            if (message.Channel is not ITextChannel textChannel && message.Channel is not IThreadChannel) return;

            // Check if this is a command for PAIGE to create a room
            if (message.MentionedUsers.Any(u => u.Id == _client?.CurrentUser?.Id) && message.Content.ToLower().Contains("create room with"))
            {
                await HandleCreateRoomCommandAsync(message);
                return;
            }

            // Note: PAIGE commands (/paige-start, etc.) are now handled by SlashCommandExecutedAsync!
            // But we keep this for backwards compatibility just in case they manage to send it as text.
            var contentLower = message.Content.ToLower().Trim();
            if (contentLower.StartsWith("/paige-start-") || contentLower.StartsWith("/paige-stop-") || contentLower.StartsWith("/paige-restart-bots") || contentLower.StartsWith("/paige-mention-"))
            {
                await HandlePaigeCommandTextAsync(message);
                return;
            }

            // Check for Auto-Ingestion of SillyTavern Character Cards
            if (message.Attachments.Any())
            {
                await HandleAutoIngestionAsync(message);
            }

            // Find if any character is confined to this channel, or if Mention Mode is active
            var channelStr = message.Channel.Id.ToString();
            var character = _characters.Values.FirstOrDefault(c => c.Config.ChannelId == channelStr);

            var cleanMessage = message.CleanContent.Trim();

            if (character == null)
            {
                // No character assigned to this channel. Let's check for Mention Mode!
                var lowerMessage = cleanMessage.ToLower();
                character = _characters.Values.FirstOrDefault(c => c.Config.MentionMode && lowerMessage.Contains(c.Config.Name.ToLower()));
                
                if (character == null)
                {
                    // No character assigned to this channel and no mention mode match
                    return;
                }
            }
            
            // Set typing indicator
            using var typing = message.Channel.EnterTypingState();

            try
            {
                // Generate Response from Character
                var response = await character.GenerateResponseAsync(message.Author.Username, cleanMessage);

                // Send via Webhook!
                await SendViaWebhookAsync(message.Channel, character.Config.Name, character.Config.AvatarUrl, response);
            }
            catch (Exception ex)
            {
                _logCallback($"[Master] Message handling error: {ex.Message}");
            }
        }

        private async Task HandleCreateRoomCommandAsync(SocketMessage message)
        {
            try
            {
                var targetName = message.Content.Substring(message.Content.ToLower().IndexOf("create room with") + 16).Trim();
                var character = _characters.Values.FirstOrDefault(c => c.Config.Name.Equals(targetName, StringComparison.OrdinalIgnoreCase));

                if (character == null)
                {
                    await message.Channel.SendMessageAsync($"Sorry, I couldn't find a character plugin named '{targetName}'.");
                    return;
                }

                if (message.Channel is ITextChannel textChannel)
                {
                    var thread = await textChannel.CreateThreadAsync($"Private RP: {message.Author.Username} & {character.Config.Name}", ThreadType.PublicThread, ThreadArchiveDuration.OneDay, message);
                    
                    // Assign the character to the new thread!
                    character.Config.ChannelId = thread.Id.ToString();
                    
                    await thread.SendMessageAsync($"Welcome {message.Author.Mention}! I have summoned {character.Config.Name} to this room for a private roleplay. Have fun!");
                    _logCallback($"[Master] Created private thread {thread.Id} for {character.Config.Name}.");
                }
            }
            catch (Exception ex)
            {
                _logCallback($"[Master] Failed to create room: {ex.Message}");
            }
        }

        private async Task HandleAutoIngestionAsync(SocketMessage message)
        {
            var attachment = message.Attachments.FirstOrDefault(a => a.Filename.EndsWith(".png", StringComparison.OrdinalIgnoreCase) || a.Filename.EndsWith(".json", StringComparison.OrdinalIgnoreCase));
            if (attachment == null) return;

            try
            {
                var tempFile = Path.Combine(Path.GetTempPath(), attachment.Filename);
                using var client = new System.Net.Http.HttpClient();
                var response = await client.GetAsync(attachment.Url);
                using var fs = new FileStream(tempFile, FileMode.Create, FileAccess.Write, FileShare.None);
                await response.Content.CopyToAsync(fs);
                fs.Close();

                var newConfig = SillyTavernImporter.ImportFromCard(tempFile);
                if (newConfig != null && !string.IsNullOrWhiteSpace(newConfig.Name))
                {
                    // Card found! Auto-load it and assign it to this channel!
                    newConfig.ChannelId = message.Channel.Id.ToString();
                    if (attachment.Filename.EndsWith(".png", StringComparison.OrdinalIgnoreCase))
                    {
                        newConfig.AvatarUrl = attachment.Url;
                    }

                    int newId = _characters.Count > 0 ? _characters.Keys.Max() + 1 : 0;
                    _characters[newId] = new CharacterInstance(newConfig, _globalNovelAiKey, _logCallback);
                    
                    await message.Channel.SendMessageAsync($"*[P.A.I.G.E.] Automatically detected and imported character plugin: **{newConfig.Name}**! They are now confined to this channel.*");
                    
                    // Trigger UI update by logging (UI reads logs, but saving to bots.json is better. For now we just load it dynamically.)
                    _logCallback($"[Master] Auto-ingested Character Card '{newConfig.Name}' into Memory Slot {newId} in channel {message.Channel.Id}");
                }
                
                File.Delete(tempFile);
            }
            catch (Exception ex)
            {
                _logCallback($"[Master] Failed to auto-ingest card: {ex.Message}");
            }
        }

        private async Task HandlePaigeCommandTextAsync(SocketMessage message)
        {
            var text = message.Content.Trim();
            if (text.StartsWith("/paige-restart-bots", StringComparison.OrdinalIgnoreCase))
            {
                _logCallback("[Master] Received remote restart command.");
                var keys = _characters.Keys.ToList();
                foreach(var k in keys) StopBot(k);
                await message.Channel.SendMessageAsync("*[P.A.I.G.E.] Shutting down all active characters. Please start them again manually or via auto-start.*");
                return;
            }

            if (text.StartsWith("/paige-start-", StringComparison.OrdinalIgnoreCase))
            {
                var name = text.Substring(13).Trim();
                _logCallback($"[Master] Remote start requested for '{name}'.");
                StartBotRequested?.Invoke(name);
                await message.Channel.SendMessageAsync($"*[P.A.I.G.E.] Attempting to boot up '{name}'...*");
                return;
            }

            if (text.StartsWith("/paige-stop-", StringComparison.OrdinalIgnoreCase))
            {
                var name = text.Substring(12).Trim();
                _logCallback($"[Master] Remote stop requested for '{name}'.");
                StopBotRequested?.Invoke(name);
                await message.Channel.SendMessageAsync($"*[P.A.I.G.E.] Shutting down '{name}'...*");
                return;
            }

            if (text.StartsWith("/paige-mention-", StringComparison.OrdinalIgnoreCase))
            {
                var name = text.Substring(15).Trim();
                _logCallback($"[Master] Remote mention mode toggle requested for '{name}'.");
                ToggleMentionModeRequested?.Invoke(name);
                await message.Channel.SendMessageAsync($"*[P.A.I.G.E.] Toggling mention mode for '{name}'...*");
                return;
            }
        }

        private async Task Client_ReadyAsync()
        {
            if (_client == null) return;
            try
            {
                var startCmd = new SlashCommandBuilder()
                    .WithName("paige-start")
                    .WithDescription("Boot up a specific character plugin")
                    .AddOption("character", ApplicationCommandOptionType.String, "Name of the character", isRequired: true);
                
                var stopCmd = new SlashCommandBuilder()
                    .WithName("paige-stop")
                    .WithDescription("Shut down a specific character plugin")
                    .AddOption("character", ApplicationCommandOptionType.String, "Name of the character", isRequired: true);
                
                var restartCmd = new SlashCommandBuilder()
                    .WithName("paige-restart-bots")
                    .WithDescription("Shut down all active characters");
                
                var mentionCmd = new SlashCommandBuilder()
                    .WithName("paige-mention")
                    .WithDescription("Toggle mention mode for a character")
                    .AddOption("character", ApplicationCommandOptionType.String, "Name of the character", isRequired: true);

                await _client.Rest.CreateGlobalCommand(startCmd.Build());
                await _client.Rest.CreateGlobalCommand(stopCmd.Build());
                await _client.Rest.CreateGlobalCommand(restartCmd.Build());
                await _client.Rest.CreateGlobalCommand(mentionCmd.Build());
                
                _logCallback("[Master] Registered global slash commands.");
            }
            catch (Exception ex)
            {
                _logCallback($"[Master] Failed to register slash commands: {ex.Message}");
            }
        }

        private async Task SlashCommandExecutedAsync(SocketSlashCommand command)
        {
            try
            {
                if (command.CommandName == "paige-restart-bots")
                {
                    _logCallback("[Master] Received remote restart command via slash command.");
                    var keys = _characters.Keys.ToList();
                    foreach (var k in keys) StopBot(k);
                    await command.RespondAsync("*[P.A.I.G.E.] Shutting down all active characters. Please start them again manually or via auto-start.*");
                    return;
                }

                if (command.CommandName == "paige-start")
                {
                    var name = command.Data.Options.First().Value.ToString() ?? "";
                    _logCallback($"[Master] Remote start requested for '{name}' via slash command.");
                    StartBotRequested?.Invoke(name);
                    await command.RespondAsync($"*[P.A.I.G.E.] Attempting to boot up '{name}'...*");
                    return;
                }

                if (command.CommandName == "paige-stop")
                {
                    var name = command.Data.Options.First().Value.ToString() ?? "";
                    _logCallback($"[Master] Remote stop requested for '{name}' via slash command.");
                    StopBotRequested?.Invoke(name);
                    await command.RespondAsync($"*[P.A.I.G.E.] Shutting down '{name}'...*");
                    return;
                }

                if (command.CommandName == "paige-mention")
                {
                    var name = command.Data.Options.First().Value.ToString() ?? "";
                    _logCallback($"[Master] Remote mention mode toggle requested for '{name}' via slash command.");
                    ToggleMentionModeRequested?.Invoke(name);
                    await command.RespondAsync($"*[P.A.I.G.E.] Toggling mention mode for '{name}'...*");
                    return;
                }
            }
            catch (Exception ex)
            {
                _logCallback($"[Master] Error handling slash command: {ex.Message}");
                if (!command.HasResponded)
                {
                    await command.RespondAsync($"*[P.A.I.G.E.] Error executing command: {ex.Message}*", ephemeral: true);
                }
            }
        }

        public async Task PostStatusAsync(string channelId, string message)
        {
            if (_client == null || !IsMasterConnected || !ulong.TryParse(channelId, out var cid)) return;

            try
            {
                var channel = await _client.GetChannelAsync(cid) as ITextChannel;
                if (channel != null)
                {
                    await channel.SendMessageAsync(message);
                }
            }
            catch(Exception ex)
            {
                _logCallback($"[Master] Failed to post status: {ex.Message}");
            }
        }

        private async Task SendViaWebhookAsync(ISocketMessageChannel channel, string username, string avatarUrl, string content)
        {
            try
            {
                ITextChannel? baseChannel = channel as ITextChannel;
                SocketThreadChannel? thread = channel as SocketThreadChannel;

                if (baseChannel == null && thread != null)
                {
                    baseChannel = thread.ParentChannel as ITextChannel;
                }

                if (baseChannel == null)
                {
                    // Fallback to normal send if we really can't get a webhook going
                    await channel.SendMessageAsync($"**{username}**: {content}");
                    return;
                }

                var webhooks = await ((IIntegrationChannel)baseChannel).GetWebhooksAsync();
                var webhook = webhooks.FirstOrDefault(w => w.Name == "AiLexandriaWebhook");

                if (webhook == null)
                {
                    _logCallback($"[Master] Creating new webhook in channel {baseChannel.Name}...");
                    webhook = await ((IIntegrationChannel)baseChannel).CreateWebhookAsync("AiLexandriaWebhook");
                }

                using var webhookClient = new DiscordWebhookClient(webhook);
                
                string? validAvatar = string.IsNullOrWhiteSpace(avatarUrl) ? null : avatarUrl;

                if (thread != null)
                {
                    await webhookClient.SendMessageAsync(content, username: username, avatarUrl: validAvatar, threadId: thread.Id);
                }
                else
                {
                    await webhookClient.SendMessageAsync(content, username: username, avatarUrl: validAvatar);
                }
            }
            catch (Exception ex)
            {
                _logCallback($"[Master] Webhook error: {ex.Message}");
            }
        }

        // Keep this for UI compatibility for now (the UI asks if a bot is running)
        // With the new architecture, if Master is running, all loaded characters are "running".
        public bool IsBotRunning(int index)
        {
            return IsMasterConnected && _characters.ContainsKey(index);
        }
    }
}
