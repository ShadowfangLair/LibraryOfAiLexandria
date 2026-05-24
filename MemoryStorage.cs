using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace LibraryOfAiLexandria
{
    public class ChatMessage
    {
        public string Author { get; set; } = string.Empty;
        public string Content { get; set; } = string.Empty;
        public bool IsBot { get; set; }
        public DateTime Timestamp { get; set; }
    }

    public class MemoryStorage
    {
        private readonly string _botFolder;
        private readonly string _memoryFile;

        public MemoryStorage(string botName)
        {
            var appDataPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "LibraryOfAiLexandria");
            _botFolder = Path.Combine(appDataPath, "brain", GetSafeFilename(botName));
            _memoryFile = Path.Combine(_botFolder, "memory.json");
            
            if (!Directory.Exists(_botFolder))
            {
                Directory.CreateDirectory(_botFolder);
            }
        }

        public void AppendMessage(ChatMessage message)
        {
            var history = GetHistory();
            history.Add(message);
            
            var json = JsonSerializer.Serialize(history, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(_memoryFile, json);
        }

        public List<ChatMessage> GetHistory()
        {
            if (!File.Exists(_memoryFile))
                return new List<ChatMessage>();

            try
            {
                var json = File.ReadAllText(_memoryFile);
                return JsonSerializer.Deserialize<List<ChatMessage>>(json) ?? new List<ChatMessage>();
            }
            catch
            {
                return new List<ChatMessage>();
            }
        }
        
        public List<ChatMessage> GetRecentHistory(int limit)
        {
            var history = GetHistory();
            if (history.Count <= limit)
                return history;
                
            return history.GetRange(history.Count - limit, limit);
        }

        private string GetSafeFilename(string filename)
        {
            return string.Join("_", filename.Split(Path.GetInvalidFileNameChars()));
        }
    }
}
