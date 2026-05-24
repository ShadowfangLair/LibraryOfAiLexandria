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
        public bool Advanced { get; set; }
        public string NovelAiModel { get; set; } = "kayra-v1";
        public double NovelAiTemp { get; set; } = 1.0;
        public int MemoryLimit { get; set; } = 20;
        public string SystemPrompt { get; set; } = string.Empty;
        public bool AutoStart { get; set; }
        public bool MentionMode { get; set; }
    }

    public class CharacterInstance
    {
        public BotConfig Config { get; }
        private readonly NovelAiClient _novelAi;
        private readonly MemoryStorage _memory;
        private readonly Action<string> _logCallback;

        public CharacterInstance(BotConfig config, string novelAiKey, Action<string> logCallback)
        {
            Config = config;
            _logCallback = logCallback;
            _novelAi = new NovelAiClient(novelAiKey);
            _memory = new MemoryStorage(config.Name);
        }

        public void UpdateNovelAiKey(string novelAiKey)
        {
            _novelAi.UpdateApiKey(novelAiKey);
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
            
            var response = await _novelAi.GenerateResponseAsync(prompt, Config.NovelAiModel, Config.NovelAiTemp);

            if (!string.IsNullOrWhiteSpace(response))
            {
                var stopIdx = response.IndexOf($"\n{username}:");
                if (stopIdx != -1) response = response.Substring(0, stopIdx);
                
                var stopIdx2 = response.IndexOf("\n***\n");
                if (stopIdx2 != -1) response = response.Substring(0, stopIdx2);
                
                var stopIdx3 = response.IndexOf("\n<|");
                if (stopIdx3 != -1) response = response.Substring(0, stopIdx3);
            }

            if (string.IsNullOrWhiteSpace(response))
            {
                response = "*[No response generated]*";
            }

            response = response.Trim();

            // Do not permanently poison the character's memory with API error strings
            if (!response.StartsWith("*[NovelAI"))
            {
                _memory.AppendMessage(new ChatMessage 
                { 
                    Author = Config.Name, 
                    Content = response, 
                    IsBot = true, 
                    Timestamp = DateTime.UtcNow 
                });
            }
            
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
