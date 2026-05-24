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
        public string ChannelId { get; set; } = string.Empty;
        public string AvatarUrl { get; set; } = string.Empty;
        public string NovelAiKey { get; set; } = string.Empty;
        public bool Advanced { get; set; }
        public string NovelAiModel { get; set; } = "kayra-v1";
        public double NovelAiTemp { get; set; } = 1.0;
        public int MemoryLimit { get; set; } = 20;
        public string SystemPrompt { get; set; } = string.Empty;
    }

    public class CharacterInstance
    {
        public BotConfig Config { get; }
        private readonly NovelAiClient _novelAi;
        private readonly MemoryStorage _memory;
        private readonly Action<string> _logCallback;

        public CharacterInstance(BotConfig config, Action<string> logCallback)
        {
            Config = config;
            _logCallback = logCallback;
            _novelAi = new NovelAiClient(config.NovelAiKey);
            _memory = new MemoryStorage(config.Name);
        }

        public async Task<string> GenerateResponseAsync(string username, string cleanMessage)
        {
            _logCallback($"[{Config.Name}] Received message from {username}: {cleanMessage}");

            _memory.AppendMessage(new ChatMessage 
            { 
                Author = username, 
                Content = cleanMessage, 
                IsBot = false, 
                Timestamp = DateTime.UtcNow 
            });

            var prompt = BuildPrompt(username);
            
            string[] stops = new[] { $"\n{username}:", "\n***\n", "\n<|" };
            var response = await _novelAi.GenerateResponseAsync(prompt, Config.NovelAiModel, Config.NovelAiTemp, stops);

            if (string.IsNullOrWhiteSpace(response))
            {
                response = "*[No response generated]*";
            }

            response = response.Trim();

            _memory.AppendMessage(new ChatMessage 
            { 
                Author = Config.Name, 
                Content = response, 
                IsBot = true, 
                Timestamp = DateTime.UtcNow 
            });
            
            _logCallback($"[{Config.Name}] Generated Response: {response}");
            return response;
        }

        private string BuildPrompt(string currentUsername)
        {
            var sb = new StringBuilder();
            
            if (!string.IsNullOrWhiteSpace(Config.SystemPrompt))
            {
                sb.AppendLine($"This is a chat transcript between a user named {currentUsername} and a character named {Config.Name}.");
                sb.AppendLine(Config.SystemPrompt);
            }
            else
            {
                sb.AppendLine($"This is a chat transcript between {currentUsername} and a character named {Config.Name}. {Config.Name} is helpful and conversational.");
            }
            sb.AppendLine("***");

            var history = _memory.GetRecentHistory(Config.MemoryLimit);
            
            foreach (var msg in history)
            {
                sb.AppendLine($"{msg.Author}: {msg.Content}");
            }

            sb.Append($"{Config.Name}:");
            
            return sb.ToString();
        }
    }
}
