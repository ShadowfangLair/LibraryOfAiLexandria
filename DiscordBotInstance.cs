using System;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Discord;
using Discord.WebSocket;

namespace LibraryOfAiLexandria
{
    public class BotConfig
    {
        public string Name { get; set; } = string.Empty;
        public string DiscordToken { get; set; } = string.Empty;
        public string NovelAiKey { get; set; } = string.Empty;
        public bool Advanced { get; set; }
        public string NovelAiModel { get; set; } = "kayra-v1";
        public double NovelAiTemp { get; set; } = 1.0;
        public int MemoryLimit { get; set; } = 20;
        public string SystemPrompt { get; set; } = string.Empty;
        public bool Connected { get; set; }
    }

    public class DiscordBotInstance
    {
        private readonly BotConfig _config;
        private readonly DiscordSocketClient _client;
        private readonly NovelAiClient _novelAi;
        private readonly MemoryStorage _memory;
        private readonly Action<string> _logCallback;
        private bool _isShuttingDown;

        public bool IsConnected => _client.ConnectionState == ConnectionState.Connected;

        public DiscordBotInstance(BotConfig config, Action<string> logCallback)
        {
            _config = config;
            _logCallback = logCallback;
            
            _novelAi = new NovelAiClient(config.NovelAiKey);
            _memory = new MemoryStorage(config.Name);

            var discordConfig = new DiscordSocketConfig
            {
                GatewayIntents = GatewayIntents.Guilds | GatewayIntents.GuildMessages | GatewayIntents.MessageContent
            };
            
            _client = new DiscordSocketClient(discordConfig);
            _client.Log += LogAsync;
            _client.MessageReceived += MessageReceivedAsync;
        }

        public async Task StartAsync()
        {
            if (string.IsNullOrWhiteSpace(_config.DiscordToken))
            {
                _logCallback($"[{_config.Name}] Cannot start: Discord Token is empty.");
                return;
            }

            try
            {
                await _client.LoginAsync(TokenType.Bot, _config.DiscordToken);
                await _client.StartAsync();
                _logCallback($"[{_config.Name}] Connecting to Discord...");
            }
            catch (Exception ex)
            {
                _logCallback($"[{_config.Name}] Failed to start: {ex.Message}");
            }
        }

        public async Task StopAsync()
        {
            _isShuttingDown = true;
            try
            {
                await _client.StopAsync();
                await _client.LogoutAsync();
                _logCallback($"[{_config.Name}] Disconnected.");
            }
            catch (Exception ex)
            {
                _logCallback($"[{_config.Name}] Error during stop: {ex.Message}");
            }
        }

        private Task LogAsync(LogMessage msg)
        {
            if (!_isShuttingDown)
            {
                _logCallback($"[{_config.Name}] {msg.Message ?? msg.Exception?.Message}");
            }
            return Task.CompletedTask;
        }

        private async Task MessageReceivedAsync(SocketMessage message)
        {
            // Ignore messages from bots (including ourselves)
            if (message.Author.IsBot) return;

            // Simple check: Only respond if mentioned or if it's a DM. You could configure this further.
            bool isMentioned = message.MentionedUsers.Any(u => u.Id == _client.CurrentUser.Id);
            bool isDm = message.Channel is IDMChannel;

            if (!isMentioned && !isDm) return;

            var cleanMessage = message.CleanContent.Replace($"@{_client.CurrentUser.Username}", "").Trim();
            
            _logCallback($"[{_config.Name}] Received message from {message.Author.Username}: {cleanMessage}");

            // 1. Save user message to memory
            _memory.AppendMessage(new ChatMessage 
            { 
                Author = message.Author.Username, 
                Content = cleanMessage, 
                IsBot = false, 
                Timestamp = DateTime.UtcNow 
            });

            // 2. Build prompt for NovelAI
            var prompt = BuildPrompt();
            
            // 3. Set typing indicator
            using var typing = message.Channel.EnterTypingState();

            // 4. Generate response
            var response = await _novelAi.GenerateResponseAsync(prompt, _config.NovelAiModel, _config.NovelAiTemp);

            // NovelAI sometimes returns empty or weird stops, let's just make sure it's not totally empty
            if (string.IsNullOrWhiteSpace(response))
            {
                response = "*[No response generated]*";
            }

            // Clean up formatting that NovelAI might inject
            response = response.Trim();

            // 5. Send response to Discord
            await message.Channel.SendMessageAsync(response);

            // 6. Save bot response to memory
            _memory.AppendMessage(new ChatMessage 
            { 
                Author = _config.Name, 
                Content = response, 
                IsBot = true, 
                Timestamp = DateTime.UtcNow 
            });
            
            _logCallback($"[{_config.Name}] Responded: {response}");
        }

        private string BuildPrompt()
        {
            var sb = new StringBuilder();
            
            // System prompt or character context
            if (!string.IsNullOrWhiteSpace(_config.SystemPrompt))
            {
                sb.AppendLine(_config.SystemPrompt);
            }
            else
            {
                sb.AppendLine($"This is a chat transcript between users and a character named {_config.Name}. {_config.Name} is helpful and conversational.");
            }
            sb.AppendLine("***");

            var history = _memory.GetRecentHistory(_config.MemoryLimit);
            
            foreach (var msg in history)
            {
                sb.AppendLine($"{msg.Author}: {msg.Content}");
            }

            // Prompt NovelAI to generate the bot's next reply
            sb.Append($"{_config.Name}:");
            
            return sb.ToString();
        }
    }
}
